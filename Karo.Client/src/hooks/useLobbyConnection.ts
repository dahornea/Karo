import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { BoardIntegrityResult, DevelopmentCardType, DevelopmentDeckComposition, ResourceType, SetupStep } from '../types/game';
import type { GameStartedEvent, JoinRoomResult, ResumeRoomSessionResult, Room } from '../types/lobby';
import { clearPlayerSession, getActivePlayerSession, savePlayerSession } from '../services/playerSessionStore';
import { getFriendlyGameError, makeFriendlyGameError, type FriendlyGameError } from '../utils/gameErrors';

export type ConnectionPhase = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? import.meta.env.VITE_API_URL ?? 'http://127.0.0.1:5193')
  .replace(/\/$/, '');
const signalRHubUrl = import.meta.env.VITE_SIGNALR_HUB_URL ?? `${apiBaseUrl}/hubs/lobby`;

export function useLobbyConnection() {
  const connectionRef = useRef<HubConnection | null>(null);
  const gameRef = useRef<GameStartedEvent | null>(null);
  const resumeInFlightRef = useRef(false);
  const [connectionPhase, setConnectionPhase] = useState<ConnectionPhase>('connecting');
  const [playerId, setPlayerId] = useState<string | null>(null);
  const [room, setRoom] = useState<Room | null>(null);
  const [gameStarted, setGameStarted] = useState<GameStartedEvent | null>(null);
  const [pendingAction, setPendingAction] = useState<string | null>(null);
  const [error, setError] = useState<FriendlyGameError | null>(null);

  const applyGameState = useCallback((nextGame: GameStartedEvent) => {
    const current = gameRef.current;
    const isNewMatch = !current || current.matchId !== nextGame.matchId;
    if (isNewMatch || nextGame.gameStateVersion > current.gameStateVersion) {
      gameRef.current = nextGame;
      setGameStarted(nextGame);
    }
  }, []);

  const applyRecoveredSession = useCallback((result: ResumeRoomSessionResult, playerName?: string) => {
    const recoveredPlayerName = playerName
      ?? result.room.players.find((player) => player.playerId === result.session.playerId)?.playerName
      ?? 'Player';
    savePlayerSession({ ...result.session, playerName: recoveredPlayerName });
    setPlayerId(result.session.playerId);
    setRoom(result.room);
    if (result.game) {
      applyGameState(result.game);
    } else {
      gameRef.current = null;
      setGameStarted(null);
    }
    setError(null);
  }, [applyGameState]);

  const restoreSession = useCallback(async () => {
    const connection = connectionRef.current;
    const session = getActivePlayerSession();
    if (!connection || connection.state !== HubConnectionState.Connected || !session || resumeInFlightRef.current) {
      return;
    }

    resumeInFlightRef.current = true;
    try {
      const result = await connection.invoke<ResumeRoomSessionResult>(
        'ResumeRoomSession',
        session.roomCode,
        session.playerId,
        session.reconnectToken
      );
      applyRecoveredSession(result, session.playerName);
    } catch (exception) {
      const friendly = getFriendlyGameError(exception);
      const detail = `${friendly.title} ${friendly.message}`.toLowerCase();
      if (detail.includes('session') || detail.includes('room') || detail.includes('reconnect')) {
        clearPlayerSession(session.roomCode);
        setPlayerId(null);
        setRoom(null);
        gameRef.current = null;
        setGameStarted(null);
      }
      setError(friendly);
    } finally {
      resumeInFlightRef.current = false;
    }
  }, [applyRecoveredSession]);

  useEffect(() => {
    let disposed = false;
    const connection = new HubConnectionBuilder()
      .withUrl(signalRHubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;
    connection.on('RoomUpdated', (nextRoom: Room) => {
      setRoom((currentRoom) => !currentRoom || nextRoom.roomStateVersion > currentRoom.roomStateVersion ? nextRoom : currentRoom);
    });
    connection.on('GameStarted', applyGameState);
    connection.on('GameUpdated', applyGameState);
    connection.on('ReturnedToLobby', (nextRoom: Room) => {
      setRoom(nextRoom);
      gameRef.current = null;
      setGameStarted(null);
    });
    connection.on('RoomClosed', (roomCode: string) => {
      clearPlayerSession(roomCode);
      setPlayerId(null);
      setRoom(null);
      gameRef.current = null;
      setGameStarted(null);
      setError(makeFriendlyGameError('Room closed', 'This room no longer exists.'));
    });
    connection.on('SessionReplaced', () => {
      const session = getActivePlayerSession();
      clearPlayerSession(session?.roomCode);
      setPlayerId(null);
      setRoom(null);
      gameRef.current = null;
      setGameStarted(null);
      setError(makeFriendlyGameError('Session moved', 'This player session is active in another browser tab or window.'));
    });

    connection.onreconnecting(() => setConnectionPhase('reconnecting'));
    connection.onreconnected(async () => {
      setConnectionPhase('connected');
      setError(null);
      await restoreSession();
    });
    connection.onclose(() => setConnectionPhase('disconnected'));

    const startConnection = async () => {
      try {
        await connection.start();
        if (disposed) {
          await connection.stop();
          return;
        }

        setConnectionPhase('connected');
        setError(null);
        await restoreSession();
      } catch {
        if (disposed) {
          return;
        }

        setConnectionPhase('disconnected');
        setError(makeFriendlyGameError('Connection unavailable', 'Could not connect to the Karo server.'));
      }
    };

    // Deferring startup lets React's development-only Strict Mode probe dispose
    // its first effect before SignalR begins negotiating a throwaway connection.
    const startTimer = window.setTimeout(() => void startConnection(), 0);

    return () => {
      disposed = true;
      window.clearTimeout(startTimer);
      if (connectionRef.current === connection) {
        connectionRef.current = null;
      }
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop().catch(() => undefined);
      }
    };
  }, [applyGameState, restoreSession]);

  const clearError = useCallback(() => setError(null), []);

  const invoke = useCallback(
    async <T,>(actionName: string, methodName: string, ...args: unknown[]): Promise<T> => {
      const connection = connectionRef.current;
      if (!connection || connection.state !== HubConnectionState.Connected) {
        const connectionError = makeFriendlyGameError('Connection unavailable', 'The Karo server is reconnecting. Try again in a moment.');
        setError(connectionError);
        throw new Error(connectionError.message);
      }

      setPendingAction(actionName);
      setError(null);
      try {
        return await connection.invoke<T>(methodName, ...args);
      } catch (exception) {
        setError(getFriendlyGameError(exception));
        throw exception;
      } finally {
        setPendingAction(null);
      }
    },
    []
  );

  const applyJoinResult = useCallback((result: JoinRoomResult, playerName: string) => {
    savePlayerSession({ ...result.session, playerName });
    setPlayerId(result.playerId);
    setRoom(result.room);
    gameRef.current = null;
    setGameStarted(null);
  }, []);

  const createRoom = useCallback(async (playerName: string) => {
    const result = await invoke<JoinRoomResult>('Creating room', 'CreateRoom', playerName);
    applyJoinResult(result, playerName.trim());
  }, [applyJoinResult, invoke]);

  const joinRoom = useCallback(async (roomCode: string, playerName: string) => {
    const result = await invoke<JoinRoomResult>('Joining room', 'JoinRoom', roomCode, playerName);
    applyJoinResult(result, playerName.trim());
  }, [applyJoinResult, invoke]);

  const leaveRoom = useCallback(async (roomCode: string) => {
    await invoke<void>('Leaving room', 'LeaveRoom', roomCode);
    clearPlayerSession(roomCode);
    setPlayerId(null);
    setRoom(null);
    gameRef.current = null;
    setGameStarted(null);
  }, [invoke]);

  const forfeitMatch = useCallback(async (roomCode: string) => {
    await invoke<void>('Forfeiting match', 'ForfeitMatch', roomCode);
    clearPlayerSession(roomCode);
    setPlayerId(null);
    setRoom(null);
    gameRef.current = null;
    setGameStarted(null);
  }, [invoke]);

  const recoverCurrentSession = useCallback(async () => {
    const result = await invoke<ResumeRoomSessionResult>('Recovering your seat', 'RecoverCurrentSession');
    applyRecoveredSession(result);
  }, [applyRecoveredSession, invoke]);

  return {
    connectionPhase,
    isActionBlocked: connectionPhase !== 'connected',
    playerId,
    room,
    gameStarted,
    pendingAction,
    error,
    clearError,
    createRoom,
    joinRoom,
    startGame: (roomCode: string) => invoke<void>('Starting game', 'StartGame', roomCode),
    setReady: (roomCode: string, isReady: boolean) => invoke<void>('Updating readiness', 'SetReady', roomCode, isReady),
    recoverCurrentSession,
    leaveRoom,
    forfeitMatch,
    returnRoomToLobby: (roomCode: string) => invoke<void>('Returning to lobby', 'ReturnRoomToLobby', roomCode),
    continueWithoutPlayer: (roomCode: string, playerIdToRemove: string) => invoke<void>('Continuing match', 'ContinueWithoutPlayer', roomCode, playerIdToRemove),
    endPausedMatch: (roomCode: string) => invoke<void>('Ending paused match', 'EndPausedMatch', roomCode),
    buyDevelopmentCard: (roomCode: string) => invoke<void>('Buying development card', 'BuyDevelopmentCard', roomCode),
    tradeWithBank: (roomCode: string, offeredResource: ResourceType, requestedResource: ResourceType) =>
      invoke<void>('Trading with harbor', 'MaritimeTrade', roomCode, offeredResource, requestedResource),
    createTradeOffer: (roomCode: string, targetPlayerId: string, offeredResources: Partial<Record<ResourceType, number>>, requestedResources: Partial<Record<ResourceType, number>>) =>
      invoke<void>('Creating trade offer', 'CreateTradeOffer', roomCode, targetPlayerId, offeredResources, requestedResources),
    acceptTradeOffer: (roomCode: string, tradeOfferId: string) => invoke<void>('Accepting trade offer', 'AcceptTradeOffer', roomCode, tradeOfferId),
    rejectTradeOffer: (roomCode: string, tradeOfferId: string) => invoke<void>('Rejecting trade offer', 'RejectTradeOffer', roomCode, tradeOfferId),
    cancelTradeOffer: (roomCode: string, tradeOfferId: string) => invoke<void>('Canceling trade offer', 'CancelTradeOffer', roomCode, tradeOfferId),
    endTurn: (roomCode: string) => invoke<void>('Ending turn', 'EndTurn', roomCode),
    placeSetupCamp: (roomCode: string, vertexId: string) => invoke<void>('Placing setup camp', 'PlaceSetupCamp', roomCode, vertexId),
    placeSetupTrail: (roomCode: string, edgeId: string) => invoke<void>('Placing setup trail', 'PlaceSetupTrail', roomCode, edgeId),
    buildTrail: (roomCode: string, edgeId: string) => invoke<void>('Building trail', 'BuildTrail', roomCode, edgeId),
    buildCamp: (roomCode: string, vertexId: string) => invoke<void>('Building camp', 'BuildCamp', roomCode, vertexId),
    buildStronghold: (roomCode: string, vertexId: string) => invoke<void>('Building stronghold', 'BuildStronghold', roomCode, vertexId),
    placeFreeTrail: (roomCode: string, edgeId: string) => invoke<void>('Placing free trail', 'PlaceFreeTrail', roomCode, edgeId),
    rollDice: (roomCode: string) => invoke<void>('Rolling dice', 'RollDice', roomCode),
    discardForWarden: (roomCode: string, discardedResources: Partial<Record<ResourceType, number>>) => invoke<void>('Discarding for Warden', 'DiscardForWarden', roomCode, discardedResources),
    moveWarden: (roomCode: string, targetTileId: string) => invoke<void>('Moving Warden', 'MoveWarden', roomCode, targetTileId),
    stealFromWardenVictim: (roomCode: string, victimPlayerId: string) => invoke<void>('Stealing with Warden', 'StealFromWardenVictim', roomCode, victimPlayerId),
    playYearOfPlenty: (roomCode: string, cardId: string, selectedResources: ResourceType[]) => invoke<void>('Playing Year of Plenty', 'PlayYearOfPlenty', roomCode, cardId, selectedResources),
    playMonopoly: (roomCode: string, cardId: string, selectedResource: ResourceType) => invoke<void>('Playing Monopoly', 'PlayMonopoly', roomCode, cardId, selectedResource),
    playKnight: (roomCode: string, cardId: string, targetTileId: string, victimPlayerId: string | null) => invoke<void>('Playing Knight', 'PlayKnight', roomCode, cardId, targetTileId, victimPlayerId),
    startRoadBuilding: (roomCode: string, cardId: string) => invoke<void>('Playing Road Building', 'StartRoadBuilding', roomCode, cardId),
    cancelActiveDevelopmentCard: (roomCode: string) => invoke<void>('Canceling development effect', 'CancelActiveDevelopmentCard', roomCode),
    debugAddResource: (roomCode: string, targetPlayerId: string, resource: ResourceType, amount: number) => invoke<void>('Debug: adding resource', 'DebugAddResource', roomCode, targetPlayerId, resource, amount),
    debugSetTestingResources: (roomCode: string, targetPlayerId: string) => invoke<void>('Debug: setting testing resources', 'DebugSetTestingResources', roomCode, targetPlayerId),
    debugClearResources: (roomCode: string, targetPlayerId: string) => invoke<void>('Debug: clearing resources', 'DebugClearResources', roomCode, targetPlayerId),
    debugSetCurrentPlayer: (roomCode: string, targetPlayerId: string) => invoke<void>('Debug: forcing turn', 'DebugSetCurrentPlayer', roomCode, targetPlayerId),
    debugForceDiceRoll: (roomCode: string, diceValue: number) => invoke<void>('Debug: forcing dice', 'DebugForceDiceRoll', roomCode, diceValue),
    debugResetRollState: (roomCode: string) => invoke<void>('Debug: resetting roll', 'DebugResetRollState', roomCode),
    debugMoveWarden: (roomCode: string, targetTileId: string) => invoke<void>('Debug: moving Warden', 'DebugMoveWarden', roomCode, targetTileId),
    debugClearWardenState: (roomCode: string) => invoke<void>('Debug: clearing Warden state', 'DebugClearWardenState', roomCode),
    debugSkipSetup: (roomCode: string) => invoke<void>('Debug: skipping setup', 'DebugSkipSetup', roomCode),
    debugForceSetupStep: (roomCode: string, targetPlayerId: string, setupStep: SetupStep) => invoke<void>('Debug: forcing setup step', 'DebugForceSetupStep', roomCode, targetPlayerId, setupStep),
    debugSetVictoryPoints: (roomCode: string, targetPlayerId: string, points: number) => invoke<void>('Debug: setting victory points', 'DebugSetVictoryPoints', roomCode, targetPlayerId, points),
    debugTriggerWinCheck: (roomCode: string, targetPlayerId: string) => invoke<void>('Debug: checking win', 'DebugTriggerWinCheck', roomCode, targetPlayerId),
    debugRecalculateLongestTrail: (roomCode: string) => invoke<void>('Debug: recalculating longest trail', 'DebugRecalculateLongestTrail', roomCode),
    debugGiveDevelopmentCard: (roomCode: string, targetPlayerId: string, cardType: DevelopmentCardType | 'Random') => invoke<void>('Debug: giving development card', 'DebugGiveDevelopmentCard', roomCode, targetPlayerId, cardType),
    debugClearDevelopmentCards: (roomCode: string, targetPlayerId: string) => invoke<void>('Debug: clearing development cards', 'DebugClearDevelopmentCards', roomCode, targetPlayerId),
    debugResetDevelopmentCardPlayLimit: (roomCode: string, targetPlayerId: string) => invoke<void>('Debug: resetting card play limit', 'DebugResetDevelopmentCardPlayLimit', roomCode, targetPlayerId),
    debugGetDevelopmentDeckComposition: (roomCode: string) => invoke<DevelopmentDeckComposition>('Debug: reading development deck', 'DebugGetDevelopmentDeckComposition', roomCode),
    debugRestartMatch: (roomCode: string) => invoke<void>('Debug: restarting match', 'DebugRestartMatch', roomCode),
    debugRegenerateBoard: (roomCode: string, boardSeed: number) => invoke<void>('Debug: regenerating board', 'DebugRegenerateBoard', roomCode, boardSeed),
    debugValidateBoard: (roomCode: string) => invoke<BoardIntegrityResult>('Debug: validating board', 'DebugValidateBoard', roomCode)
  };
}
