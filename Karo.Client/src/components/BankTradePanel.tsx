import { Anchor, ArrowRightLeft, Handshake } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import type { GameState, PlayerGameState, ResourceType } from '../types/game';
import { resources } from '../types/game';
import {
  getTradeAvailability,
  getTradeOfferAvailability
} from '../utils/actionAvailability';
import { ActionIcon, ResourceAmount, ResourceIcon, ResourceInlineSummary } from './GameAsset';

interface BankTradePanelProps {
  game: GameState;
  me: PlayerGameState;
  actionsUnlocked: boolean;
  isMyTurn: boolean;
  pendingAction: string | null;
  surface?: 'card' | 'tray';
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
}

export function BankTradePanel({
  game,
  me,
  actionsUnlocked: _actionsUnlocked,
  isMyTurn: _isMyTurn,
  pendingAction,
  surface = 'card',
  onTradeWithBank,
  onCreateTradeOffer,
  onAcceptTradeOffer,
  onRejectTradeOffer,
  onCancelTradeOffer
}: BankTradePanelProps) {
  const [offeredResource, setOfferedResource] = useState<ResourceType>('Wood');
  const [requestedResource, setRequestedResource] = useState<ResourceType>('Clay');

  useEffect(() => {
    if (offeredResource === requestedResource) {
      setRequestedResource(resources.find((resource) => resource !== offeredResource) ?? 'Wood');
    }
  }, [offeredResource, requestedResource]);

  const availability = useMemo(
    () => getTradeAvailability(game, me, me.playerId, pendingAction),
    [game, me, pendingAction]
  );
  const currentRoute = availability.possibleMaritimeRoutes.find((route) => route.resource === offeredResource)
    ?? availability.possibleMaritimeRoutes[0];
  const currentRate = availability.maritimeRatesByResource[offeredResource];
  const isSameResource = offeredResource === requestedResource;
  const accessibleHarborCount = me.accessibleHarborSlotIds?.length ?? me.accessiblePortIds.length;
  const canTrade = !!currentRoute?.canOffer && !isSameResource && !pendingAction;

  useEffect(() => {
    if (currentRoute?.canOffer || !availability.canMaritimeTrade) {
      return;
    }

    const firstAvailableRoute = availability.possibleMaritimeRoutes.find((route) => route.canOffer);
    if (firstAvailableRoute) {
      setOfferedResource(firstAvailableRoute.resource);
    }
  }, [availability.canMaritimeTrade, availability.possibleMaritimeRoutes, currentRoute?.canOffer]);

  const submitMaritimeTrade = async () => {
    if (!canTrade) {
      return;
    }

    try {
      await onTradeWithBank(game.roomCode, offeredResource, requestedResource);
    } catch {
      const nextGive = availability.possibleMaritimeRoutes.find((route) => route.canOffer)?.resource ?? 'Wood';
      setOfferedResource(nextGive);
      setRequestedResource(resources.find((resource) => resource !== nextGive) ?? 'Clay');
    }
  };

  return (
    <section className={`${surface === 'tray' ? 'tray-section' : 'game-side-card'} bank-trade-card`}>
      <div className="panel-heading">
        <h2>Maritime Trade</h2>
        <ActionIcon type="Trade" />
      </div>

      <div className="trade-summary-card">
        <span>Selected route</span>
        <strong aria-label={`${offeredResource} at ${currentRate.rate} to 1`} title={offeredResource}>
          <ResourceIcon decorative size="md" type={offeredResource} />
          {currentRate.rate}:1
          <span className="sr-only">{offeredResource}</span>
        </strong>
        <small>{currentRoute?.source}</small>
      </div>

      <div className="trade-rate-list" aria-label="Available maritime trade rates">
        {resources.map((resource) => {
          const tradeRate = availability.maritimeRatesByResource[resource];
          const route = availability.possibleMaritimeRoutes.find((candidate) => candidate.resource === resource)!;

          return (
            <button
              aria-label={`${resource}: ${tradeRate.rate} to 1, owned ${route.owned}, required ${route.required}`}
              className="trade-rate-row"
              data-active={resource === offeredResource}
              data-affordable={route.canOffer}
              disabled={!route.canOffer}
              key={resource}
              title={route.disabledReason ?? `${tradeRate.rate}:1 via ${route.source}`}
              type="button"
              onClick={() => setOfferedResource(resource)}
            >
              <span>
                <strong title={resource}><ResourceIcon decorative size="md" type={resource} /><span className="sr-only">{resource}</span></strong>
                <small>{route.source} - owned {route.owned}, required {route.required}</small>
                {!route.canOffer && route.disabledReason ? <em>{route.disabledReason}</em> : null}
              </span>
              <b>{tradeRate.rate}:1</b>
            </button>
          );
        })}
      </div>

      {availability.canMaritimeTrade ? <div className="bank-trade-controls">
        <label>
          <span>Give</span>
          <select disabled={!availability.canMaritimeTrade} value={offeredResource} onChange={(event) => setOfferedResource(event.target.value as ResourceType)}>
            {availability.possibleMaritimeRoutes.map((route) => (
              <option disabled={!route.canOffer} key={route.resource} value={route.resource}>{route.resource} ({route.required}:1)</option>
            ))}
          </select>
        </label>

        <ArrowRightLeft aria-hidden="true" size={18} />

        <label>
          <span>Receive</span>
          <select disabled={!availability.canMaritimeTrade} value={requestedResource} onChange={(event) => setRequestedResource(event.target.value as ResourceType)}>
            {resources.map((resource) => (
              <option disabled={resource === offeredResource} key={resource}>
                {resource}
              </option>
            ))}
          </select>
        </label>
      </div> : null}

      {availability.canMaritimeTrade ? <button
        aria-label={`Trade ${currentRate.rate} ${offeredResource} for 1 ${isSameResource ? 'resource' : requestedResource}`}
        className="primary-button trade-action-button"
        disabled={!canTrade}
        type="button"
        onClick={() => void submitMaritimeTrade()}
      >
        <span>Trade</span>
        <ResourceAmount amount={currentRate.rate} compact type={offeredResource} />
        <ArrowRightLeft aria-hidden="true" size={15} />
        {isSameResource ? <span>Choose another Supply</span> : <ResourceAmount amount={1} compact type={requestedResource} />}
      </button> : null}

      <p className="trade-explain">
        {!availability.canMaritimeTrade ? `${availability.maritimeDisabledReason} ` : ''}
        {currentRoute ? `${currentRoute.required}:1 via ${currentRoute.source}.` : ''}
        {accessibleHarborCount > 0 ? ` You have access to ${accessibleHarborCount} harbor${accessibleHarborCount === 1 ? '' : 's'}.` : ''}
      </p>

      <PlayerTradePanel
        game={game}
        me={me}
        pendingAction={pendingAction}
        onAcceptTradeOffer={onAcceptTradeOffer}
        onCancelTradeOffer={onCancelTradeOffer}
        onCreateTradeOffer={onCreateTradeOffer}
        onRejectTradeOffer={onRejectTradeOffer}
      />
    </section>
  );
}

interface PlayerTradePanelProps {
  game: GameState;
  me: PlayerGameState;
  pendingAction: string | null;
  onCreateTradeOffer: (
    roomCode: string,
    targetPlayerId: string,
    offeredResources: Partial<Record<ResourceType, number>>,
    requestedResources: Partial<Record<ResourceType, number>>
  ) => Promise<void>;
  onAcceptTradeOffer: (roomCode: string, tradeOfferId: string) => Promise<void>;
  onRejectTradeOffer: (roomCode: string, tradeOfferId: string) => Promise<void>;
  onCancelTradeOffer: (roomCode: string, tradeOfferId: string) => Promise<void>;
}

function PlayerTradePanel({
  game,
  me,
  pendingAction,
  onCreateTradeOffer,
  onAcceptTradeOffer,
  onRejectTradeOffer,
  onCancelTradeOffer
}: PlayerTradePanelProps) {
  const availability = useMemo(
    () => getTradeAvailability(game, me, me.playerId, pendingAction),
    [game, me, pendingAction]
  );
  const opponents = availability.validTradeTargets;
  const [targetPlayerId, setTargetPlayerId] = useState(opponents[0]?.playerId ?? '');
  const [offeredResources, setOfferedResources] = useState<Record<ResourceType, number>>(() => createEmptySelection());
  const [requestedResources, setRequestedResources] = useState<Record<ResourceType, number>>(() => createEmptySelection());

  useEffect(() => {
    if (opponents.length === 0) {
      setTargetPlayerId('');
      return;
    }

    if (!opponents.some((player) => player.playerId === targetPlayerId)) {
      setTargetPlayerId(opponents[0].playerId);
    }
  }, [opponents, targetPlayerId]);

  const offeredTotal = sumResources(offeredResources);
  const requestedTotal = sumResources(requestedResources);
  const offerHasAffordableSupplies = resources.every((resource) => offeredResources[resource] <= (me.supplies[resource] ?? 0));
  const createValidationReason = !availability.canCreatePlayerTrade
    ? availability.disabledReason
    : !targetPlayerId
      ? 'Choose another player.'
      : offeredTotal <= 0
        ? 'Select at least one Supply to offer.'
        : requestedTotal <= 0
          ? 'Select at least one Supply to request.'
          : !offerHasAffordableSupplies
            ? getOwnedSupplyError(me, offeredResources)
            : null;
  const canCreateOffer =
    !createValidationReason &&
    !!targetPlayerId &&
    offeredTotal > 0 &&
    requestedTotal > 0 &&
    offerHasAffordableSupplies;
  const relevantOffers = game.tradeOffers
    .filter((offer) => offer.proposerPlayerId === me.playerId || offer.targetPlayerId === me.playerId || offer.status === 'Pending')
    .slice()
    .sort((left, right) => right.createdAt.localeCompare(left.createdAt))
    .slice(0, 8);

  const updateSelection = (
    setter: Dispatch<SetStateAction<Record<ResourceType, number>>>,
    resource: ResourceType,
    value: number,
    max?: number
  ) => {
    const safeValue = Math.max(0, Math.min(max ?? 20, Number.isFinite(value) ? Math.floor(value) : 0));
    setter((current) => ({
      ...current,
      [resource]: safeValue
    }));
  };

  const resetSelections = () => {
    setOfferedResources(createEmptySelection());
    setRequestedResources(createEmptySelection());
  };

  const createOffer = async () => {
    try {
      await onCreateTradeOffer(
        game.roomCode,
        targetPlayerId,
        compactResources(offeredResources),
        compactResources(requestedResources)
      );
      resetSelections();
    } catch {
      resetSelections();
    }
  };

  return (
    <section className="player-trade-section" aria-label="Player-to-player trade">
      <div className="trade-section-heading">
        <div>
          <span>Player Trade</span>
          <strong>Swap Supplies with another player</strong>
        </div>
        <Handshake size={17} />
      </div>

      {availability.canCreatePlayerTrade ? <div className="player-trade-create">
        <label>
          <span>Trade with</span>
          <select value={targetPlayerId} disabled={!availability.canCreatePlayerTrade || opponents.length === 0} onChange={(event) => setTargetPlayerId(event.target.value)}>
            {opponents.map((player) => (
              <option key={player.playerId} value={player.playerId}>
                {player.playerName}
              </option>
            ))}
          </select>
        </label>

        <div className="player-trade-grid">
          <ResourceOfferGrid
            title="You offer"
            values={offeredResources}
            maxByResource={me.supplies}
            onChange={(resource, value) => updateSelection(setOfferedResources, resource, value, me.supplies[resource] ?? 0)}
          />
          <ResourceOfferGrid
            title="You request"
            values={requestedResources}
            onChange={(resource, value) => updateSelection(setRequestedResources, resource, value)}
          />
        </div>

        <button
          className="primary-button trade-action-button"
          disabled={!canCreateOffer}
          title={createValidationReason ?? undefined}
          type="button"
          onClick={() => void createOffer()}
        >
          Offer Trade
        </button>
        <p className="trade-explain">{createValidationReason ?? 'Offers exchange Supplies only. Development Cards stay private.'}</p>
      </div> : (
        <p className="empty-note player-trade-unavailable">{availability.playerTradeDisabledReason}</p>
      )}

      <div className="trade-offer-list" aria-label="Trade offers">
        {relevantOffers.length === 0 ? (
          <p className="empty-note">No player trade offers yet.</p>
        ) : (
          relevantOffers.map((offer) => {
            const offerAvailability = getTradeOfferAvailability(game, offer, me, pendingAction);
            return (
            <article className="trade-offer-card" data-status={offer.status.toLowerCase()} key={offer.tradeOfferId}>
              <div className="trade-offer-copy">
                <strong>
                  {offer.proposerPlayerId === me.playerId ? 'You' : offer.proposerName} to {offer.targetPlayerId === me.playerId ? 'you' : offer.targetName}
                </strong>
                <div className="trade-offer-assets" aria-label={`Gives ${formatResourceMap(offer.offeredResources)} for ${formatResourceMap(offer.requestedResources)}`}>
                  <ResourceMap selection={offer.offeredResources} />
                  <ArrowRightLeft aria-hidden="true" size={14} />
                  <ResourceMap selection={offer.requestedResources} />
                </div>
              </div>
              <span className="trade-status-pill">{formatStatus(offer.status)}</span>
              {offer.status === 'Pending' ? (
                <div className="trade-offer-actions">
                  {offer.canAccept ? (
                    <button type="button" disabled={!offerAvailability.canAccept} title={offerAvailability.acceptDisabledReason ?? undefined} onClick={() => void onAcceptTradeOffer(game.roomCode, offer.tradeOfferId).catch(() => undefined)}>
                      Accept
                    </button>
                  ) : null}
                  {offer.canReject ? (
                    <button type="button" disabled={!offerAvailability.canReject} onClick={() => void onRejectTradeOffer(game.roomCode, offer.tradeOfferId).catch(() => undefined)}>
                      Reject
                    </button>
                  ) : null}
                  {offer.canCancel ? (
                    <button type="button" disabled={!offerAvailability.canCancel} onClick={() => void onCancelTradeOffer(game.roomCode, offer.tradeOfferId).catch(() => undefined)}>
                      Cancel
                    </button>
                  ) : null}
                </div>
              ) : null}
            </article>
            );
          })
        )}
      </div>
    </section>
  );
}

interface ResourceOfferGridProps {
  title: string;
  values: Record<ResourceType, number>;
  maxByResource?: Record<ResourceType, number>;
  onChange: (resource: ResourceType, value: number) => void;
}

function ResourceOfferGrid({ title, values, maxByResource, onChange }: ResourceOfferGridProps) {
  return (
    <div className="player-trade-resource-box">
      <strong>{title}</strong>
      {resources.map((resource) => (
        <label aria-label={`${title} ${resource}`} key={resource} title={resource}>
          <span aria-hidden="true"><ResourceIcon decorative size="md" type={resource} /><span className="sr-only">{resource}</span></span>
          <input
            aria-label={`${title} ${resource}`}
            min="0"
            max={maxByResource?.[resource] ?? 20}
            type="number"
            value={values[resource]}
            onChange={(event) => onChange(resource, Number(event.target.value))}
          />
        </label>
      ))}
    </div>
  );
}

function ResourceMap({ selection }: { selection: Partial<Record<ResourceType, number>> }) {
  return <ResourceInlineSummary values={selection} />;
}

function createEmptySelection(): Record<ResourceType, number> {
  return resources.reduce<Record<ResourceType, number>>((selection, resource) => {
    selection[resource] = 0;
    return selection;
  }, {} as Record<ResourceType, number>);
}

function compactResources(selection: Record<ResourceType, number>): Partial<Record<ResourceType, number>> {
  return resources.reduce<Partial<Record<ResourceType, number>>>((compact, resource) => {
    if (selection[resource] > 0) {
      compact[resource] = selection[resource];
    }

    return compact;
  }, {});
}

function sumResources(selection: Partial<Record<ResourceType, number>>) {
  return resources.reduce((sum, resource) => sum + (selection[resource] ?? 0), 0);
}

function getOwnedSupplyError(me: PlayerGameState, offered: Partial<Record<ResourceType, number>>) {
  const resource = resources.find((candidate) => (offered[candidate] ?? 0) > (me.supplies[candidate] ?? 0));
  return resource ? `You only have ${me.supplies[resource] ?? 0} ${resource}.` : 'You do not have enough Supplies.';
}

function formatResourceMap(selection: Partial<Record<ResourceType, number>>) {
  const parts = resources
    .filter((resource) => (selection[resource] ?? 0) > 0)
    .map((resource) => `${selection[resource]} ${resource}`);

  return parts.length > 0 ? parts.join(', ') : 'nothing';
}

function formatStatus(status: string) {
  return status.replace(/([A-Z])/g, ' $1').trim();
}
