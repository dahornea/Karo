import { Anchor, Boxes, Check, Clock3, Hand, Info, LockKeyhole, ScrollText, Shield, Sparkles, Star, Trophy, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { DevelopmentCardType, GameState, PlayerDevelopmentCard, PlayerGameState, ResourceType } from '../types/game';
import { developmentCardCost, resources } from '../types/game';
import type { CardAvailability } from '../utils/actionAvailability';
import {
  formatMissingDevelopmentCardResources,
  getDevelopmentCardAvailability
} from '../utils/actionAvailability';
import type { CommandDockActionId } from '../utils/commandDock';
import { getVisibleCommandDockActions } from '../utils/commandDock';
import { getTurnPanelContext } from '../utils/sidePanels';
import { BankTradePanel } from './BankTradePanel';
import type { ActionAssetType } from '../assets/game/gameAssets';
import { ActionIcon, DevelopmentCardArtwork, PieceAsset, ResourceCost, ResourceIcon, ResourceStripItem } from './GameAsset';
import { ContextStateIcon, ContextStatusIcon, IconActionButton, PieceCount } from './MatchIconUI';
import type { DirectBuildSelection, DirectBuildType } from '../utils/directBuild';
import { directBuildCosts, directBuildLabels } from '../utils/directBuild';

interface TurnStatusPanelProps {
  game: GameState;
  playerId: string | null;
  pendingAction: string | null;
  directBuildSelection: DirectBuildSelection | null;
  showDirectBuildHint: boolean;
  onCancelDirectBuild: () => void;
  onConfirmDirectBuild: () => void;
  onDismissDirectBuildHint: () => void;
  onEndTurn: (roomCode: string) => Promise<void>;
  onRollDice: (roomCode: string) => Promise<void>;
  onCancelActiveDevelopmentCard: (roomCode: string) => Promise<void>;
}

export type UtilityDrawerId = 'trade' | 'cards' | 'log' | 'details';

interface BottomActionTrayProps {
  game: GameState;
  playerId: string | null;
  activeDrawer: UtilityDrawerId | null;
  pendingAction: string | null;
  onDrawerChange: (drawer: UtilityDrawerId | null) => void;
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
  onPlayYearOfPlenty: (roomCode: string, cardId: string, selectedResources: ResourceType[]) => Promise<void>;
  onPlayMonopoly: (roomCode: string, cardId: string, selectedResource: ResourceType) => Promise<void>;
  onPlayKnight: (roomCode: string, cardId: string, targetTileId: string, victimPlayerId: string | null) => Promise<void>;
  onStartRoadBuilding: (roomCode: string, cardId: string) => Promise<void>;
}

interface DevelopmentCardsPanelProps {
  game: GameState;
  playerId: string | null;
  pendingAction: string | null;
  actionsUnlocked: boolean;
  developmentLockReason: string | null;
  onBuyDevelopmentCard: (roomCode: string) => Promise<void>;
  onPlayYearOfPlenty: (roomCode: string, cardId: string, selectedResources: ResourceType[]) => Promise<void>;
  onPlayMonopoly: (roomCode: string, cardId: string, selectedResource: ResourceType) => Promise<void>;
  onPlayKnight: (roomCode: string, cardId: string, targetTileId: string, victimPlayerId: string | null) => Promise<void>;
  onStartRoadBuilding: (roomCode: string, cardId: string) => Promise<void>;
}

const cardText: Record<DevelopmentCardType, { title: string; description: string }> = {
  Knight: {
    title: 'Knight',
    description: 'Move the Warden and steal 1 random supply when possible.'
  },
  RoadBuilding: {
    title: 'Road Building',
    description: 'Prepare up to 2 free Trails once trail placement exists.'
  },
  YearOfPlenty: {
    title: 'Year of Plenty',
    description: 'Take any 2 supplies. You may choose the same supply twice.'
  },
  Monopoly: {
    title: 'Monopoly',
    description: 'Name a supply and collect all of it from opponents.'
  },
  VictoryPoint: {
    title: 'Victory Point',
    description: 'Hidden private point. Reveals when scoring the win.'
  }
};

const utilityActions: Array<{ id: CommandDockActionId; label: string; asset: ActionAssetType }> = [
  { id: 'trade', label: 'Trade', asset: 'Trade' },
  { id: 'cards', label: 'Cards', asset: 'Cards' },
  { id: 'log', label: 'Game Log', asset: 'GameLog' }
];

export function TurnStatusPanel({
  game,
  playerId,
  pendingAction,
  directBuildSelection,
  showDirectBuildHint,
  onCancelDirectBuild,
  onConfirmDirectBuild,
  onDismissDirectBuildHint,
  onEndTurn,
  onRollDice,
  onCancelActiveDevelopmentCard
}: TurnStatusPanelProps) {
  const { actionsUnlocked, currentTurnPlayer, isMyTurn, me, wardenFlowActive } = getTurnContext(game, playerId);
  const winner = game.players.find((player) => player.playerId === game.winnerPlayerId);
  const isSetup = game.phase === 'Setup';
  const isFinished = game.status === 'Finished';
  const setupPiece = game.setupStep === 'PlaceTrail' ? 'Trail' : 'Camp';
  const setupRound = game.setupRound === 'SecondPlacement' ? 'Round 2' : 'Round 1';
  const context = getTurnPanelContext({
    currentTurnPlayerName: currentTurnPlayer?.playerName ?? 'Player',
    game,
    isFinished,
    isMyTurn,
    setupPiece,
    setupRound,
    wardenFlowActive,
    winnerName: winner?.playerName ?? 'Player'
  });

  if (!me) {
    return null;
  }

  if (directBuildSelection) {
    return (
      <DirectBuildConfirmation
        me={me}
        pendingAction={pendingAction}
        selection={directBuildSelection}
        onCancel={onCancelDirectBuild}
        onConfirm={onConfirmDirectBuild}
      />
    );
  }

  return (
    <section className="game-side-card turn-card right-status-card context-action-panel">
      <span className="context-panel-label">{context.phaseLabel}</span>
      <ContextStateIcon kind={context.visual} value={context.visual === 'after-roll' ? null : game.lastDiceRoll} />
      <div className="turn-focus">
        <strong>{context.primaryInstruction}</strong>
        <p>{context.helperText}</p>
      </div>
      {context.details.length > 0 ? (
        <div className="context-status-chips" aria-label="Current action details">
          {context.details.map((detail) => (
            <ContextStatusIcon key={detail.label} label={detail.label} type={getContextDetailType(detail.label)} value={detail.value} />
          ))}
        </div>
      ) : null}
      {!isSetup && !isFinished && !game.activeDevelopmentCardEffect && context.secondaryText ? (
        <p className="context-help-note">{context.secondaryText}</p>
      ) : null}
      {game.activeDevelopmentCardEffect ? (
        <div className="context-effect-row">
          <Sparkles size={15} />
          <div>
            <strong>Road Building</strong>
            <span>
              {game.activeDevelopmentCardEffect.freeTrailsPlaced}/{game.activeDevelopmentCardEffect.maxFreeTrails} free Trails placed
            </span>
          </div>
          <button
            type="button"
            disabled={!isMyTurn || !!pendingAction}
            onClick={() => void onCancelActiveDevelopmentCard(game.roomCode)}
          >
            Cancel
          </button>
        </div>
      ) : null}
      {!isFinished && game.phase === 'NormalTurn' && !game.hasRolledThisTurn && !game.activeDevelopmentCardEffect ? (
        <IconActionButton
          asset="RollDice"
          className="primary-button full-action"
          type="button"
          disabled={!isMyTurn || wardenFlowActive || !!game.activeDevelopmentCardEffect || !!pendingAction}
          label="Roll Dice"
          title={game.activeDevelopmentCardEffect ? 'Resolve the current Development Card action first.' : undefined}
          onClick={() => void onRollDice(game.roomCode)}
        />
      ) : null}
      {!isFinished && game.phase === 'NormalTurn' && game.hasRolledThisTurn && !game.activeDevelopmentCardEffect ? (
        <IconActionButton asset="EndTurn" className="secondary-button full-action" type="button" disabled={!actionsUnlocked || !!pendingAction} label="End Turn" onClick={() => void onEndTurn(game.roomCode)} />
      ) : null}
      {showDirectBuildHint ? (
        <div className="direct-build-hint" role="status">
          <div className="direct-build-hint-pieces" aria-hidden="true">
            <PieceAsset decorative type="Trail" />
            <PieceAsset decorative type="Camp" />
            <PieceAsset decorative type="Stronghold" />
          </div>
          <p><strong>Build on the island</strong><span>Select a highlighted edge, intersection, or one of your Camps.</span></p>
          <button aria-label="Dismiss direct building hint" type="button" onClick={onDismissDirectBuildHint}>
            <ActionIcon decorative size="xs" type="Close" />
          </button>
        </div>
      ) : null}
      {isFinished ? (
        <div className="final-score-list">
          {game.players
            .slice()
            .sort((left, right) => right.totalVictoryPoints - left.totalVictoryPoints)
            .map((player) => (
              <span key={player.playerId}>
                <strong>{player.playerName}</strong>
                <ContextStatusIcon label="Victory Points" type="score" value={String(player.totalVictoryPoints)} />
              </span>
            ))}
        </div>
      ) : null}
    </section>
  );
}

function DirectBuildConfirmation({
  me,
  pendingAction,
  selection,
  onCancel,
  onConfirm
}: {
  me: PlayerGameState;
  pendingAction: string | null;
  selection: DirectBuildSelection;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const pieceType: DirectBuildType = selection.type;
  const pieceSupply = getDirectBuildPieceSupply(me, pieceType);

  return (
    <section aria-live="polite" className="game-side-card turn-card right-status-card context-action-panel direct-build-confirmation">
      <span className="context-panel-label">Confirm construction</span>
      <div className="direct-build-piece-preview">
        <PieceAsset decorative type={pieceType} />
      </div>
      <div className="turn-focus">
        <span>Selected on the island</span>
        <strong>{directBuildLabels[pieceType].action}</strong>
        <p>{pieceType === 'Stronghold' ? 'Upgrade this Camp. The Camp piece returns to your supply.' : 'Spend the shown supplies and place this piece.'}</p>
      </div>
      <div className="direct-build-summary">
        <ResourceCost cost={directBuildCosts[pieceType]} />
        <PieceCount remaining={pieceSupply.remaining} total={pieceSupply.total} type={pieceType} />
      </div>
      <div className="direct-build-confirm-actions">
        <IconActionButton
          asset="Build"
          className="primary-button full-action"
          disabled={!!pendingAction}
          label={pendingAction ? 'Building...' : 'Confirm Build'}
          type="button"
          onClick={onConfirm}
        />
        <button className="secondary-button full-action" disabled={!!pendingAction} type="button" onClick={onCancel}>
          <X aria-hidden="true" size={17} />
          Cancel
        </button>
      </div>
    </section>
  );
}

function getDirectBuildPieceSupply(me: PlayerGameState, type: DirectBuildType) {
  if (type === 'Trail') return { remaining: me.remainingTrails, total: me.totalTrails };
  if (type === 'Camp') return { remaining: me.remainingCamps, total: me.totalCamps };
  return { remaining: me.remainingStrongholds, total: me.totalStrongholds };
}

function getContextDetailType(label: string): 'player' | 'dice' | 'piece' | 'progress' | 'score' {
  if (label === 'Active player' || label === 'Winner') return 'player';
  if (label === 'Dice') return 'dice';
  if (label === 'Step') return 'piece';
  if (label === 'Progress' || label === 'State') return 'progress';
  return 'score';
}

export function BottomActionTray({
  game,
  playerId,
  activeDrawer,
  pendingAction,
  onDrawerChange,
  onBuyDevelopmentCard,
  onTradeWithBank,
  onCreateTradeOffer,
  onAcceptTradeOffer,
  onRejectTradeOffer,
  onCancelTradeOffer,
  onPlayYearOfPlenty,
  onPlayMonopoly,
  onPlayKnight,
  onStartRoadBuilding
}: BottomActionTrayProps) {
  const { actionsUnlocked, developmentLockReason, isMyTurn, me } = getTurnContext(game, playerId);
  const visibleActionStates = useMemo(
    () => me ? getVisibleCommandDockActions(game, me, playerId, actionsUnlocked) : [],
    [actionsUnlocked, game, me, playerId]
  );
  const visibleActions = visibleActionStates
    .map((state) => {
      const action = utilityActions.find((utilityAction) => utilityAction.id === state.id);
      return action ? { ...action, disabledReason: state.disabledReason } : null;
    })
    .filter((action): action is { id: CommandDockActionId; label: string; asset: ActionAssetType; disabledReason: string | null } => !!action);
  const activeDrawerDetails = activeDrawer === 'details'
    ? { id: 'details' as const, label: 'Details', asset: 'MatchDetails' as const }
    : activeDrawer
      ? utilityActions.find((action) => action.id === activeDrawer) ?? null
      : null;

  useEffect(() => {
    if (!activeDrawer) {
      return;
    }

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onDrawerChange(null);
      }
    };

    window.addEventListener('keydown', closeOnEscape);
    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [activeDrawer, onDrawerChange]);

  useEffect(() => {
    if (!activeDrawer || activeDrawer === 'details') {
      return;
    }

    const activeState = visibleActionStates.find((action) => action.id === activeDrawer);
    if (!activeState || activeState.disabledReason) {
      onDrawerChange(null);
    }
  }, [activeDrawer, onDrawerChange, visibleActionStates]);

  if (!me) {
    return null;
  }

  return (
    <>
      <section className="bottom-action-tray utility-action-bar command-dock" aria-label="Player command dock" data-phase={game.phase.toLowerCase()}>
        <div className="dock-resources" aria-label="Your supplies">
          {resources.map((resource) => {
            const quantity = me.supplies[resource] ?? 0;

            return (
              <ResourceStripItem
                amount={quantity}
                className={`dock-resource dock-resource-${resource.toLowerCase()}`}
                key={resource}
                type={resource}
              />
            );
          })}
        </div>
        {game.phase === 'Setup' ? (
          <div className="setup-dock-progress" aria-label={`Setup progress: ${Math.min(me.campsBuilt, 2)} of 2 Camps and ${Math.min(me.trailsBuilt, 2)} of 2 Trails placed`}>
            <span className="setup-progress-label">Your setup</span>
            <span className="setup-progress-item">
              <PieceAsset decorative type="Camp" />
              <b>Camp {Math.min(me.campsBuilt, 2)}/2</b>
            </span>
            <span className="setup-progress-item">
              <PieceAsset decorative type="Trail" />
              <b>Trail {Math.min(me.trailsBuilt, 2)}/2</b>
            </span>
          </div>
        ) : null}
        <nav className="dock-actions" aria-label="Secondary game systems">
          {visibleActions.map((action) => {
            const isOpen = activeDrawer === action.id;
            const summary = getUtilitySummary(action.id, game, me);
            const disabledReason = action.disabledReason;
            const accessibleLabel = disabledReason ? `${action.label}: ${disabledReason}` : action.label;

            return (
              <button
                disabled={!!disabledReason}
                aria-disabled={!!disabledReason}
                aria-label={accessibleLabel}
                aria-expanded={isOpen}
                className="utility-action-button"
                data-available={!disabledReason}
                key={action.id}
                title={disabledReason ?? summary}
                type="button"
                onClick={() => {
                  if (disabledReason) {
                    return;
                  }

                  onDrawerChange(activeDrawer === action.id ? null : action.id);
                }}
              >
                <ActionIcon type={action.asset} />
                <strong>{action.label}</strong>
                {disabledReason ? <span className="sr-only">{disabledReason}</span> : null}
              </button>
            );
          })}
        </nav>
      </section>

      {activeDrawerDetails ? (
        <div className="utility-drawer-overlay" role="presentation">
          <button
            aria-label="Close utility panel"
            className="utility-drawer-backdrop"
            type="button"
            onClick={() => onDrawerChange(null)}
          />
          <section aria-label={`${activeDrawerDetails.label} panel`} aria-modal="true" className="utility-drawer-panel" role="dialog">
            <header className="utility-drawer-header">
              <div className="utility-drawer-title">
                <span>
                  <ActionIcon type={activeDrawerDetails.asset} />
                </span>
                <div>
                  <p className="eyebrow">Utility Drawer</p>
                  <h2>{activeDrawerDetails.label}</h2>
                </div>
              </div>
              <button className="utility-drawer-close" type="button" onClick={() => onDrawerChange(null)}>
                <ActionIcon type="Close" />
                <span className="sr-only">Close</span>
              </button>
            </header>

            <div className="utility-drawer-body">
              {activeDrawer === 'trade' ? (
                <BankTradePanel
                  actionsUnlocked={actionsUnlocked}
                  game={game}
                  isMyTurn={isMyTurn}
                  me={me}
                  pendingAction={pendingAction}
                  surface="tray"
                  onTradeWithBank={onTradeWithBank}
                  onCreateTradeOffer={onCreateTradeOffer}
                  onAcceptTradeOffer={onAcceptTradeOffer}
                  onRejectTradeOffer={onRejectTradeOffer}
                  onCancelTradeOffer={onCancelTradeOffer}
                />
              ) : null}
              {activeDrawer === 'cards' ? (
                <DevelopmentCardsPanel
                  actionsUnlocked={actionsUnlocked}
                  developmentLockReason={developmentLockReason}
                  game={game}
                  pendingAction={pendingAction}
                  playerId={playerId}
                  onBuyDevelopmentCard={onBuyDevelopmentCard}
                  onPlayYearOfPlenty={onPlayYearOfPlenty}
                  onPlayMonopoly={onPlayMonopoly}
                  onPlayKnight={onPlayKnight}
                  onStartRoadBuilding={onStartRoadBuilding}
                />
              ) : null}
              {activeDrawer === 'log' ? <GameLogPanel game={game} /> : null}
              {activeDrawer === 'details' ? <MatchDetailsPanel game={game} me={me} /> : null}
            </div>
          </section>
        </div>
      ) : null}
    </>
  );
}

function getUtilitySummary(id: UtilityDrawerId, game: GameState, me: PlayerGameState) {
  if (id === 'trade') {
    const pendingOffers = game.tradeOffers.filter((offer) => offer.status === 'Pending').length;
    if (pendingOffers > 0) {
      return `${pendingOffers} offer${pendingOffers === 1 ? '' : 's'}`;
    }

    const bestRate = resources.reduce((best, resource) => {
      const rate = me.tradeRates.find((tradeRate) => tradeRate.resource === resource)?.rate ?? 4;
      return Math.min(best, rate);
    }, 4);

    return `${bestRate}:1 best`;
  }

  if (id === 'cards') {
    return `${me.developmentCardCount} held`;
  }

  if (id === 'details') {
    return `${game.players.length} players`;
  }

  const latestLog = [...game.log].reverse()[0];
  return latestLog ? `Entry ${latestLog.sequence}` : 'No events';
}

function SuppliesPanel({ me }: { me: PlayerGameState }) {
  return (
    <section className="tray-section supplies-tray-section">
      <div className="panel-heading">
        <h2>Supplies</h2>
        <Boxes size={16} />
      </div>
      <div className="supply-grid">
        {resources.map((resource) => (
          <ResourceStripItem
            amount={me.supplies[resource] ?? 0}
            className={`supply-card supply-${resource.toLowerCase()}`}
            key={resource}
            type={resource}
          />
        ))}
      </div>
    </section>
  );
}

function DevelopmentCardsPanel({
  actionsUnlocked: _actionsUnlocked,
  developmentLockReason: _developmentLockReason,
  game,
  pendingAction,
  playerId,
  onBuyDevelopmentCard,
  onPlayYearOfPlenty,
  onPlayMonopoly,
  onPlayKnight,
  onStartRoadBuilding
}: DevelopmentCardsPanelProps) {
  const me = game.players.find((player) => player.playerId === playerId) ?? null;
  const [yearPickOne, setYearPickOne] = useState<ResourceType>('Wood');
  const [yearPickTwo, setYearPickTwo] = useState<ResourceType>('Grain');
  const [monopolyResource, setMonopolyResource] = useState<ResourceType>('Wood');

  if (!me) {
    return null;
  }

  const availability = getDevelopmentCardAvailability(game, me, playerId, pendingAction);
  const missingPurchaseText = formatMissingDevelopmentCardResources(availability.missingPurchaseResources);

  const buyCard = async () => {
    if (!availability.canBuyCard) {
      return;
    }

    try {
      await onBuyDevelopmentCard(game.roomCode);
    } catch {
      // The shared error surface explains the authoritative rejection.
    }
  };

  if (game.phase === 'Setup') {
    return (
      <section className="tray-section development-lock-card">
        <div className="panel-heading">
          <h2>Development</h2>
          <Hand size={16} />
        </div>
        <p className="empty-note">Development Cards are disabled during setup.</p>
      </section>
    );
  }

  return (
    <section className="tray-section development-tray-section">
      <div className="panel-heading">
        <h2>Development</h2>
        <Hand size={16} />
      </div>
      <div className="development-buy-row">
        <div>
          <strong>{game.developmentDeckCount}</strong>
          <span>cards left</span>
        </div>
        <button
          className="primary-button"
          type="button"
          disabled={!availability.canBuyCard}
          title={availability.buyDisabledReason ?? undefined}
          onClick={() => void buyCard()}
        >
          Buy Card
        </button>
      </div>
      <p className="cost-caption">
        {availability.buyDisabledReason ?? <><span>Cost</span> <ResourceCost compact cost={developmentCardCost} /></>}
      </p>
      <div className="development-cost-grid" aria-label="Development Card purchase cost">
        {(['Wool', 'Grain', 'Stone'] as ResourceType[]).map((resource) => (
          <span
            aria-label={`${resource}: owned ${me.supplies[resource] ?? 0}, required ${developmentCardCost[resource] ?? 0}`}
            data-affordable={(me.supplies[resource] ?? 0) >= (developmentCardCost[resource] ?? 0)}
            key={resource}
            title={resource}
          >
            <b><ResourceIcon decorative size="md" type={resource} /> <span>{developmentCardCost[resource] ?? 0}</span></b>
            <small>owned {me.supplies[resource] ?? 0}</small>
            <span className="sr-only">{resource}</span>
          </span>
        ))}
      </div>
      {missingPurchaseText ? <p className="development-missing-list">{missingPurchaseText}</p> : null}

      <div className="development-card-list">
        {me.developmentCards.length === 0 ? (
          <p className="empty-note">Your development cards will appear here.</p>
        ) : (
          me.developmentCards.map((card) => (
          <DevelopmentCardItem
              availability={availability.playableCards.find((candidate) => candidate.cardId === card.cardId)!}
              card={card}
              game={game}
              key={card.cardId}
              monopolyResource={monopolyResource}
              setMonopolyResource={setMonopolyResource}
              setYearPickOne={setYearPickOne}
              setYearPickTwo={setYearPickTwo}
              yearPickOne={yearPickOne}
              yearPickTwo={yearPickTwo}
              onPlayYearOfPlenty={onPlayYearOfPlenty}
              onPlayMonopoly={onPlayMonopoly}
              onPlayKnight={onPlayKnight}
              onStartRoadBuilding={onStartRoadBuilding}
            />
          ))
        )}
      </div>
    </section>
  );
}

function GameLogPanel({ game }: { game: GameState }) {
  const recentLog = [...game.log].reverse().slice(0, 8);

  return (
    <section className="tray-section game-log-card">
      <div className="panel-heading">
        <h2>Log</h2>
        <ScrollText size={16} />
      </div>
      <ol>
        {recentLog.map((entry) => (
          <li key={entry.sequence}>
            <span>{entry.sequence}</span>
            <AssetLogMessage message={entry.message} />
          </li>
        ))}
      </ol>
    </section>
  );
}

function AssetLogMessage({ message }: { message: string }) {
  const parts = message.split(/\b(Wood|Clay|Wool|Grain|Stone)\b/g);
  const eventIcon = getLogEventIcon(message);

  return (
    <span className="asset-log-message" aria-label={message}>
      <span aria-hidden="true" className="asset-log-event">{eventIcon}</span>
      {parts.map((part, index) => resources.includes(part as ResourceType) ? (
        <span aria-hidden="true" className="asset-log-resource" key={`${part}-${index}`}>
          <ResourceIcon decorative size="xs" type={part as ResourceType} />
          <span className="sr-only">{part}</span>
        </span>
      ) : <span aria-hidden="true" key={`text-${index}`}>{part}</span>)}
    </span>
  );
}

function getLogEventIcon(message: string) {
  if (/\bCamp\b/i.test(message)) return <PieceAsset decorative type="Camp" />;
  if (/\bStronghold\b/i.test(message)) return <PieceAsset decorative type="Stronghold" />;
  if (/\bTrail\b/i.test(message)) return <PieceAsset decorative type="Trail" />;
  if (/\bWarden\b/i.test(message)) return <ActionIcon decorative size="xs" type="MoveWarden" />;
  if (/\bKnight\b|Development Card/i.test(message)) return <ActionIcon decorative size="xs" type="Cards" />;
  if (/\btrade|offer\b/i.test(message)) return <ActionIcon decorative size="xs" type="Trade" />;
  if (/\broll|dice\b/i.test(message)) return <ActionIcon decorative size="xs" type="RollDice" />;
  if (/\bSupply|supplies\b/i.test(message)) return <ActionIcon decorative size="xs" type="Supplies" />;
  return <ActionIcon decorative size="xs" type="GameLog" />;
}

function MatchDetailsPanel({ game, me }: { game: GameState; me: PlayerGameState }) {
  const currentPlayer = game.players.find((player) => player.playerId === game.currentPlayerId);
  const setupPlayer = game.players.find((player) => player.playerId === game.currentSetupPlayerId);
  const awardSummary = [
    game.largestArmyPlayerId
      ? `Largest Army: ${game.players.find((player) => player.playerId === game.largestArmyPlayerId)?.playerName ?? 'Claimed'} (${game.largestArmyKnightCount})`
      : 'Largest Army: unclaimed',
    game.longestTrailPlayerId
      ? `Longest Trail: ${game.players.find((player) => player.playerId === game.longestTrailPlayerId)?.playerName ?? 'Claimed'} (${game.longestTrailLength})`
      : 'Longest Trail: unclaimed'
  ];

  return (
    <section className="tray-section match-details-panel">
      <div className="panel-heading">
        <h2>Match Details</h2>
        <Info size={16} />
      </div>
      <div className="match-details-grid">
        <span>Room</span>
        <b>{game.roomCode}</b>
        <span>Goal</span>
        <b>{game.winningVictoryPoints} Victory Points</b>
        <span>Phase</span>
        <b>{game.phase === 'Setup' ? `Setup - ${game.setupRound === 'SecondPlacement' ? 'Round 2' : 'Round 1'}` : `Turn ${game.turnNumber}`}</b>
        <span>Current</span>
        <b>{game.phase === 'Setup' ? setupPlayer?.playerName ?? 'Player' : currentPlayer?.playerName ?? 'Player'}</b>
        <span>Regions</span>
        <b>{game.board.tiles.length}</b>
        <span>Harbors</span>
        <b>{game.board.harborSlots.length}</b>
        <span>Deck</span>
        <b>{game.developmentDeckCount} cards</b>
      </div>
      <div className="match-piece-details" aria-label="Your remaining construction pieces">
        <PieceCount remaining={me.remainingTrails} total={me.totalTrails} type="Trail" />
        <PieceCount remaining={me.remainingCamps} total={me.totalCamps} type="Camp" />
        <PieceCount remaining={me.remainingStrongholds} total={me.totalStrongholds} type="Stronghold" />
      </div>
      <div className="direct-build-help">
        <div aria-hidden="true">
          <PieceAsset decorative type="Trail" />
          <PieceAsset decorative type="Camp" />
          <PieceAsset decorative type="Stronghold" />
        </div>
        <p><strong>Direct building</strong><span>After rolling, select an available edge or intersection on the island. Select one of your Camps to upgrade it.</span></p>
      </div>
      <div className="match-awards-list">
        {awardSummary.map((award) => (
          <span key={award}>
            <Trophy size={14} />
            {award}
          </span>
        ))}
      </div>
      <div className="match-player-order">
        <strong>Player order</strong>
        <ol>
          {game.playerOrder.map((orderedPlayerId) => {
            const player = game.players.find((candidate) => candidate.playerId === orderedPlayerId);
            return player ? <li key={player.playerId}>{player.playerName}</li> : null;
          })}
        </ol>
      </div>
    </section>
  );
}

const emptyDiscardSelection: Record<ResourceType, number> = {
  Wood: 0,
  Clay: 0,
  Wool: 0,
  Grain: 0,
  Stone: 0
};

interface WardenPanelProps {
  game: GameState;
  me: PlayerGameState;
  playerId: string | null;
  pendingAction: string | null;
  onDiscardForWarden: (roomCode: string, discardedResources: Partial<Record<ResourceType, number>>) => Promise<void>;
  onStealFromWardenVictim: (roomCode: string, victimPlayerId: string) => Promise<void>;
}

export function WardenPanel({
  game,
  me,
  playerId,
  pendingAction,
  onDiscardForWarden,
  onStealFromWardenVictim
}: WardenPanelProps) {
  const [discardSelection, setDiscardSelection] = useState<Record<ResourceType, number>>(emptyDiscardSelection);
  const [victimPlayerId, setVictimPlayerId] = useState('');
  const currentWardenPlayer = game.players.find((player) => player.playerId === game.currentWardenPlayerId) ?? null;
  const myDiscard = game.pendingWardenDiscards.find((discard) => discard.playerId === playerId) ?? null;
  const pendingDiscardPlayers = game.pendingWardenDiscards
    .map((discard) => {
      const player = game.players.find((candidate) => candidate.playerId === discard.playerId);
      return player ? `${player.playerName} (${discard.requiredAmount})` : null;
    })
    .filter(Boolean)
    .join(', ');
  const victimOptions = game.wardenVictimOptions
    .map((victimId) => game.players.find((player) => player.playerId === victimId))
    .filter((player): player is PlayerGameState => !!player);
  const discardTotal = resources.reduce((sum, resource) => sum + (discardSelection[resource] ?? 0), 0);
  const discardIsValid = !!myDiscard
    && discardTotal === myDiscard.requiredAmount
    && resources.every((resource) => (discardSelection[resource] ?? 0) <= (me.supplies[resource] ?? 0));
  const isCurrentWardenPlayer = game.currentWardenPlayerId === playerId;
  const wardenTitle = game.pendingWardenAction === 'Discarding'
    ? 'Discard Supplies'
    : game.pendingWardenAction === 'MoveWarden'
      ? 'Move the Warden'
      : 'Choose a victim';
  const wardenHelper = game.pendingWardenAction === 'Discarding'
    ? pendingDiscardPlayers
      ? `Waiting on ${pendingDiscardPlayers}.`
      : 'Players with large hands must discard before the Warden moves.'
    : game.pendingWardenAction === 'MoveWarden'
      ? 'Choose a highlighted region on the island.'
      : 'Choose one eligible adjacent player to lose a random Supply.';

  useEffect(() => {
    setDiscardSelection(emptyDiscardSelection);
  }, [game.pendingWardenAction, game.pendingWardenDiscards.length, playerId]);

  useEffect(() => {
    if (victimOptions.length > 0 && !victimOptions.some((player) => player.playerId === victimPlayerId)) {
      setVictimPlayerId(victimOptions[0].playerId);
    }
  }, [victimOptions, victimPlayerId]);

  const updateDiscard = (resource: ResourceType, value: number) => {
    const safeValue = Math.max(0, Math.min(me.supplies[resource] ?? 0, Number.isFinite(value) ? value : 0));
    setDiscardSelection((current) => ({
      ...current,
      [resource]: safeValue
    }));
  };

  return (
    <section className="game-side-card warden-flow-card">
      <span className="context-panel-label">Warden</span>
      <ContextStateIcon kind="warden" />
      <div className="turn-focus">
        <strong>{wardenTitle}</strong>
        <p>{wardenHelper}</p>
      </div>

      {myDiscard ? (
        <div className="warden-discard-box">
          <strong>You must discard {myDiscard.requiredAmount} supplies</strong>
          <div className="warden-discard-grid">
            {resources.map((resource) => (
              <label aria-label={`${resource} discard amount`} key={resource} title={resource}>
                <span aria-hidden="true"><ResourceIcon decorative size="md" type={resource} /></span>
                <input
                  aria-label={`${resource} discard amount`}
                  min="0"
                  max={me.supplies[resource] ?? 0}
                  type="number"
                  value={discardSelection[resource] ?? 0}
                  onChange={(event) => updateDiscard(resource, Number(event.target.value))}
                />
              </label>
            ))}
          </div>
          <p className="cost-caption">{discardTotal}/{myDiscard.requiredAmount} selected</p>
          <button
            className="primary-button full-action"
            disabled={!discardIsValid || !!pendingAction}
            type="button"
            onClick={() => void onDiscardForWarden(game.roomCode, discardSelection)}
          >
            <Check aria-hidden="true" size={18} />
            Confirm Discard
          </button>
        </div>
      ) : game.pendingWardenAction === 'Discarding' ? (
        <p className="empty-note">Waiting for required discards.</p>
      ) : null}

      {game.pendingWardenAction === 'MoveWarden' ? (
        <p className="empty-note">
          {isCurrentWardenPlayer ? 'Select a highlighted region on the board.' : `Waiting for ${currentWardenPlayer?.playerName ?? 'the current player'} to move the Warden.`}
        </p>
      ) : null}

      {game.pendingWardenAction === 'ChooseVictim' ? (
        isCurrentWardenPlayer && victimOptions.length > 0 ? (
          <div className="card-controls">
            <select value={victimPlayerId} onChange={(event) => setVictimPlayerId(event.target.value)}>
              {victimOptions.map((player) => (
                <option value={player.playerId} key={player.playerId}>
                  {player.playerName}
                </option>
              ))}
            </select>
            <button
              className="primary-button full-action"
              type="button"
              disabled={!victimPlayerId || !!pendingAction}
              onClick={() => void onStealFromWardenVictim(game.roomCode, victimPlayerId)}
            >
              <ActionIcon decorative type="MoveWarden" />
              Steal 1 Supply
            </button>
          </div>
        ) : (
          <p className="empty-note">Waiting for victim selection.</p>
        )
      ) : null}
    </section>
  );
}

interface DevelopmentCardItemProps {
  availability: CardAvailability;
  card: PlayerDevelopmentCard;
  game: GameState;
  yearPickOne: ResourceType;
  yearPickTwo: ResourceType;
  monopolyResource: ResourceType;
  setYearPickOne: (resource: ResourceType) => void;
  setYearPickTwo: (resource: ResourceType) => void;
  setMonopolyResource: (resource: ResourceType) => void;
  onPlayYearOfPlenty: (roomCode: string, cardId: string, selectedResources: ResourceType[]) => Promise<void>;
  onPlayMonopoly: (roomCode: string, cardId: string, selectedResource: ResourceType) => Promise<void>;
  onPlayKnight: (roomCode: string, cardId: string, targetTileId: string, victimPlayerId: string | null) => Promise<void>;
  onStartRoadBuilding: (roomCode: string, cardId: string) => Promise<void>;
}

function DevelopmentCardItem({
  availability,
  card,
  game,
  yearPickOne,
  yearPickTwo,
  monopolyResource,
  setYearPickOne,
  setYearPickTwo,
  setMonopolyResource,
  onPlayYearOfPlenty,
  onPlayMonopoly,
  onPlayKnight,
  onStartRoadBuilding
}: DevelopmentCardItemProps) {
  const type = card.type ?? 'VictoryPoint';
  const details = cardText[type];
  const canPlay = availability.canPlay;
  const displayStatus = canPlay ? 'Playable' : availability.disabledReason ?? formatStatus(card.status);

  const runCardAction = async (action: () => Promise<void>) => {
    if (!canPlay) {
      return;
    }

    try {
      await action();
    } catch {
      setYearPickOne('Wood');
      setYearPickTwo('Grain');
      setMonopolyResource('Wood');
      // The shared error surface explains authoritative stale-state failures.
    }
  };

  return (
    <article className={`development-card development-card-${type.toLowerCase()}`}>
      <DevelopmentCardArtwork type={type} />
      <div className="development-card-top">
        <div>
          <strong>{details.title}</strong>
          <DevelopmentCardStatus available={canPlay} status={card.status} text={displayStatus} type={type} />
        </div>
      </div>
      <p>{details.description}</p>

      {type === 'YearOfPlenty' && canPlay ? (
        <div className="card-controls two-selects">
          <ResourceSelect value={yearPickOne} onChange={setYearPickOne} />
          <ResourceSelect value={yearPickTwo} onChange={setYearPickTwo} />
          <button type="button" onClick={() => void runCardAction(() => onPlayYearOfPlenty(game.roomCode, card.cardId, [yearPickOne, yearPickTwo]))}>
            <ActionIcon decorative size="sm" type="Cards" />
            Play
          </button>
        </div>
      ) : null}

      {type === 'Monopoly' && canPlay ? (
        <div className="card-controls">
          <ResourceSelect value={monopolyResource} onChange={setMonopolyResource} />
          <button type="button" onClick={() => void runCardAction(() => onPlayMonopoly(game.roomCode, card.cardId, monopolyResource))}>
            <ActionIcon decorative size="sm" type="Cards" />
            Play
          </button>
        </div>
      ) : null}

      {type === 'Knight' ? (
        <div className="card-controls">
          <button type="button" disabled={!canPlay} title={!canPlay ? displayStatus : undefined} onClick={() => void runCardAction(() => onPlayKnight(game.roomCode, card.cardId, '', null))}>
            <ActionIcon decorative size="sm" type="Cards" />
            Play
          </button>
        </div>
      ) : null}

      {type === 'RoadBuilding' ? (
        <div className="card-controls">
          <button type="button" disabled={!canPlay} title={!canPlay ? displayStatus : undefined} onClick={() => void runCardAction(() => onStartRoadBuilding(game.roomCode, card.cardId))}>
            <ActionIcon decorative size="sm" type="Cards" />
            Start
          </button>
        </div>
      ) : null}

      {!canPlay && type !== 'VictoryPoint' ? (
        <p className="development-card-disabled-reason">{displayStatus}</p>
      ) : null}
      {type === 'VictoryPoint' ? (
        <p className="development-card-passive">Passive - 1 hidden Victory Point</p>
      ) : null}
    </article>
  );
}

function ResourceSelect({ value, onChange }: { value: ResourceType; onChange: (resource: ResourceType) => void }) {
  return (
    <select value={value} onChange={(event) => onChange(event.target.value as ResourceType)}>
      {resources.map((resource) => (
        <option key={resource}>{resource}</option>
      ))}
    </select>
  );
}

function formatStatus(status: string) {
  return status.replace(/([A-Z])/g, ' $1').trim();
}

function getTurnContext(game: GameState, playerId: string | null) {
  const me = game.players.find((player) => player.playerId === playerId) ?? null;
  const isNormalTurn = game.phase === 'NormalTurn';
  const isMyTurn = isNormalTurn && game.currentPlayerId === playerId && game.status === 'InProgress';
  const wardenFlowActive = game.pendingWardenAction !== 'None';
  const developmentEffectActive = !!game.activeDevelopmentCardEffect;
  const actionsUnlocked = isMyTurn && game.hasRolledThisTurn && !wardenFlowActive && !developmentEffectActive;
  const developmentLockReason = game.phase === 'Setup'
    ? 'Development Cards unlock after setup.'
    : wardenFlowActive
      ? 'Resolve the Warden first.'
      : developmentEffectActive
        ? 'Resolve the current Development Card action first.'
      : !isMyTurn
        ? 'Waiting for your turn.'
        : !game.hasRolledThisTurn
          ? 'Roll to unlock building, trading, and card purchases. You may play one eligible Development Card before rolling.'
          : null;
  const setupPlayer = game.players.find((player) => player.playerId === game.currentSetupPlayerId);
  const currentTurnPlayer = game.players.find((player) => player.playerId === game.currentPlayerId);

  return {
    actionsUnlocked,
    currentTurnPlayer,
    developmentLockReason,
    isMyTurn,
    me,
    setupPlayer,
    wardenFlowActive
  };
}

function DevelopmentCardStatus({
  available,
  status,
  text,
  type
}: {
  available: boolean;
  status: string;
  text: string;
  type: DevelopmentCardType;
}) {
  const icon = available
    ? <Check size={13} />
    : type === 'VictoryPoint'
      ? <Star size={13} />
      : status === 'BoughtThisTurn'
        ? <Clock3 size={13} />
        : <LockKeyhole size={13} />;

  return (
    <span aria-label={text} className="development-card-status" data-available={available} title={text}>
      <span aria-hidden="true">{icon}</span>
      <span>{text}</span>
    </span>
  );
}
