import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { DevelopmentCardType, DevelopmentDeckComposition, ResourceType, SetupStep } from '../types/game';
import type { GameStartedEvent, JoinRoomResult, Room } from '../types/lobby';
import { getFriendlyGameError, makeFriendlyGameError, type FriendlyGameError } from '../utils/gameErrors';

export type ConnectionPhase = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? import.meta.env.VITE_API_URL ?? 'http://localhost:5193')
  .replace(/\/$/, '');
const signalRHubUrl = import.meta.env.VITE_SIGNALR_HUB_URL ?? `${apiBaseUrl}/hubs/lobby`;

export function useLobbyConnection() {
  const connectionRef = useRef<HubConnection | null>(null);
  const [connectionPhase, setConnectionPhase] = useState<ConnectionPhase>('connecting');
  const [playerId, setPlayerId] = useState<string | null>(null);
  const [room, setRoom] = useState<Room | null>(null);
  const [gameStarted, setGameStarted] = useState<GameStartedEvent | null>(null);
  const [pendingAction, setPendingAction] = useState<string | null>(null);
  const [error, setError] = useState<FriendlyGameError | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(signalRHubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('RoomUpdated', (nextRoom: Room) => {
      setRoom(nextRoom);
    });

    connection.on('GameStarted', (event: GameStartedEvent) => {
      setGameStarted(event);
    });

    connection.on('GameUpdated', (event: GameStartedEvent) => {
      setGameStarted(event);
    });

    connection.onreconnecting(() => setConnectionPhase('reconnecting'));
    connection.onreconnected(() => {
      setConnectionPhase('connected');
      setError(null);
    });
    connection.onclose(() => setConnectionPhase('disconnected'));

    connection
      .start()
      .then(() => {
        setConnectionPhase('connected');
        setError(null);
      })
      .catch(() => {
        setConnectionPhase('disconnected');
        setError(makeFriendlyGameError('Connection unavailable', 'Could not connect to the Karo server.'));
      });

    return () => {
      connection.stop().catch(() => undefined);
    };
  }, []);

  const clearError = useCallback(() => setError(null), []);

  const invoke = useCallback(
    async <T,>(actionName: string, methodName: string, ...args: unknown[]): Promise<T> => {
      const connection = connectionRef.current;

      if (!connection || connection.state !== HubConnectionState.Connected) {
        const connectionError = makeFriendlyGameError('Connection unavailable', 'The Karo server is not connected yet.');
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

  const createRoom = useCallback(
    async (playerName: string) => {
      const result = await invoke<JoinRoomResult>('Creating room', 'CreateRoom', playerName);
      setPlayerId(result.playerId);
      setRoom(result.room);
      setGameStarted(null);
    },
    [invoke]
  );

  const joinRoom = useCallback(
    async (roomCode: string, playerName: string) => {
      const result = await invoke<JoinRoomResult>('Joining room', 'JoinRoom', roomCode, playerName);
      setPlayerId(result.playerId);
      setRoom(result.room);
      setGameStarted(null);
    },
    [invoke]
  );

  const startGame = useCallback(
    async (roomCode: string) => {
      await invoke<void>('Starting game', 'StartGame', roomCode);
    },
    [invoke]
  );

  return {
    connectionPhase,
    playerId,
    room,
    gameStarted,
    pendingAction,
    error,
    clearError,
    createRoom,
    joinRoom,
    startGame,
    buyDevelopmentCard: (roomCode: string) =>
      invoke<void>('Buying development card', 'BuyDevelopmentCard', roomCode),
    tradeWithBank: (roomCode: string, offeredResource: ResourceType, requestedResource: ResourceType) =>
      invoke<void>('Trading with harbor', 'MaritimeTrade', roomCode, offeredResource, requestedResource),
    endTurn: (roomCode: string) => invoke<void>('Ending turn', 'EndTurn', roomCode),
    placeSetupCamp: (roomCode: string, vertexId: string) =>
      invoke<void>('Placing setup camp', 'PlaceSetupCamp', roomCode, vertexId),
    placeSetupTrail: (roomCode: string, edgeId: string) =>
      invoke<void>('Placing setup trail', 'PlaceSetupTrail', roomCode, edgeId),
    rollDice: (roomCode: string) => invoke<void>('Rolling dice', 'RollDice', roomCode),
    discardForWarden: (roomCode: string, discardedResources: Partial<Record<ResourceType, number>>) =>
      invoke<void>('Discarding for Warden', 'DiscardForWarden', roomCode, discardedResources),
    moveWarden: (roomCode: string, targetTileId: string) =>
      invoke<void>('Moving Warden', 'MoveWarden', roomCode, targetTileId),
    stealFromWardenVictim: (roomCode: string, victimPlayerId: string) =>
      invoke<void>('Stealing with Warden', 'StealFromWardenVictim', roomCode, victimPlayerId),
    playYearOfPlenty: (roomCode: string, cardId: string, selectedResources: ResourceType[]) =>
      invoke<void>('Playing Year of Plenty', 'PlayYearOfPlenty', roomCode, cardId, selectedResources),
    playMonopoly: (roomCode: string, cardId: string, selectedResource: ResourceType) =>
      invoke<void>('Playing Monopoly', 'PlayMonopoly', roomCode, cardId, selectedResource),
    playKnight: (roomCode: string, cardId: string, targetTileId: string, victimPlayerId: string | null) =>
      invoke<void>('Playing Knight', 'PlayKnight', roomCode, cardId, targetTileId, victimPlayerId),
    startRoadBuilding: (roomCode: string, cardId: string) =>
      invoke<void>('Playing Road Building', 'StartRoadBuilding', roomCode, cardId),
    cancelActiveDevelopmentCard: (roomCode: string) =>
      invoke<void>('Canceling development effect', 'CancelActiveDevelopmentCard', roomCode),
    debugAddResource: (roomCode: string, targetPlayerId: string, resource: ResourceType, amount: number) =>
      invoke<void>('Debug: adding resource', 'DebugAddResource', roomCode, targetPlayerId, resource, amount),
    debugSetTestingResources: (roomCode: string, targetPlayerId: string) =>
      invoke<void>('Debug: setting testing resources', 'DebugSetTestingResources', roomCode, targetPlayerId),
    debugClearResources: (roomCode: string, targetPlayerId: string) =>
      invoke<void>('Debug: clearing resources', 'DebugClearResources', roomCode, targetPlayerId),
    debugSetCurrentPlayer: (roomCode: string, targetPlayerId: string) =>
      invoke<void>('Debug: forcing turn', 'DebugSetCurrentPlayer', roomCode, targetPlayerId),
    debugForceDiceRoll: (roomCode: string, diceValue: number) =>
      invoke<void>('Debug: forcing dice', 'DebugForceDiceRoll', roomCode, diceValue),
    debugResetRollState: (roomCode: string) =>
      invoke<void>('Debug: resetting roll', 'DebugResetRollState', roomCode),
    debugMoveWarden: (roomCode: string, targetTileId: string) =>
      invoke<void>('Debug: moving Warden', 'DebugMoveWarden', roomCode, targetTileId),
    debugClearWardenState: (roomCode: string) =>
      invoke<void>('Debug: clearing Warden state', 'DebugClearWardenState', roomCode),
    debugSkipSetup: (roomCode: string) =>
      invoke<void>('Debug: skipping setup', 'DebugSkipSetup', roomCode),
    debugForceSetupStep: (roomCode: string, targetPlayerId: string, setupStep: SetupStep) =>
      invoke<void>('Debug: forcing setup step', 'DebugForceSetupStep', roomCode, targetPlayerId, setupStep),
    debugSetVictoryPoints: (roomCode: string, targetPlayerId: string, points: number) =>
      invoke<void>('Debug: setting victory points', 'DebugSetVictoryPoints', roomCode, targetPlayerId, points),
    debugTriggerWinCheck: (roomCode: string, targetPlayerId: string) =>
      invoke<void>('Debug: checking win', 'DebugTriggerWinCheck', roomCode, targetPlayerId),
    debugGiveDevelopmentCard: (roomCode: string, targetPlayerId: string, cardType: DevelopmentCardType | 'Random') =>
      invoke<void>('Debug: giving development card', 'DebugGiveDevelopmentCard', roomCode, targetPlayerId, cardType),
    debugClearDevelopmentCards: (roomCode: string, targetPlayerId: string) =>
      invoke<void>('Debug: clearing development cards', 'DebugClearDevelopmentCards', roomCode, targetPlayerId),
    debugResetDevelopmentCardPlayLimit: (roomCode: string, targetPlayerId: string) =>
      invoke<void>('Debug: resetting card play limit', 'DebugResetDevelopmentCardPlayLimit', roomCode, targetPlayerId),
    debugGetDevelopmentDeckComposition: (roomCode: string) =>
      invoke<DevelopmentDeckComposition>('Debug: reading development deck', 'DebugGetDevelopmentDeckComposition', roomCode),
    debugRestartMatch: (roomCode: string) =>
      invoke<void>('Debug: restarting match', 'DebugRestartMatch', roomCode)
  };
}
