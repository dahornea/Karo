import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import ts from 'typescript';

const __dirname = dirname(fileURLToPath(import.meta.url));
const resources = ['Wood', 'Clay', 'Wool', 'Grain', 'Stone'];
const developmentCardCost = { Wool: 1, Grain: 1, Stone: 1 };
const sourcePath = resolve(__dirname, '../src/utils/actionAvailability.ts');
const source = readFileSync(sourcePath, 'utf8');
const compiled = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2020,
    importsNotUsedAsValues: ts.ImportsNotUsedAsValues.Remove
  }
}).outputText;
const sandbox = {
  exports: {},
  module: { exports: {} },
  require: (request) => request.endsWith('/types/game') || request.endsWith('types/game')
    ? { resources, developmentCardCost }
    : (() => { throw new Error(`Unexpected dependency: ${request}`); })()
};
sandbox.exports = sandbox.module.exports;
vm.runInNewContext(compiled, sandbox, { filename: 'actionAvailability.ts' });

const {
  formatMissingDevelopmentCardResources,
  getDevelopmentCardAvailability,
  getTradeAvailability,
  getTradeOfferAvailability
} = sandbox.module.exports;

function supplies(overrides = {}) {
  return { Wood: 0, Clay: 0, Wool: 0, Grain: 0, Stone: 0, ...overrides };
}

function player(overrides = {}) {
  return {
    playerId: 'p1',
    supplies: supplies(),
    tradeRates: resources.map((resource) => ({ resource, rate: 4, source: 'DefaultBank', portId: null })),
    developmentCards: [],
    developmentCardCount: 0,
    hasPlayedDevelopmentCardThisTurn: false,
    remainingTrails: 15,
    ...overrides
  };
}

function game(overrides = {}) {
  const me = overrides.me ?? player();
  const opponent = overrides.opponent ?? player({ playerId: 'p2' });
  return {
    status: 'InProgress',
    phase: 'NormalTurn',
    currentPlayerId: 'p1',
    hasRolledThisTurn: true,
    pendingWardenAction: 'None',
    activeDevelopmentCardEffect: null,
    developmentDeckCount: 25,
    turnNumber: 3,
    wardenTileId: 't1',
    players: [me, opponent],
    tradeOffers: [],
    board: {
      tiles: [{ tileId: 't1' }, { tileId: 't2' }],
      vertices: [{ vertexId: 'v1', ownerPlayerId: 'p1', structureType: 'Camp' }, { vertexId: 'v2', ownerPlayerId: null, structureType: null }],
      edges: [{ edgeId: 'e1', startVertexId: 'v1', endVertexId: 'v2', ownerPlayerId: null }]
    },
    ...overrides,
    me: undefined,
    opponent: undefined
  };
}

function card(type, overrides = {}) {
  return {
    cardId: `${type}-1`,
    type,
    purchasedTurn: 1,
    isPlayed: false,
    status: type === 'VictoryPoint' ? 'HiddenVictoryPoint' : 'Playable',
    ...overrides
  };
}

function assert(condition, label) {
  if (!condition) throw new Error(label);
}

function assertEqual(actual, expected, label) {
  if (actual !== expected) throw new Error(`${label}\nExpected: ${expected}\nActual:   ${actual}`);
}

let count = 0;
function test(label, action) {
  action();
  count++;
}

test('trade is unavailable before rolling', () => {
  const me = player({ supplies: supplies({ Wood: 4 }) });
  const result = getTradeAvailability(game({ me, hasRolledThisTurn: false }), me, 'p1');
  assert(!result.canOpenTrade, 'Trade should be locked before rolling.');
});

test('default bank requires four matching Supplies', () => {
  const me = player({ supplies: supplies({ Wood: 3 }) });
  const route = getTradeAvailability(game({ me }), me, 'p1').possibleMaritimeRoutes[0];
  assertEqual(route.required, 4, 'Default Wood rate');
  assert(!route.canOffer, 'Three Wood should not afford 4:1.');
});

test('generic harbor enables 3:1', () => {
  const me = player({
    supplies: supplies({ Clay: 3 }),
    tradeRates: resources.map((resource) => ({ resource, rate: 3, source: 'GenericPort', portId: 'h1' }))
  });
  const route = getTradeAvailability(game({ me }), me, 'p1').possibleMaritimeRoutes.find((item) => item.resource === 'Clay');
  assert(route.canOffer && route.required === 3 && route.source === 'Generic harbor', 'Generic harbor route should be valid.');
});

test('specific harbor applies only to its resource', () => {
  const me = player({
    supplies: supplies({ Wood: 2, Clay: 2 }),
    tradeRates: resources.map((resource) => ({
      resource,
      rate: resource === 'Wood' ? 2 : 4,
      source: resource === 'Wood' ? 'SpecificPort' : 'DefaultBank',
      portId: resource === 'Wood' ? 'h2' : null
    }))
  });
  const result = getTradeAvailability(game({ me }), me, 'p1');
  assert(result.possibleMaritimeRoutes.find((item) => item.resource === 'Wood').canOffer, 'Wood should afford 2:1.');
  assert(!result.possibleMaritimeRoutes.find((item) => item.resource === 'Clay').canOffer, 'Clay should still require 4.');
});

test('zero Supplies prevents player trade creation', () => {
  const me = player();
  const result = getTradeAvailability(game({ me }), me, 'p1');
  assert(!result.canCreatePlayerTrade, 'Empty proposer should not create a trade.');
  assertEqual(result.disabledReason, 'You do not have any Supplies to offer.', 'Empty proposer reason');
});

test('non-current player cannot create an offer', () => {
  const me = player({ supplies: supplies({ Wood: 1 }) });
  assert(!getTradeAvailability(game({ me, currentPlayerId: 'p2' }), me, 'p1').canCreatePlayerTrade, 'Only current player can offer.');
});

test('stale recipient inventory disables Accept', () => {
  const me = player({ playerId: 'p2', supplies: supplies() });
  const proposer = player({ supplies: supplies({ Wood: 1 }) });
  const offer = {
    tradeOfferId: 'o1', status: 'Pending', turnNumber: 3, proposerPlayerId: 'p1', targetPlayerId: 'p2',
    offeredResources: { Wood: 1 }, requestedResources: { Grain: 1 }, canAccept: true, canReject: true, canCancel: false
  };
  const result = getTradeOfferAvailability(game({ me: proposer, opponent: me, tradeOffers: [offer] }), offer, me);
  assert(!result.canAccept, 'Accept must disable after recipient Supplies change.');
  assertEqual(result.acceptDisabledReason, 'You no longer have the requested Supplies.', 'Stale recipient reason');
});

test('current payable incoming offer enables Accept', () => {
  const me = player({ playerId: 'p2', supplies: supplies({ Grain: 1 }) });
  const proposer = player({ supplies: supplies({ Wood: 1 }) });
  const offer = {
    tradeOfferId: 'o2', status: 'Pending', turnNumber: 3, proposerPlayerId: 'p1', targetPlayerId: 'p2',
    offeredResources: { Wood: 1 }, requestedResources: { Grain: 1 }, canAccept: true, canReject: true, canCancel: false
  };
  const result = getTradeOfferAvailability(game({ me: proposer, opponent: me, tradeOffers: [offer] }), offer, me);
  assert(result.canAccept, 'Payable current offer should enable Accept.');
});

test('buy is disabled before rolling', () => {
  const me = player({ supplies: supplies({ Wool: 1, Grain: 1, Stone: 1 }) });
  const result = getDevelopmentCardAvailability(game({ me, hasRolledThisTurn: false }), me, 'p1');
  assertEqual(result.buyDisabledReason, 'Roll before buying a Development Card.', 'Pre-roll buy reason');
});

test('missing Development Card resources are exact', () => {
  const me = player({ supplies: supplies({ Wool: 1 }) });
  const result = getDevelopmentCardAvailability(game({ me }), me, 'p1');
  assertEqual(formatMissingDevelopmentCardResources(result.missingPurchaseResources), 'Missing 1 Grain and 1 Stone.', 'Missing purchase Supplies');
});

test('buy is disabled for an empty deck', () => {
  const me = player({ supplies: supplies({ Wool: 1, Grain: 1, Stone: 1 }) });
  const result = getDevelopmentCardAvailability(game({ me, developmentDeckCount: 0 }), me, 'p1');
  assertEqual(result.buyDisabledReason, 'The Development Card deck is empty.', 'Empty deck reason');
});

test('buy is enabled with all requirements', () => {
  const me = player({ supplies: supplies({ Wool: 1, Grain: 1, Stone: 1 }) });
  assert(getDevelopmentCardAvailability(game({ me }), me, 'p1').canBuyCard, 'Affordable card should be purchasable.');
});

test('card bought this turn is disabled', () => {
  const owned = card('Knight', { purchasedTurn: 3, status: 'BoughtThisTurn' });
  const me = player({ developmentCards: [owned], developmentCardCount: 1 });
  const availability = getDevelopmentCardAvailability(game({ me }), me, 'p1').playableCards[0];
  assertEqual(availability.disabledReason, 'Bought this turn', 'Same-turn card reason');
});

test('card limit disables another action card', () => {
  const owned = card('Monopoly');
  const me = player({ developmentCards: [owned], developmentCardCount: 1, hasPlayedDevelopmentCardThisTurn: true });
  assertEqual(getDevelopmentCardAvailability(game({ me }), me, 'p1').playableCards[0].disabledReason, 'Card limit used', 'Card limit reason');
});

test('eligible action card can play before and after rolling', () => {
  const owned = card('YearOfPlenty');
  const me = player({ developmentCards: [owned], developmentCardCount: 1 });
  assert(getDevelopmentCardAvailability(game({ me, hasRolledThisTurn: false }), me, 'p1').playableCards[0].canPlay, 'Pre-roll card should play.');
  assert(getDevelopmentCardAvailability(game({ me, hasRolledThisTurn: true }), me, 'p1').playableCards[0].canPlay, 'Post-roll card should play.');
});

test('Victory Point is passive', () => {
  const owned = card('VictoryPoint');
  const me = player({ developmentCards: [owned], developmentCardCount: 1 });
  const availability = getDevelopmentCardAvailability(game({ me }), me, 'p1').playableCards[0];
  assert(availability.isPassive && !availability.canPlay, 'Victory Point must not expose Play.');
});

test('Road Building requires a physical Trail', () => {
  const owned = card('RoadBuilding');
  const me = player({ developmentCards: [owned], developmentCardCount: 1, remainingTrails: 0 });
  assertEqual(getDevelopmentCardAvailability(game({ me }), me, 'p1').playableCards[0].disabledReason, 'No Trail pieces remaining', 'Road Building piece reason');
});

test('Road Building requires a legal edge', () => {
  const owned = card('RoadBuilding');
  const me = player({ developmentCards: [owned], developmentCardCount: 1 });
  const board = { tiles: [{ tileId: 't1' }, { tileId: 't2' }], vertices: [], edges: [{ edgeId: 'e1', startVertexId: 'x', endVertexId: 'y', ownerPlayerId: null }] };
  assertEqual(getDevelopmentCardAvailability(game({ me, board }), me, 'p1').playableCards[0].disabledReason, 'No legal Trail placement', 'Road Building placement reason');
});

test('pending mandatory action disables cards', () => {
  const owned = card('Knight');
  const me = player({ developmentCards: [owned], developmentCardCount: 1 });
  assertEqual(getDevelopmentCardAvailability(game({ me, pendingWardenAction: 'MoveWarden' }), me, 'p1').playableCards[0].disabledReason, 'Resolve current action first', 'Mandatory action reason');
});

console.log(`action availability tests passed (${count})`);
