import { useEffect, useMemo, useState } from 'react';
import type { ConnectionPhase } from '../hooks/useLobbyConnection';
import { defaultBoardDebugOptions } from '../types/debug';
import type { BoardDebugOptions } from '../types/debug';
import type { BoardIntegrityResult, DevelopmentCardType, DevelopmentDeckComposition, GameState, ResourceType, SetupStep } from '../types/game';
import type { DirectBuildSelection } from '../utils/directBuild';
import { getDirectBuildAvailability, isDirectBuildSelectionAvailable } from '../utils/directBuild';
import { useBoardRendererMode } from '../hooks/useBoardRendererMode';
import { BoardRendererSwitch } from './BoardRendererSwitch';
import { DebugModePanel } from './DebugModePanel';
import { BottomActionTray, TurnStatusPanel, WardenPanel } from './DevelopmentCardsPanel';
import type { UtilityDrawerId } from './DevelopmentCardsPanel';
import { GameHeader } from './GameHeader';
import { PlayerPanel } from './PlayerPanel';

interface GamePageProps {
  connectionPhase: ConnectionPhase;
  game: GameState;
  playerId: string | null;
  pendingAction: string | null;
  onBuyDevelopmentCard: (roomCode: string) => Promise<void>;
  onTradeWithBank: (roomCode: string, offeredResource: ResourceType, requestedResource: ResourceType) => Promise<void>;
  onCreateTradeOffer: (
    roomCode: string,
    targetPlayerId: string,
    offeredResources: Partial<Record<ResourceType, number>>,
    requestedResources: Partial<Record<ResourceType, number>>
  ) => Promise<void>;
  onAcceptTradeOffer: (roomCode: string, tradeOfferId: string) => Promise<void>;
  onRejectTradeOffer: (roomCode: string, tradeOfferId: string) => Promise<void>;
  onCancelTradeOffer: (roomCode: string, tradeOfferId: string) => Promise<void>;
  onEndTurn: (roomCode: string) => Promise<void>;
  onPlaceSetupCamp: (roomCode: string, vertexId: string) => Promise<void>;
  onPlaceSetupTrail: (roomCode: string, edgeId: string) => Promise<void>;
  onPlaceFreeTrail: (roomCode: string, edgeId: string) => Promise<void>;
  onBuildTrail: (roomCode: string, edgeId: string) => Promise<void>;
  onBuildCamp: (roomCode: string, vertexId: string) => Promise<void>;
  onBuildStronghold: (roomCode: string, vertexId: string) => Promise<void>;
  onRollDice: (roomCode: string) => Promise<void>;
  onDiscardForWarden: (roomCode: string, discardedResources: Partial<Record<ResourceType, number>>) => Promise<void>;
  onMoveWarden: (roomCode: string, targetTileId: string) => Promise<void>;
  onStealFromWardenVictim: (roomCode: string, victimPlayerId: string) => Promise<void>;
  onPlayYearOfPlenty: (roomCode: string, cardId: string, selectedResources: ResourceType[]) => Promise<void>;
  onPlayMonopoly: (roomCode: string, cardId: string, selectedResource: ResourceType) => Promise<void>;
  onPlayKnight: (roomCode: string, cardId: string, targetTileId: string, victimPlayerId: string | null) => Promise<void>;
  onStartRoadBuilding: (roomCode: string, cardId: string) => Promise<void>;
  onCancelActiveDevelopmentCard: (roomCode: string) => Promise<void>;
  onForfeitMatch: (roomCode: string) => Promise<void>;
  onReturnRoomToLobby: (roomCode: string) => Promise<void>;
  onContinueWithoutPlayer: (roomCode: string, playerId: string) => Promise<void>;
  onEndPausedMatch: (roomCode: string) => Promise<void>;
  isDebugEnabled: boolean;
  onDebugModeChange: (enabled: boolean) => void;
  onDebugAddResource: (roomCode: string, targetPlayerId: string, resource: ResourceType, amount: number) => Promise<void>;
  onDebugSetTestingResources: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugClearResources: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugSetCurrentPlayer: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugForceDiceRoll: (roomCode: string, diceValue: number) => Promise<void>;
  onDebugResetRollState: (roomCode: string) => Promise<void>;
  onDebugMoveWarden: (roomCode: string, targetTileId: string) => Promise<void>;
  onDebugClearWardenState: (roomCode: string) => Promise<void>;
  onDebugSkipSetup: (roomCode: string) => Promise<void>;
  onDebugForceSetupStep: (roomCode: string, targetPlayerId: string, setupStep: SetupStep) => Promise<void>;
  onDebugSetVictoryPoints: (roomCode: string, targetPlayerId: string, points: number) => Promise<void>;
  onDebugTriggerWinCheck: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugRecalculateLongestTrail: (roomCode: string) => Promise<void>;
  onDebugGiveDevelopmentCard: (roomCode: string, targetPlayerId: string, cardType: DevelopmentCardType | 'Random') => Promise<void>;
  onDebugClearDevelopmentCards: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugResetDevelopmentCardPlayLimit: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugGetDevelopmentDeckComposition: (roomCode: string) => Promise<DevelopmentDeckComposition>;
  onDebugRestartMatch: (roomCode: string) => Promise<void>;
  onDebugRegenerateBoard: (roomCode: string, boardSeed: number) => Promise<void>;
  onDebugValidateBoard: (roomCode: string) => Promise<BoardIntegrityResult>;
}

export function GamePage({
  connectionPhase,
  game,
  playerId,
  pendingAction,
  onBuyDevelopmentCard,
  onTradeWithBank,
  onCreateTradeOffer,
  onAcceptTradeOffer,
  onRejectTradeOffer,
  onCancelTradeOffer,
  onEndTurn,
  onPlaceSetupCamp,
  onPlaceSetupTrail,
  onPlaceFreeTrail,
  onBuildTrail,
  onBuildCamp,
  onBuildStronghold,
  onRollDice,
  onDiscardForWarden,
  onMoveWarden,
  onStealFromWardenVictim,
  onPlayYearOfPlenty,
  onPlayMonopoly,
  onPlayKnight,
  onStartRoadBuilding,
  onCancelActiveDevelopmentCard,
  onForfeitMatch,
  onReturnRoomToLobby,
  onContinueWithoutPlayer,
  onEndPausedMatch,
  isDebugEnabled,
  onDebugModeChange,
  onDebugAddResource,
  onDebugSetTestingResources,
  onDebugClearResources,
  onDebugSetCurrentPlayer,
  onDebugForceDiceRoll,
  onDebugResetRollState,
  onDebugMoveWarden,
  onDebugClearWardenState,
  onDebugSkipSetup,
  onDebugForceSetupStep,
  onDebugSetVictoryPoints,
  onDebugTriggerWinCheck,
  onDebugRecalculateLongestTrail,
  onDebugGiveDevelopmentCard,
  onDebugClearDevelopmentCards,
  onDebugResetDevelopmentCardPlayLimit,
  onDebugGetDevelopmentDeckComposition,
  onDebugRestartMatch,
  onDebugRegenerateBoard,
  onDebugValidateBoard
}: GamePageProps) {
  const [boardDebugOptions, setBoardDebugOptions] = useState<BoardDebugOptions>(defaultBoardDebugOptions);
  const [selectedBuildTarget, setSelectedBuildTarget] = useState<DirectBuildSelection | null>(null);
  const [directBuildHintDismissed, setDirectBuildHintDismissed] = useState(() => {
    try {
      return window.localStorage.getItem('karo.direct-build-hint.dismissed') === 'true';
    } catch {
      return false;
    }
  });
  const [activeDrawer, setActiveDrawer] = useState<UtilityDrawerId | null>(null);
  const { boardRendererMode, setBoardRendererMode } = useBoardRendererMode();
  const me = game.players.find((player) => player.playerId === playerId) ?? null;
  const showWardenPanel = game.pendingWardenAction !== 'None' && !!me;
  const pausePlayer = game.pause ? game.players.find((player) => player.playerId === game.pause?.disconnectedPlayerId) ?? null : null;
  const isHost = !!me?.isHost;
  const directBuildAvailability = useMemo(
    () => getDirectBuildAvailability(game, playerId, pendingAction),
    [game, pendingAction, playerId]
  );
  const showDirectBuildHint = !directBuildHintDismissed
    && !selectedBuildTarget
    && directBuildAvailability.hasAnyAction;

  useEffect(() => {
    if (game.pendingWardenAction !== 'None' || game.activeDevelopmentCardEffect) {
      setActiveDrawer(null);
      setSelectedBuildTarget(null);
    }
  }, [game.pendingWardenAction, game.activeDevelopmentCardEffect]);

  useEffect(() => {
    if (selectedBuildTarget && !isDirectBuildSelectionAvailable(directBuildAvailability, selectedBuildTarget)) {
      setSelectedBuildTarget(null);
    }
  }, [directBuildAvailability, selectedBuildTarget]);

  useEffect(() => {
    if (!selectedBuildTarget) {
      return;
    }

    const cancelOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setSelectedBuildTarget(null);
      }
    };
    const cancelOnOutsidePointer = (event: PointerEvent) => {
      const target = event.target instanceof Element ? event.target : null;
      if (!target?.closest('.direct-build-confirmation, [data-direct-build-target="true"]')) {
        setSelectedBuildTarget(null);
      }
    };

    window.addEventListener('keydown', cancelOnEscape);
    document.addEventListener('pointerdown', cancelOnOutsidePointer);
    return () => {
      window.removeEventListener('keydown', cancelOnEscape);
      document.removeEventListener('pointerdown', cancelOnOutsidePointer);
    };
  }, [selectedBuildTarget]);

  const selectBuildTarget = (selection: DirectBuildSelection) => {
    setActiveDrawer(null);
    setSelectedBuildTarget(selection);
  };

  const confirmDirectBuild = async () => {
    if (!selectedBuildTarget || !isDirectBuildSelectionAvailable(directBuildAvailability, selectedBuildTarget)) {
      setSelectedBuildTarget(null);
      return;
    }

    try {
      if (selectedBuildTarget.type === 'Trail') {
        await onBuildTrail(game.roomCode, selectedBuildTarget.targetId);
      } else if (selectedBuildTarget.type === 'Camp') {
        await onBuildCamp(game.roomCode, selectedBuildTarget.targetId);
      } else {
        await onBuildStronghold(game.roomCode, selectedBuildTarget.targetId);
      }
    } finally {
      setSelectedBuildTarget(null);
    }
  };

  const dismissDirectBuildHint = () => {
    setDirectBuildHintDismissed(true);
    try {
      window.localStorage.setItem('karo.direct-build-hint.dismissed', 'true');
    } catch {
      // Storage can be unavailable in privacy-restricted browser sessions.
    }
  };

  return (
    <section className="game-screen">
      {isDebugEnabled ? <div className="debug-mode-banner">Debug Mode Active</div> : null}
      <GameHeader connectionPhase={connectionPhase} game={game} onOpenMatchDetails={() => setActiveDrawer('details')} />
      <MatchLifecycleBar
        game={game}
        isHost={isHost}
        pausePlayerName={pausePlayer?.playerName ?? 'A player'}
        pendingAction={pendingAction}
        onForfeitMatch={onForfeitMatch}
        onReturnRoomToLobby={onReturnRoomToLobby}
        onContinueWithoutPlayer={onContinueWithoutPlayer}
        onEndPausedMatch={onEndPausedMatch}
      />

      {connectionPhase !== 'connected' ? (
        <div className="match-lifecycle-overlay" role="status">
          {connectionPhase === 'reconnecting' ? 'Connection lost. Reconnecting...' : 'Waiting for the Karo connection.'}
        </div>
      ) : null}

      <div className="match-table-layout">
        <aside className="match-left-rail" aria-label="Players and match summary">
          <PlayerPanel game={game} playerId={playerId} />
        </aside>

        <main className="match-board-zone">
          <BoardRendererSwitch
            rendererMode={boardRendererMode}
            game={game}
            playerId={playerId}
            selectedBuildTarget={selectedBuildTarget}
            debugOptions={isDebugEnabled ? boardDebugOptions : undefined}
            pendingAction={pendingAction}
            onRendererModeChange={setBoardRendererMode}
            onSelectBuildTarget={selectBuildTarget}
            onPlaceFreeTrail={onPlaceFreeTrail}
            onPlaceSetupCamp={onPlaceSetupCamp}
            onPlaceSetupTrail={onPlaceSetupTrail}
            onMoveWarden={onMoveWarden}
          />

          <BottomActionTray
            activeDrawer={activeDrawer}
            game={game}
            playerId={playerId}
            pendingAction={pendingAction}
            onDrawerChange={setActiveDrawer}
            onBuyDevelopmentCard={onBuyDevelopmentCard}
            onTradeWithBank={onTradeWithBank}
            onCreateTradeOffer={onCreateTradeOffer}
            onAcceptTradeOffer={onAcceptTradeOffer}
            onRejectTradeOffer={onRejectTradeOffer}
            onCancelTradeOffer={onCancelTradeOffer}
            onPlayYearOfPlenty={onPlayYearOfPlenty}
            onPlayMonopoly={onPlayMonopoly}
            onPlayKnight={onPlayKnight}
            onStartRoadBuilding={onStartRoadBuilding}
          />
        </main>

        <aside className="match-right-rail" aria-label="Turn status">
          {showWardenPanel ? (
            <WardenPanel
              game={game}
              me={me}
              pendingAction={pendingAction}
              playerId={playerId}
              onDiscardForWarden={onDiscardForWarden}
              onStealFromWardenVictim={onStealFromWardenVictim}
            />
          ) : (
            <TurnStatusPanel
              game={game}
              playerId={playerId}
              pendingAction={pendingAction}
              directBuildSelection={selectedBuildTarget}
              showDirectBuildHint={showDirectBuildHint}
              onCancelDirectBuild={() => setSelectedBuildTarget(null)}
              onConfirmDirectBuild={() => void confirmDirectBuild()}
              onDismissDirectBuildHint={dismissDirectBuildHint}
              onEndTurn={onEndTurn}
              onRollDice={onRollDice}
              onCancelActiveDevelopmentCard={onCancelActiveDevelopmentCard}
            />
          )}
        </aside>
      </div>

      {isDebugEnabled ? (
        <DebugModePanel
          boardOptions={boardDebugOptions}
          boardRendererMode={boardRendererMode}
          game={game}
          pendingAction={pendingAction}
          playerId={playerId}
          onBoardOptionsChange={setBoardDebugOptions}
          onBoardRendererModeChange={setBoardRendererMode}
          onDebugAddResource={onDebugAddResource}
          onDebugClearDevelopmentCards={onDebugClearDevelopmentCards}
          onDebugClearResources={onDebugClearResources}
          onDebugForceDiceRoll={onDebugForceDiceRoll}
          onDebugMoveWarden={onDebugMoveWarden}
          onDebugClearWardenState={onDebugClearWardenState}
          onDebugGiveDevelopmentCard={onDebugGiveDevelopmentCard}
          onDebugGetDevelopmentDeckComposition={onDebugGetDevelopmentDeckComposition}
          onDebugResetDevelopmentCardPlayLimit={onDebugResetDevelopmentCardPlayLimit}
          onDebugResetRollState={onDebugResetRollState}
          onDebugRestartMatch={onDebugRestartMatch}
          onDebugRegenerateBoard={onDebugRegenerateBoard}
          onDebugSkipSetup={onDebugSkipSetup}
          onDebugForceSetupStep={onDebugForceSetupStep}
          onDebugSetCurrentPlayer={onDebugSetCurrentPlayer}
          onDebugSetTestingResources={onDebugSetTestingResources}
          onDebugSetVictoryPoints={onDebugSetVictoryPoints}
          onDebugTriggerWinCheck={onDebugTriggerWinCheck}
          onDebugRecalculateLongestTrail={onDebugRecalculateLongestTrail}
          onDebugValidateBoard={onDebugValidateBoard}
          onDisableDebugMode={() => onDebugModeChange(false)}
          onEndTurn={onEndTurn}
        />
      ) : null}
    </section>
  );
}

function MatchLifecycleBar({
  game,
  isHost,
  pausePlayerName,
  pendingAction,
  onForfeitMatch,
  onReturnRoomToLobby,
  onContinueWithoutPlayer,
  onEndPausedMatch
}: {
  game: GameState;
  isHost: boolean;
  pausePlayerName: string;
  pendingAction: string | null;
  onForfeitMatch: (roomCode: string) => Promise<void>;
  onReturnRoomToLobby: (roomCode: string) => Promise<void>;
  onContinueWithoutPlayer: (roomCode: string, playerId: string) => Promise<void>;
  onEndPausedMatch: (roomCode: string) => Promise<void>;
}) {
  if (game.status === 'Finished') {
    return isHost ? (
      <div className="match-lifecycle-bar postgame-lifecycle">
        <span>Match complete. Return the table to the lobby for a rematch.</span>
        <button className="secondary-button" disabled={!!pendingAction} type="button" onClick={() => void onReturnRoomToLobby(game.roomCode)}>
          Return to Lobby
        </button>
      </div>
    ) : null;
  }

  if (game.pause?.isPaused) {
    const reconnectDeadline = new Date(game.pause.reconnectDeadline);
    const deadline = reconnectDeadline.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    const canResolveTimeout = reconnectDeadline.getTime() <= Date.now();
    return (
      <div className="match-lifecycle-bar paused-lifecycle" role="status">
        <span>Waiting for {pausePlayerName} to reconnect. Grace window ends at {deadline}.</span>
        {isHost && canResolveTimeout ? (
          <div>
            <button className="secondary-button" disabled={!!pendingAction} type="button" onClick={() => void onContinueWithoutPlayer(game.roomCode, game.pause!.disconnectedPlayerId)}>
              Continue Without Player
            </button>
            <button className="text-button" disabled={!!pendingAction} type="button" onClick={() => void onEndPausedMatch(game.roomCode)}>
              End Match
            </button>
          </div>
        ) : isHost ? <span className="match-lifecycle-hint">Host controls unlock if the reconnect window expires.</span> : null}
      </div>
    );
  }

  return (
    <div className="match-lifecycle-bar quiet-lifecycle">
      <button className="text-button" disabled={!!pendingAction} type="button" onClick={() => {
        if (window.confirm('Leaving now will forfeit the match. Continue?')) {
          void onForfeitMatch(game.roomCode);
        }
      }}>
        Forfeit Match
      </button>
    </div>
  );
}
