import { Anchor, Boxes, Clock3, Hand, ScrollText, Shield, Sparkles, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { DevelopmentCardType, GameState, PlayerDevelopmentCard, PlayerGameState, ResourceType } from '../types/game';
import { developmentCardCost, resources } from '../types/game';
import { BankTradePanel } from './BankTradePanel';

interface TurnStatusPanelProps {
  game: GameState;
  playerId: string | null;
  pendingAction: string | null;
  onEndTurn: (roomCode: string) => Promise<void>;
  onRollDice: (roomCode: string) => Promise<void>;
  onCancelActiveDevelopmentCard: (roomCode: string) => Promise<void>;
}

interface BottomActionTrayProps {
  game: GameState;
  playerId: string | null;
  pendingAction: string | null;
  onBuyDevelopmentCard: (roomCode: string) => Promise<void>;
  onTradeWithBank: (roomCode: string, offeredResource: ResourceType, requestedResource: ResourceType) => Promise<void>;
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

const cardText: Record<DevelopmentCardType, { title: string; description: string; icon: string }> = {
  Knight: {
    title: 'Knight',
    description: 'Move the Warden and steal 1 random supply when possible.',
    icon: 'K'
  },
  RoadBuilding: {
    title: 'Road Building',
    description: 'Prepare up to 2 free Trails once trail placement exists.',
    icon: 'R'
  },
  YearOfPlenty: {
    title: 'Year of Plenty',
    description: 'Take any 2 supplies. You may choose the same supply twice.',
    icon: 'Y'
  },
  Monopoly: {
    title: 'Monopoly',
    description: 'Name a supply and collect all of it from opponents.',
    icon: 'M'
  },
  VictoryPoint: {
    title: 'Victory Point',
    description: 'Hidden private point. Reveals when scoring the win.',
    icon: 'V'
  }
};

const resourceSymbols: Record<ResourceType, string> = {
  Wood: 'Wd',
  Clay: 'C',
  Wool: 'Wl',
  Grain: 'G',
  Stone: 'S'
};

type UtilityDrawerId = 'supplies' | 'trade' | 'cards' | 'log';

const utilityActions: Array<{ id: UtilityDrawerId; label: string; icon: typeof Boxes }> = [
  { id: 'supplies', label: 'Supplies', icon: Boxes },
  { id: 'trade', label: 'Trade', icon: Anchor },
  { id: 'cards', label: 'Cards', icon: Hand },
  { id: 'log', label: 'Log', icon: ScrollText }
];

export function TurnStatusPanel({
  game,
  playerId,
  pendingAction,
  onEndTurn,
  onRollDice,
  onCancelActiveDevelopmentCard
}: TurnStatusPanelProps) {
  const { actionsUnlocked, currentTurnPlayer, developmentLockReason, isMyTurn, me, setupPlayer, wardenFlowActive } = getTurnContext(game, playerId);
  const turnPrimaryText = game.phase === 'Setup'
    ? `${setupPlayer?.playerName ?? 'Player'} is setting up`
    : isMyTurn
      ? 'Your turn'
      : `${currentTurnPlayer?.playerName ?? 'Player'} is playing`;
  const turnNextStep = game.phase === 'Setup'
    ? `${game.setupRound === 'SecondPlacement' ? 'Second' : 'First'} setup round`
    : wardenFlowActive
      ? 'Resolve the Warden'
      : game.hasRolledThisTurn
        ? `Dice result ${game.lastDiceRoll}`
        : 'Roll to unlock actions';
  const primaryButtonLabel = game.hasRolledThisTurn ? `Rolled ${game.lastDiceRoll}` : 'Roll Dice';

  if (!me) {
    return null;
  }

  return (
    <section className="game-side-card turn-card right-status-card">
      <div className="panel-heading">
        <h2>Turn</h2>
        <Clock3 size={16} />
      </div>
      <div className="turn-focus">
        <span>{game.phase === 'Setup' ? 'Setup phase' : `Turn ${game.turnNumber}`}</span>
        <strong>{turnPrimaryText}</strong>
        <p>{turnNextStep}</p>
      </div>
      <div className="turn-mini-grid compact-turn-grid">
        <span>
          <b>{game.phase === 'Setup' ? (game.setupStep === 'PlaceTrail' ? 'Trail' : 'Camp') : game.hasRolledThisTurn ? game.lastDiceRoll ?? '-' : '-'}</b>
          {game.phase === 'Setup' ? 'next piece' : 'dice'}
        </span>
        <span>
          <b>{me.totalVictoryPoints}</b>
          your VP
        </span>
      </div>
      <div className="status-list compact-status-list">
        {game.phase === 'NormalTurn' ? (
          <span>{developmentLockReason ?? 'Actions are available.'}</span>
        ) : null}
      </div>
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
            disabled={!actionsUnlocked || !!pendingAction}
            onClick={() => void onCancelActiveDevelopmentCard(game.roomCode)}
          >
            Cancel
          </button>
        </div>
      ) : null}
      {game.phase === 'NormalTurn' ? (
        <button className="primary-button full-action" type="button" disabled={!isMyTurn || game.hasRolledThisTurn || !!pendingAction} onClick={() => void onRollDice(game.roomCode)}>
          {primaryButtonLabel}
        </button>
      ) : null}
      {game.phase === 'NormalTurn' ? (
        <button className="secondary-button full-action" type="button" disabled={!actionsUnlocked || !!pendingAction} onClick={() => void onEndTurn(game.roomCode)}>
          End Turn
        </button>
      ) : null}
    </section>
  );
}

export function BottomActionTray({
  game,
  playerId,
  pendingAction,
  onBuyDevelopmentCard,
  onTradeWithBank,
  onPlayYearOfPlenty,
  onPlayMonopoly,
  onPlayKnight,
  onStartRoadBuilding
}: BottomActionTrayProps) {
  const [activeDrawer, setActiveDrawer] = useState<UtilityDrawerId | null>(null);
  const { actionsUnlocked, developmentLockReason, isMyTurn, me } = getTurnContext(game, playerId);
  const activeDrawerDetails = activeDrawer ? utilityActions.find((action) => action.id === activeDrawer) ?? null : null;
  const ActiveIcon = activeDrawerDetails?.icon;

  if (!me) {
    return null;
  }

  return (
    <>
      <nav className="bottom-action-tray utility-action-bar" aria-label="Player utilities">
        {utilityActions.map((action) => {
          const Icon = action.icon;
          const isOpen = activeDrawer === action.id;
          const summary = getUtilitySummary(action.id, game, me);

          return (
            <button
              aria-label={`${action.label}: ${summary}`}
              aria-expanded={isOpen}
              className="utility-action-button"
              key={action.id}
              type="button"
              onClick={() => setActiveDrawer((current) => current === action.id ? null : action.id)}
            >
              <Icon size={17} />
              <strong>{action.label}</strong>
              <span>{summary}</span>
            </button>
          );
        })}
      </nav>

      {activeDrawerDetails && ActiveIcon ? (
        <div className="utility-drawer-overlay" role="presentation">
          <button
            aria-label="Close utility panel"
            className="utility-drawer-backdrop"
            type="button"
            onClick={() => setActiveDrawer(null)}
          />
          <section aria-label={`${activeDrawerDetails.label} panel`} aria-modal="true" className="utility-drawer-panel" role="dialog">
            <header className="utility-drawer-header">
              <div className="utility-drawer-title">
                <span>
                  <ActiveIcon size={17} />
                </span>
                <div>
                  <p className="eyebrow">Utility Drawer</p>
                  <h2>{activeDrawerDetails.label}</h2>
                </div>
              </div>
              <button className="utility-drawer-close" type="button" onClick={() => setActiveDrawer(null)}>
                <X size={17} />
                <span className="sr-only">Close</span>
              </button>
            </header>

            <div className="utility-drawer-body">
              {activeDrawer === 'supplies' ? <SuppliesPanel me={me} /> : null}
              {activeDrawer === 'trade' ? (
                <BankTradePanel
                  actionsUnlocked={actionsUnlocked}
                  game={game}
                  isMyTurn={isMyTurn}
                  me={me}
                  pendingAction={pendingAction}
                  surface="tray"
                  onTradeWithBank={onTradeWithBank}
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
            </div>
          </section>
        </div>
      ) : null}
    </>
  );
}

function getUtilitySummary(id: UtilityDrawerId, game: GameState, me: PlayerGameState) {
  if (id === 'supplies') {
    return `${me.supplyCount} total`;
  }

  if (id === 'trade') {
    const bestRate = resources.reduce((best, resource) => {
      const rate = me.tradeRates.find((tradeRate) => tradeRate.resource === resource)?.rate ?? 4;
      return Math.min(best, rate);
    }, 4);

    return `${bestRate}:1 best`;
  }

  if (id === 'cards') {
    return `${me.developmentCardCount} held`;
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
          <article className={`supply-card supply-${resource.toLowerCase()}`} key={resource}>
            <span className="resource-symbol">{resourceSymbols[resource]}</span>
            <span>{resource}</span>
            <strong>{me.supplies[resource] ?? 0}</strong>
          </article>
        ))}
      </div>
    </section>
  );
}

function DevelopmentCardsPanel({
  actionsUnlocked,
  developmentLockReason,
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

  const canAffordDevelopmentCard = useMemo(() => {
    if (!me) {
      return false;
    }

    return resources.every((resource) => (me.supplies[resource] ?? 0) >= (developmentCardCost[resource] ?? 0));
  }, [me]);

  if (!me) {
    return null;
  }

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
          disabled={!actionsUnlocked || !canAffordDevelopmentCard || game.developmentDeckCount === 0 || !!pendingAction}
          onClick={() => void onBuyDevelopmentCard(game.roomCode)}
        >
          Buy Card
        </button>
      </div>
      <p className="cost-caption">
        {developmentLockReason ?? 'Cost: 1 Wool + 1 Grain + 1 Stone'}
      </p>

      <div className="development-card-list">
        {me.developmentCards.length === 0 ? (
          <p className="empty-note">Your development cards will appear here.</p>
        ) : (
          me.developmentCards.map((card) => (
            <DevelopmentCardItem
              actionsUnlocked={actionsUnlocked}
              card={card}
              game={game}
              key={card.cardId}
              lockReason={developmentLockReason}
              monopolyResource={monopolyResource}
              pendingAction={pendingAction}
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
            {entry.message}
          </li>
        ))}
      </ol>
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
      <div className="panel-heading">
        <h2>Warden</h2>
        <Shield size={16} />
      </div>
      <div className="status-list">
        <span>{currentWardenPlayer?.playerName ?? 'Current player'} resolves the Warden</span>
        <span>
          {game.pendingWardenAction === 'Discarding'
            ? `Discarding: ${pendingDiscardPlayers || 'none'}`
            : game.pendingWardenAction === 'MoveWarden'
              ? 'Move the Warden to a new region'
              : 'Choose an adjacent victim'}
        </span>
      </div>

      {myDiscard ? (
        <div className="warden-discard-box">
          <strong>You must discard {myDiscard.requiredAmount} supplies</strong>
          <div className="warden-discard-grid">
            {resources.map((resource) => (
              <label key={resource}>
                <span>{resource}</span>
                <input
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
            Discard Supplies
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
              type="button"
              disabled={!victimPlayerId || !!pendingAction}
              onClick={() => void onStealFromWardenVictim(game.roomCode, victimPlayerId)}
            >
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
  card: PlayerDevelopmentCard;
  game: GameState;
  actionsUnlocked: boolean;
  lockReason: string | null;
  pendingAction: string | null;
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
  card,
  game,
  actionsUnlocked,
  lockReason,
  pendingAction,
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
  const canPlay = actionsUnlocked && card.status === 'Playable' && !pendingAction && type !== 'VictoryPoint';
  const displayStatus = getCardDisplayStatus(card, type, canPlay, lockReason);

  return (
    <article className={`development-card development-card-${type.toLowerCase()}`}>
      <div className="development-card-top">
        <span className="card-symbol">{details.icon}</span>
        <div>
          <strong>{details.title}</strong>
          <span className="development-card-status">{displayStatus}</span>
        </div>
      </div>
      <p>{details.description}</p>

      {type === 'YearOfPlenty' ? (
        <div className="card-controls two-selects">
          <ResourceSelect value={yearPickOne} onChange={setYearPickOne} />
          <ResourceSelect value={yearPickTwo} onChange={setYearPickTwo} />
          <button type="button" disabled={!canPlay} onClick={() => void onPlayYearOfPlenty(game.roomCode, card.cardId, [yearPickOne, yearPickTwo])}>
            Play
          </button>
        </div>
      ) : null}

      {type === 'Monopoly' ? (
        <div className="card-controls">
          <ResourceSelect value={monopolyResource} onChange={setMonopolyResource} />
          <button type="button" disabled={!canPlay} onClick={() => void onPlayMonopoly(game.roomCode, card.cardId, monopolyResource)}>
            Play
          </button>
        </div>
      ) : null}

      {type === 'Knight' ? (
        <div className="card-controls">
          <button type="button" disabled={!canPlay} onClick={() => void onPlayKnight(game.roomCode, card.cardId, '', null)}>
            Play
          </button>
        </div>
      ) : null}

      {type === 'RoadBuilding' ? (
        <div className="card-controls">
          <button type="button" disabled={!canPlay} onClick={() => void onStartRoadBuilding(game.roomCode, card.cardId)}>
            Start
          </button>
        </div>
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

function getCardDisplayStatus(
  card: PlayerDevelopmentCard,
  type: DevelopmentCardType,
  canPlay: boolean,
  lockReason: string | null
) {
  if (type === 'VictoryPoint') {
    return 'Private VP';
  }

  if (card.status === 'BoughtThisTurn') {
    return 'Bought this turn';
  }

  if (card.status === 'AlreadyPlayed') {
    return 'Played';
  }

  if (canPlay) {
    return 'Playable';
  }

  return lockReason ?? formatStatus(card.status);
}

function getTurnContext(game: GameState, playerId: string | null) {
  const me = game.players.find((player) => player.playerId === playerId) ?? null;
  const isNormalTurn = game.phase === 'NormalTurn';
  const isMyTurn = isNormalTurn && game.currentPlayerId === playerId && game.status === 'InProgress';
  const wardenFlowActive = game.pendingWardenAction !== 'None';
  const actionsUnlocked = isMyTurn && game.hasRolledThisTurn && !wardenFlowActive;
  const developmentLockReason = game.phase === 'Setup'
    ? 'Development Cards unlock after setup.'
    : wardenFlowActive
      ? 'Resolve the Warden first.'
      : !isMyTurn
        ? 'Waiting for your turn.'
        : !game.hasRolledThisTurn
          ? 'Roll before buying or playing Development Cards.'
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
