import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import ts from 'typescript';

const __dirname = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(__dirname, '../src/utils/commandDock.ts');
const availabilityPath = resolve(__dirname, '../src/utils/actionAvailability.ts');
const resources = ['Wood', 'Clay', 'Wool', 'Grain', 'Stone'];
const developmentCardCost = { Wool: 1, Grain: 1, Stone: 1 };
const availabilitySource = readFileSync(availabilityPath, 'utf8');
const availabilityCompiled = ts.transpileModule(availabilitySource, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2020,
    importsNotUsedAsValues: ts.ImportsNotUsedAsValues.Remove
  }
}).outputText;
const availabilitySandbox = {
  exports: {},
  module: { exports: {} },
  require: () => ({ resources, developmentCardCost })
};
availabilitySandbox.exports = availabilitySandbox.module.exports;
vm.runInNewContext(availabilityCompiled, availabilitySandbox, { filename: 'actionAvailability.ts' });
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
  require: (request) => request.includes('actionAvailability')
    ? availabilitySandbox.module.exports
    : (() => { throw new Error(`Unexpected dependency: ${request}`); })()
};
sandbox.exports = sandbox.module.exports;
vm.runInNewContext(compiled, sandbox, { filename: 'commandDock.ts' });

const { getVisibleCommandDockActions } = sandbox.module.exports;

function player(overrides = {}) {
  return {
    playerId: 'p1',
    supplies: { Wood: 0, Clay: 0, Wool: 0, Grain: 0, Stone: 0 },
    tradeRates: resources.map((resource) => ({ resource, rate: 4, source: 'DefaultBank', portId: null })),
    developmentCards: [],
    developmentCardCount: 0,
    hasPlayedDevelopmentCardThisTurn: false,
    remainingTrails: 15,
    ...overrides
  };
}

function game(overrides = {}) {
  return {
    status: 'InProgress',
    phase: 'NormalTurn',
    currentPlayerId: 'p1',
    hasRolledThisTurn: false,
    pendingWardenAction: 'None',
    activeDevelopmentCardEffect: null,
    developmentDeckCount: 25,
    turnNumber: 1,
    wardenTileId: 't1',
    players: [player(), player({ playerId: 'p2' })],
    tradeOffers: [],
    board: { tiles: [{ tileId: 't1' }, { tileId: 't2' }], vertices: [], edges: [] },
    ...overrides
  };
}

function actionIds(result) {
  return result.map((action) => action.id);
}

function assertDeepEqual(actual, expected, label) {
  const actualJson = JSON.stringify(actual);
  const expectedJson = JSON.stringify(expected);

  if (actualJson !== expectedJson) {
    throw new Error(`${label}\nExpected: ${expectedJson}\nActual:   ${actualJson}`);
  }
}

const playableCard = {
  cardId: 'c1',
  type: 'Knight',
  purchasedTurn: 1,
  isPlayed: false,
  status: 'Playable'
};

const cases = [
  {
    label: 'setup shows only Game Log',
    game: game({ phase: 'Setup', currentPlayerId: '' }),
    me: player({ supplies: { Wood: 4, Clay: 0, Wool: 0, Grain: 0, Stone: 0 } }),
    actionsUnlocked: false,
    expected: ['log']
  },
  {
    label: 'pre-roll without playable card shows only Game Log',
    game: game(),
    me: player(),
    actionsUnlocked: false,
    expected: ['log']
  },
  {
    label: 'pre-roll with playable card shows Cards and Game Log',
    game: game(),
    me: player({ developmentCards: [playableCard], developmentCardCount: 1 }),
    actionsUnlocked: false,
    expected: ['cards', 'log']
  },
  {
    label: 'after rolling shows Trade, Cards, and Game Log without a Build drawer',
    game: game({ hasRolledThisTurn: true }),
    me: player({ supplies: { Wood: 4, Clay: 0, Wool: 0, Grain: 0, Stone: 0 } }),
    actionsUnlocked: true,
    expected: ['trade', 'cards', 'log']
  },
  {
    label: 'Warden flow hides incompatible controls',
    game: game({ hasRolledThisTurn: true, pendingWardenAction: 'MoveWarden' }),
    me: player(),
    actionsUnlocked: false,
    expected: ['log']
  },
  {
    label: 'active Development Card effect hides incompatible controls',
    game: game({ hasRolledThisTurn: true, activeDevelopmentCardEffect: { type: 'RoadBuilding', cardId: 'c2', freeTrailsPlaced: 0, maxFreeTrails: 2 } }),
    me: player(),
    actionsUnlocked: false,
    expected: ['log']
  },
  {
    label: 'finished match shows only Game Log',
    game: game({ status: 'Finished', phase: 'Finished', hasRolledThisTurn: true }),
    me: player(),
    actionsUnlocked: false,
    expected: ['log']
  }
];

for (const testCase of cases) {
  const result = getVisibleCommandDockActions(testCase.game, testCase.me, 'p1', testCase.actionsUnlocked);
  assertDeepEqual(actionIds(result), testCase.expected, testCase.label);
}

console.log(`command dock tests passed (${cases.length})`);
