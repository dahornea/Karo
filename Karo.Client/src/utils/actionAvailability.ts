import type {
  BankTradeRate,
  DevelopmentCardType,
  GameState,
  PlayerDevelopmentCard,
  PlayerGameState,
  PlayerTradeOffer,
  ResourceType
} from '../types/game';
import { developmentCardCost, resources } from '../types/game';

export interface MaritimeTradeOption {
  resource: ResourceType;
  owned: number;
  required: number;
  source: string;
  canOffer: boolean;
  disabledReason: string | null;
  receiveResources: ResourceType[];
}

export interface TradeAvailability {
  canOpenTrade: boolean;
  canCreatePlayerTrade: boolean;
  canMaritimeTrade: boolean;
  disabledReason: string | null;
  playerTradeDisabledReason: string | null;
  maritimeDisabledReason: string | null;
  maritimeRatesByResource: Record<ResourceType, BankTradeRate>;
  possibleMaritimeRoutes: MaritimeTradeOption[];
  validTradeTargets: PlayerGameState[];
}

export interface CardAvailability {
  cardId: string;
  canPlay: boolean;
  disabledReason: string | null;
  boughtThisTurn: boolean;
  isPassive: boolean;
  requiresPendingActionResolution: boolean;
}

export interface DevelopmentCardAvailability {
  canOpenCards: boolean;
  canBuyCard: boolean;
  buyDisabledReason: string | null;
  deckRemainingCount: number;
  missingPurchaseResources: Partial<Record<ResourceType, number>>;
  playableCards: CardAvailability[];
  cardDisabledReasons: Record<string, string | null>;
}

export interface TradeOfferAvailability {
  canAccept: boolean;
  acceptDisabledReason: string | null;
  canReject: boolean;
  canCancel: boolean;
}

export function getTradeAvailability(
  game: GameState,
  me: PlayerGameState,
  playerId: string | null,
  pendingAction: string | null = null
): TradeAvailability {
  const isCurrentPlayer = sameId(game.currentPlayerId, playerId);
  const mandatoryReason = getMandatoryActionReason(game);
  const baseReason = getNormalActionReason(game, isCurrentPlayer, pendingAction);
  const maritimeRatesByResource = resources.reduce<Record<ResourceType, BankTradeRate>>((rates, resource) => {
    rates[resource] = me.tradeRates.find((rate) => rate.resource === resource) ?? {
      resource,
      rate: 4,
      source: 'DefaultBank',
      portId: null
    };
    return rates;
  }, {} as Record<ResourceType, BankTradeRate>);
  const possibleMaritimeRoutes = resources.map((resource): MaritimeTradeOption => {
    const rate = maritimeRatesByResource[resource];
    const owned = me.supplies[resource] ?? 0;
    const canOffer = !baseReason && owned >= rate.rate;

    return {
      resource,
      owned,
      required: rate.rate,
      source: tradeRateSourceLabel(rate, resource),
      canOffer,
      disabledReason: baseReason ?? (owned < rate.rate ? `Need ${rate.rate - owned} more ${resource}.` : null),
      receiveResources: resources.filter((candidate) => candidate !== resource)
    };
  });
  const validTradeTargets = game.players.filter((player) => !sameId(player.playerId, me.playerId));
  const hasSuppliesToOffer = resources.some((resource) => (me.supplies[resource] ?? 0) > 0);
  const canMaritimeTrade = possibleMaritimeRoutes.some((route) => route.canOffer && route.receiveResources.length > 0);
  const playerTradeReason = baseReason
    ?? (validTradeTargets.length === 0
      ? 'There are no other players to trade with.'
      : !hasSuppliesToOffer
        ? 'You do not have any Supplies to offer.'
        : null);
  const canCreatePlayerTrade = !playerTradeReason;
  const maritimeDisabledReason = canMaritimeTrade
    ? null
    : baseReason ?? 'You do not have enough matching Supplies for a maritime trade.';
  const hasActionableOffer = game.tradeOffers.some((offer) => {
    const availability = getTradeOfferAvailability(game, offer, me, pendingAction);
    return availability.canAccept || availability.canReject || availability.canCancel;
  });
  const canOpenTrade = canMaritimeTrade || canCreatePlayerTrade || hasActionableOffer;
  const disabledReason = canOpenTrade
    ? null
    : mandatoryReason
      ?? baseReason
      ?? (validTradeTargets.length === 0
        ? 'There are no other players to trade with.'
        : !hasSuppliesToOffer
          ? 'You do not have any Supplies to offer.'
          : 'You do not have enough matching Supplies for a maritime trade.');

  return {
    canOpenTrade,
    canCreatePlayerTrade,
    canMaritimeTrade,
    disabledReason,
    playerTradeDisabledReason: playerTradeReason,
    maritimeDisabledReason,
    maritimeRatesByResource,
    possibleMaritimeRoutes,
    validTradeTargets
  };
}

export function getTradeOfferAvailability(
  game: GameState,
  offer: PlayerTradeOffer,
  me: PlayerGameState,
  pendingAction: string | null = null
): TradeOfferAvailability {
  const target = game.players.find((player) => sameId(player.playerId, offer.targetPlayerId));
  const offerIsCurrent = offer.status === 'Pending'
    && game.status === 'InProgress'
    && game.phase === 'NormalTurn'
    && game.hasRolledThisTurn
    && !getMandatoryActionReason(game)
    && offer.turnNumber === game.turnNumber
    && sameId(game.currentPlayerId, offer.proposerPlayerId);
  const targetCanPay = !!target && hasSupplies(target, offer.requestedResources);
  const isTarget = sameId(me.playerId, offer.targetPlayerId);
  const isProposer = sameId(me.playerId, offer.proposerPlayerId);
  const canAccept = !!offer.canAccept && offerIsCurrent && isTarget && targetCanPay && !pendingAction;
  const acceptDisabledReason = canAccept
    ? null
    : !offerIsCurrent
      ? 'This trade can no longer be completed.'
      : !targetCanPay && isTarget
        ? 'You no longer have the requested Supplies.'
        : pendingAction
            ? 'Another action is resolving.'
            : 'This trade can no longer be completed.';

  return {
    canAccept,
    acceptDisabledReason,
    canReject: !!offer.canReject && offerIsCurrent && isTarget && !pendingAction,
    canCancel: !!offer.canCancel && offerIsCurrent && isProposer && !pendingAction
  };
}

export function getDevelopmentCardAvailability(
  game: GameState,
  me: PlayerGameState,
  playerId: string | null,
  pendingAction: string | null = null
): DevelopmentCardAvailability {
  const isCurrentPlayer = sameId(game.currentPlayerId, playerId);
  const missingPurchaseResources = resources.reduce<Partial<Record<ResourceType, number>>>((missing, resource) => {
    const required = developmentCardCost[resource] ?? 0;
    const deficit = Math.max(0, required - (me.supplies[resource] ?? 0));
    if (deficit > 0) {
      missing[resource] = deficit;
    }
    return missing;
  }, {});
  const buyDisabledReason = getDevelopmentCardBuyReason(
    game,
    isCurrentPlayer,
    missingPurchaseResources,
    pendingAction
  );
  const playableCards = me.developmentCards.map((card) => getCardAvailability(game, me, card, isCurrentPlayer, pendingAction));
  const cardDisabledReasons = playableCards.reduce<Record<string, string | null>>((reasons, card) => {
    reasons[card.cardId] = card.disabledReason;
    return reasons;
  }, {});

  return {
    canOpenCards: game.phase !== 'Setup'
      && (me.developmentCards.length > 0 || (isCurrentPlayer && game.hasRolledThisTurn)),
    canBuyCard: !buyDisabledReason,
    buyDisabledReason,
    deckRemainingCount: game.developmentDeckCount,
    missingPurchaseResources,
    playableCards,
    cardDisabledReasons
  };
}

export function getCardAvailability(
  game: GameState,
  me: PlayerGameState,
  card: PlayerDevelopmentCard,
  isCurrentPlayer: boolean,
  pendingAction: string | null = null
): CardAvailability {
  const type = card.type ?? 'VictoryPoint';
  const isPassive = type === 'VictoryPoint';
  const boughtThisTurn = card.status === 'BoughtThisTurn' || card.purchasedTurn === game.turnNumber;
  const pendingReason = getMandatoryActionReason(game) ?? (pendingAction ? 'Resolve current action first' : null);
  let disabledReason: string | null = null;

  if (isPassive) {
    disabledReason = 'Passive Victory Point';
  } else if (game.status === 'Finished' || game.phase === 'Finished') {
    disabledReason = 'Match finished';
  } else if (game.phase === 'Setup') {
    disabledReason = 'Unavailable during setup';
  } else if (!isCurrentPlayer) {
    disabledReason = 'Not your turn';
  } else if (pendingReason) {
    disabledReason = 'Resolve current action first';
  } else if (card.isPlayed || card.status === 'AlreadyPlayed') {
    disabledReason = 'Already played';
  } else if (boughtThisTurn) {
    disabledReason = 'Bought this turn';
  } else if (me.hasPlayedDevelopmentCardThisTurn) {
    disabledReason = 'Card limit used';
  } else if (type === 'RoadBuilding' && me.remainingTrails <= 0) {
    disabledReason = 'No Trail pieces remaining';
  } else if (type === 'RoadBuilding' && !hasLegalTrailPlacement(game, me.playerId)) {
    disabledReason = 'No legal Trail placement';
  } else if (type === 'Knight' && !game.board.tiles.some((tile) => tile.tileId !== game.wardenTileId)) {
    disabledReason = 'No valid Warden move';
  }

  return {
    cardId: card.cardId,
    canPlay: !disabledReason,
    disabledReason,
    boughtThisTurn,
    isPassive,
    requiresPendingActionResolution: !!pendingReason
  };
}

export function formatMissingDevelopmentCardResources(missing: Partial<Record<ResourceType, number>>) {
  const parts = resources
    .filter((resource) => (missing[resource] ?? 0) > 0)
    .map((resource) => `${missing[resource]} ${resource}`);

  if (parts.length === 0) {
    return null;
  }

  return `Missing ${joinWithAnd(parts)}.`;
}

export function tradeRateSourceLabel(rate: BankTradeRate, resource: ResourceType) {
  if (rate.source === 'SpecificPort') {
    return `${resource} harbor`;
  }

  if (rate.source === 'GenericPort') {
    return 'Generic harbor';
  }

  return 'Default bank';
}

function getDevelopmentCardBuyReason(
  game: GameState,
  isCurrentPlayer: boolean,
  missing: Partial<Record<ResourceType, number>>,
  pendingAction: string | null
) {
  if (game.status === 'Finished' || game.phase === 'Finished') {
    return 'The match is finished.';
  }

  if (game.phase === 'Setup') {
    return 'Development Cards are unavailable during setup.';
  }

  if (!isCurrentPlayer) {
    return 'It is not your turn.';
  }

  if (getMandatoryActionReason(game) || pendingAction) {
    return 'Resolve the current action first.';
  }

  if (!game.hasRolledThisTurn) {
    return 'Roll before buying a Development Card.';
  }

  if (game.developmentDeckCount <= 0) {
    return 'The Development Card deck is empty.';
  }

  return formatMissingDevelopmentCardResources(missing);
}

function getNormalActionReason(game: GameState, isCurrentPlayer: boolean, pendingAction: string | null) {
  if (game.status === 'Finished' || game.phase === 'Finished') {
    return 'The match is finished.';
  }

  if (game.phase === 'Setup') {
    return 'Trading is unavailable during setup.';
  }

  if (!isCurrentPlayer) {
    return 'It is not your turn.';
  }

  if (getMandatoryActionReason(game) || pendingAction) {
    return 'Resolve the current action before trading.';
  }

  if (!game.hasRolledThisTurn) {
    return 'Roll before creating a trade offer.';
  }

  return null;
}

function getMandatoryActionReason(game: GameState) {
  return game.pendingWardenAction !== 'None' || !!game.activeDevelopmentCardEffect
    ? 'Resolve the current action first.'
    : null;
}

function hasLegalTrailPlacement(game: GameState, playerId: string) {
  return game.board.edges.some((edge) => {
    if (edge.ownerPlayerId) {
      return false;
    }

    return canConnectTrailAtVertex(game, playerId, edge.startVertexId)
      || canConnectTrailAtVertex(game, playerId, edge.endVertexId);
  });
}

function canConnectTrailAtVertex(game: GameState, playerId: string, vertexId: string) {
  const vertex = game.board.vertices.find((candidate) => candidate.vertexId === vertexId);
  if (vertex?.ownerPlayerId && vertex.structureType) {
    return sameId(vertex.ownerPlayerId, playerId);
  }

  return game.board.edges.some((edge) => (
    (edge.startVertexId === vertexId || edge.endVertexId === vertexId)
    && sameId(edge.ownerPlayerId, playerId)
  ));
}

function hasSupplies(player: PlayerGameState, required: Partial<Record<ResourceType, number>>) {
  return resources.every((resource) => (player.supplies[resource] ?? 0) >= (required[resource] ?? 0));
}

function joinWithAnd(parts: string[]) {
  if (parts.length <= 1) {
    return parts[0] ?? '';
  }

  return `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`;
}

function sameId(left: string | null | undefined, right: string | null | undefined) {
  return Boolean(left && right && left.toLocaleLowerCase() === right.toLocaleLowerCase());
}
