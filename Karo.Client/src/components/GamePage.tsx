import { useState } from 'react';
import type { ConnectionPhase } from '../hooks/useLobbyConnection';
import { defaultBoardDebugOptions } from '../types/debug';
import type { BoardDebugOptions } from '../types/debug';
import type { DevelopmentCardType, DevelopmentDeckComposition, GameState, ResourceType, SetupStep } from '../types/game';
import { useBoardRendererMode } from '../hooks/useBoardRendererMode';
import { BoardRendererSwitch } from './BoardRendererSwitch';
import { DebugModePanel } from './DebugModePanel';
import { BottomActionTray, TurnStatusPanel, WardenPanel } from './DevelopmentCardsPanel';
import { GameHeader } from './GameHeader';
import { PlayerPanel } from './PlayerPanel';

interface GamePageProps {
  connectionPhase: ConnectionPhase;
  game: GameState;
  playerId: string | null;
  pendingAction: string | null;
  onBuyDevelopmentCard: (roomCode: string) => Promise<void>;
  onTradeWithBank: (roomCode: string, offeredResource: ResourceType, requestedResource: ResourceType) => Promise<void>;
  onEndTurn: (roomCode: string) => Promise<void>;
  onPlaceSetupCamp: (roomCode: string, vertexId: string) => Promise<void>;
  onPlaceSetupTrail: (roomCode: string, edgeId: string) => Promise<void>;
  onRollDice: (roomCode: string) => Promise<void>;
  onDiscardForWarden: (roomCode: string, discardedResources: Partial<Record<ResourceType, number>>) => Promise<void>;
  onMoveWarden: (roomCode: string, targetTileId: string) => Promise<void>;
  onStealFromWardenVictim: (roomCode: string, victimPlayerId: string) => Promise<void>;
  onPlayYearOfPlenty: (roomCode: string, cardId: string, selectedResources: ResourceType[]) => Promise<void>;
  onPlayMonopoly: (roomCode: string, cardId: string, selectedResource: ResourceType) => Promise<void>;
  onPlayKnight: (roomCode: string, cardId: string, targetTileId: string, victimPlayerId: string | null) => Promise<void>;
  onStartRoadBuilding: (roomCode: string, cardId: string) => Promise<void>;
  onCancelActiveDevelopmentCard: (roomCode: string) => Promise<void>;
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
  onDebugGiveDevelopmentCard: (roomCode: string, targetPlayerId: string, cardType: DevelopmentCardType | 'Random') => Promise<void>;
  onDebugClearDevelopmentCards: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugResetDevelopmentCardPlayLimit: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugGetDevelopmentDeckComposition: (roomCode: string) => Promise<DevelopmentDeckComposition>;
  onDebugRestartMatch: (roomCode: string) => Promise<void>;
}

export function GamePage({
  connectionPhase,
  game,
  playerId,
  pendingAction,
  onBuyDevelopmentCard,
  onTradeWithBank,
  onEndTurn,
  onPlaceSetupCamp,
  onPlaceSetupTrail,
  onRollDice,
  onDiscardForWarden,
  onMoveWarden,
  onStealFromWardenVictim,
  onPlayYearOfPlenty,
  onPlayMonopoly,
  onPlayKnight,
  onStartRoadBuilding,
  onCancelActiveDevelopmentCard,
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
  onDebugGiveDevelopmentCard,
  onDebugClearDevelopmentCards,
  onDebugResetDevelopmentCardPlayLimit,
  onDebugGetDevelopmentDeckComposition,
  onDebugRestartMatch
}: GamePageProps) {
  const [boardDebugOptions, setBoardDebugOptions] = useState<BoardDebugOptions>(defaultBoardDebugOptions);
  const { boardRendererMode, setBoardRendererMode } = useBoardRendererMode();
  const me = game.players.find((player) => player.playerId === playerId) ?? null;
  const showWardenPanel = game.pendingWardenAction !== 'None' && !!me;

  return (
    <section className="game-screen">
      {isDebugEnabled ? <div className="debug-mode-banner">Debug Mode Active</div> : null}
      <GameHeader connectionPhase={connectionPhase} game={game} />
      {game.phase === 'Setup' ? <SetupBanner game={game} playerId={playerId} /> : null}

      <div className="match-table-layout">
        <aside className="match-left-rail" aria-label="Players and match summary">
          <PlayerPanel game={game} playerId={playerId} />
        </aside>

        <main className="match-board-zone">
          <BoardRendererSwitch
            rendererMode={boardRendererMode}
            game={game}
            playerId={playerId}
            debugOptions={isDebugEnabled ? boardDebugOptions : undefined}
            pendingAction={pendingAction}
            onRendererModeChange={setBoardRendererMode}
            onPlaceSetupCamp={onPlaceSetupCamp}
            onPlaceSetupTrail={onPlaceSetupTrail}
            onMoveWarden={onMoveWarden}
          />

          <BottomActionTray
            game={game}
            playerId={playerId}
            pendingAction={pendingAction}
            onBuyDevelopmentCard={onBuyDevelopmentCard}
            onTradeWithBank={onTradeWithBank}
            onPlayYearOfPlenty={onPlayYearOfPlenty}
            onPlayMonopoly={onPlayMonopoly}
            onPlayKnight={onPlayKnight}
            onStartRoadBuilding={onStartRoadBuilding}
          />
        </main>

        <aside className="match-right-rail" aria-label="Turn status">
          <TurnStatusPanel
            game={game}
            playerId={playerId}
            pendingAction={pendingAction}
            onEndTurn={onEndTurn}
            onRollDice={onRollDice}
            onCancelActiveDevelopmentCard={onCancelActiveDevelopmentCard}
          />

          {showWardenPanel ? (
            <WardenPanel
              game={game}
              me={me}
              pendingAction={pendingAction}
              playerId={playerId}
              onDiscardForWarden={onDiscardForWarden}
              onStealFromWardenVictim={onStealFromWardenVictim}
            />
          ) : null}
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
          onDebugSkipSetup={onDebugSkipSetup}
          onDebugForceSetupStep={onDebugForceSetupStep}
          onDebugSetCurrentPlayer={onDebugSetCurrentPlayer}
          onDebugSetTestingResources={onDebugSetTestingResources}
          onDebugSetVictoryPoints={onDebugSetVictoryPoints}
          onDebugTriggerWinCheck={onDebugTriggerWinCheck}
          onDisableDebugMode={() => onDebugModeChange(false)}
          onEndTurn={onEndTurn}
        />
      ) : null}
    </section>
  );
}

function SetupBanner({ game, playerId }: { game: GameState; playerId: string | null }) {
  const currentSetupPlayer = game.players.find((player) => player.playerId === game.currentSetupPlayerId);
  const isMySetupTurn = playerId === game.currentSetupPlayerId;
  const order = game.playerOrder
    .map((orderedPlayerId) => game.players.find((player) => player.playerId === orderedPlayerId)?.playerName)
    .filter(Boolean)
    .join(' -> ');
  const roundLabel = game.setupRound === 'SecondPlacement' ? 'Second placement' : 'First placement';
  const stepLabel = game.setupStep === 'PlaceTrail' ? 'Place a connected Trail' : 'Place a Camp';

  return (
    <section className="setup-banner">
      <div>
        <p className="eyebrow">Setup Phase</p>
        <h2>{isMySetupTurn ? stepLabel : `${currentSetupPlayer?.playerName ?? 'Player'} is setting up`}</h2>
        <p>
          {roundLabel} - {game.setupDirection === 'Reverse' ? 'reverse order' : 'forward order'} - {order}
        </p>
      </div>
      <span className="setup-step-pill">{stepLabel}</span>
    </section>
  );
}
