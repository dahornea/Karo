import { Anchor, ArrowRightLeft } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { BankTradeRate, GameState, PlayerGameState, ResourceType } from '../types/game';
import { resources } from '../types/game';

interface BankTradePanelProps {
  game: GameState;
  me: PlayerGameState;
  actionsUnlocked: boolean;
  isMyTurn: boolean;
  pendingAction: string | null;
  surface?: 'card' | 'tray';
  onTradeWithBank: (roomCode: string, offeredResource: ResourceType, requestedResource: ResourceType) => Promise<void>;
}

export function BankTradePanel({
  game,
  me,
  actionsUnlocked,
  isMyTurn,
  pendingAction,
  surface = 'card',
  onTradeWithBank
}: BankTradePanelProps) {
  const [offeredResource, setOfferedResource] = useState<ResourceType>('Wood');
  const [requestedResource, setRequestedResource] = useState<ResourceType>('Clay');

  useEffect(() => {
    if (offeredResource === requestedResource) {
      setRequestedResource(resources.find((resource) => resource !== offeredResource) ?? 'Wood');
    }
  }, [offeredResource, requestedResource]);

  const ratesByResource = useMemo(() => {
    return resources.reduce<Record<ResourceType, BankTradeRate>>((rates, resource) => {
      rates[resource] = me.tradeRates.find((rate) => rate.resource === resource) ?? {
        resource,
        rate: 4,
        source: 'DefaultBank',
        portId: null
      };
      return rates;
    }, {} as Record<ResourceType, BankTradeRate>);
  }, [me.tradeRates]);

  const currentRate = ratesByResource[offeredResource];
  const offeredAmount = me.supplies[offeredResource] ?? 0;
  const isSameResource = offeredResource === requestedResource;
  const accessibleHarborCount = me.accessibleHarborSlotIds?.length ?? me.accessiblePortIds.length;
  const tradeLockReason = getTradeLockReason(game, actionsUnlocked, isMyTurn, pendingAction);
  const canTrade =
    isMyTurn &&
    actionsUnlocked &&
    !pendingAction &&
    !isSameResource &&
    offeredAmount >= currentRate.rate;

  return (
    <section className={`${surface === 'tray' ? 'tray-section' : 'game-side-card'} bank-trade-card`}>
      <div className="panel-heading">
        <h2>Maritime Trade</h2>
        <Anchor size={16} />
      </div>

      <div className="trade-summary-card">
        <span>Selected route</span>
        <strong>{currentRate.rate}:1 {offeredResource}</strong>
        <small>{sourceLabel(currentRate, offeredResource)}</small>
      </div>

      <div className="trade-rate-list" aria-label="Available maritime trade rates">
        {resources.map((resource) => {
          const tradeRate = ratesByResource[resource];
          const available = me.supplies[resource] ?? 0;
          const isAffordable = available >= tradeRate.rate;

          return (
            <button
              className="trade-rate-row"
              data-active={resource === offeredResource}
              data-affordable={isAffordable}
              disabled={!isMyTurn || !actionsUnlocked || !!pendingAction || !isAffordable}
              key={resource}
              title={rateTooltip(tradeRate, resource)}
              type="button"
              onClick={() => setOfferedResource(resource)}
            >
              <span>
                <strong>{resource}</strong>
                <small>{sourceLabel(tradeRate, resource)}</small>
              </span>
              <b>{tradeRate.rate}:1</b>
            </button>
          );
        })}
      </div>

      <div className="bank-trade-controls">
        <label>
          <span>Give</span>
          <select value={offeredResource} onChange={(event) => setOfferedResource(event.target.value as ResourceType)}>
            {resources.map((resource) => (
              <option key={resource}>{resource}</option>
            ))}
          </select>
        </label>

        <ArrowRightLeft aria-hidden="true" size={18} />

        <label>
          <span>Receive</span>
          <select value={requestedResource} onChange={(event) => setRequestedResource(event.target.value as ResourceType)}>
            {resources.map((resource) => (
              <option disabled={resource === offeredResource} key={resource}>
                {resource}
              </option>
            ))}
          </select>
        </label>
      </div>

      <button
        className="primary-button trade-action-button"
        disabled={!canTrade}
        type="button"
        onClick={() => void onTradeWithBank(game.roomCode, offeredResource, requestedResource)}
      >
        Trade {currentRate.rate} {offeredResource} for 1 {isSameResource ? 'resource' : requestedResource}
      </button>

      <p className="trade-explain">
        {tradeLockReason ? `${tradeLockReason} ` : ''}
        {rateTooltip(currentRate, offeredResource)}
        {accessibleHarborCount > 0 ? ` You have access to ${accessibleHarborCount} harbor${accessibleHarborCount === 1 ? '' : 's'}.` : ''}
      </p>
    </section>
  );
}

function getTradeLockReason(
  game: GameState,
  actionsUnlocked: boolean,
  isMyTurn: boolean,
  pendingAction: string | null
) {
  if (!isMyTurn) {
    return 'Waiting for your turn.';
  }

  if (game.phase === 'Setup') {
    return 'Trading is not available during setup.';
  }

  if (game.pendingWardenAction !== 'None') {
    return 'Resolve the Warden before trading.';
  }

  if (!game.hasRolledThisTurn || !actionsUnlocked) {
    return 'Roll dice before trading.';
  }

  if (pendingAction) {
    return 'Another action is resolving.';
  }

  return null;
}

function sourceLabel(tradeRate: BankTradeRate, resource: ResourceType) {
  if (tradeRate.source === 'SpecificPort') {
    return `${resource} harbor`;
  }

  if (tradeRate.source === 'GenericPort') {
    return 'Generic harbor';
  }

  return 'Default bank';
}

function rateTooltip(tradeRate: BankTradeRate, resource: ResourceType) {
  if (tradeRate.source === 'SpecificPort') {
    return `${resource} harbor - Trade 2 ${resource} for 1 resource.`;
  }

  if (tradeRate.source === 'GenericPort') {
    return `Generic harbor - Trade any 3 identical resources for 1 resource.`;
  }

  return `Default bank trade - Trade 4 ${resource} for 1 resource.`;
}
