import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import ts from 'typescript';

const __dirname = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(__dirname, '../src/utils/directBuild.ts');
const resources = ['Wood', 'Clay', 'Wool', 'Grain', 'Stone'];
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
  require: (request) => request.includes('types/game')
    ? { resources }
    : (() => { throw new Error(`Unexpected dependency: ${request}`); })()
};
sandbox.exports = sandbox.module.exports;
vm.runInNewContext(compiled, sandbox, { filename: 'directBuild.ts' });

const {
  getDirectBuildAvailability,
  getFreeTrailTargetIds,
  getSetupCampTargetIds,
  getSetupTrailTargetIds,
  isDirectBuildSelectionAvailable
} = sandbox.module.exports;

function player(overrides = {}) {
  return {
    playerId: 'p1',
    supplies: { Wood: 3, Clay: 3, Wool: 2, Grain: 4, Stone: 4 },
    remainingTrails: 12,
    remainingCamps: 4,
    remainingStrongholds: 4,
    ...overrides
  };
}

function board() {
  return {
    tiles: [],
    harborSlots: [],
    ports: [],
    vertices: [
      vertex('v1', 'p1', 'Camp'),
      vertex('v2'),
      vertex('v3'),
      vertex('v4'),
      vertex('v5', 'p2', 'Camp'),
      vertex('v6')
    ],
    edges: [
      edge('e-owned-a', 'v1', 'v2', 'p1'),
      edge('e-trail-target', 'v2', 'v3'),
      edge('e-owned-b', 'v3', 'v4', 'p1'),
      edge('e-block-test', 'v2', 'v6')
    ]
  };
}

function game(overrides = {}) {
  return {
    status: 'InProgress',
    phase: 'NormalTurn',
    currentPlayerId: 'p1',
    hasRolledThisTurn: true,
    pendingWardenAction: 'None',
    activeDevelopmentCardEffect: null,
    players: [player(), player({ playerId: 'p2' })],
    board: board(),
    ...overrides
  };
}

function vertex(vertexId, ownerPlayerId = null, structureType = null) {
  return { vertexId, ownerPlayerId, structureType, adjacentTileIds: [] };
}

function edge(edgeId, startVertexId, endVertexId, ownerPlayerId = null) {
  return { edgeId, startVertexId, endVertexId, ownerPlayerId };
}

function action(state, type) {
  return state.actions[type];
}

function includes(values, value, message) {
  assert(values.includes(value), message);
}

function excludes(values, value, message) {
  assert(!values.includes(value), message);
}

let assertions = 0;
const available = getDirectBuildAvailability(game(), 'p1');
includes(action(available, 'Trail').actionableTargetIds, 'e-trail-target', 'Affordable connected Trail edge should be actionable.');
includes(action(available, 'Camp').actionableTargetIds, 'v4', 'Affordable connected Camp node should be actionable.');
includes(action(available, 'Stronghold').actionableTargetIds, 'v1', 'Own Camp should be an actionable Stronghold target.');
excludes(action(available, 'Stronghold').actionableTargetIds, 'v5', 'Opponent Camps must never be upgrade targets.');
assert(isDirectBuildSelectionAvailable(available, { type: 'Trail', targetId: 'e-trail-target' }), 'Selected legal Trail should remain confirmable.');

const poorPlayer = player({ supplies: { Wood: 0, Clay: 0, Wool: 0, Grain: 0, Stone: 0 } });
const poor = getDirectBuildAvailability(game({ players: [poorPlayer, player({ playerId: 'p2' })] }), 'p1');
assert(action(poor, 'Trail').legalTargetIds.length > 0 && action(poor, 'Trail').actionableTargetIds.length === 0, 'Unaffordable Trail targets must not be clickable.');
assert(action(poor, 'Camp').actionableTargetIds.length === 0, 'Unaffordable Camp targets must not be clickable.');
assert(action(poor, 'Stronghold').actionableTargetIds.length === 0, 'Unaffordable Stronghold targets must not be clickable.');

for (const blockedGame of [
  game({ hasRolledThisTurn: false }),
  game({ currentPlayerId: 'p2' }),
  game({ pendingWardenAction: 'MoveWarden' }),
  game({ activeDevelopmentCardEffect: { type: 'RoadBuilding', freeTrailsPlaced: 0, maxFreeTrails: 2 } }),
  game({ phase: 'Setup' })
]) {
  assert(!getDirectBuildAvailability(blockedGame, 'p1').hasAnyAction, 'Turn, roll, phase, and mandatory-action gates must suppress paid targets.');
}
assert(!getDirectBuildAvailability(game(), 'p1', 'BuildTrail').hasAnyAction, 'Pending actions must suppress paid targets.');

const noPieces = getDirectBuildAvailability(game({
  players: [player({ remainingTrails: 0, remainingCamps: 0, remainingStrongholds: 0 }), player({ playerId: 'p2' })]
}), 'p1');
assert(!noPieces.hasAnyAction, 'Exhausted physical piece supplies must suppress construction targets.');

const blockedNetworkGame = game();
blockedNetworkGame.board.vertices.find((candidate) => candidate.vertexId === 'v2').ownerPlayerId = 'p2';
blockedNetworkGame.board.vertices.find((candidate) => candidate.vertexId === 'v2').structureType = 'Camp';
const blockedNetwork = getDirectBuildAvailability(blockedNetworkGame, 'p1');
excludes(action(blockedNetwork, 'Trail').legalTargetIds, 'e-block-test', 'An opponent structure must block Trail continuation through its node.');

const alreadyUpgradedGame = game();
alreadyUpgradedGame.board.vertices.find((candidate) => candidate.vertexId === 'v1').structureType = 'Stronghold';
excludes(action(getDirectBuildAvailability(alreadyUpgradedGame, 'p1'), 'Stronghold').legalTargetIds, 'v1', 'Own Strongholds must not be upgrade targets.');

const setupCampGame = game({ phase: 'Setup', currentSetupPlayerId: 'p1', setupStep: 'PlaceCamp' });
includes(getSetupCampTargetIds(setupCampGame, 'p1', null), 'v4', 'Setup Camp placement remains direct and free.');
assert(getSetupCampTargetIds({ ...setupCampGame, players: [player({ remainingCamps: 0 }), player({ playerId: 'p2' })] }, 'p1', null).length === 0, 'Setup Camp targets still respect physical piece limits.');
const setupTrailGame = game({ phase: 'Setup', currentSetupPlayerId: 'p1', setupStep: 'PlaceTrail', lastSetupCampVertexId: 'v4' });
setupTrailGame.board.edges.find((candidate) => candidate.edgeId === 'e-owned-b').ownerPlayerId = null;
includes(getSetupTrailTargetIds(setupTrailGame, 'p1', null), 'e-owned-b', 'Setup Trail target derives from the newly placed Camp node.');
assert(getSetupTrailTargetIds({ ...setupTrailGame, players: [player({ remainingTrails: 0 }), player({ playerId: 'p2' })] }, 'p1', null).length === 0, 'Setup Trail targets still respect physical piece limits.');

const freeTrailGame = game({
  hasRolledThisTurn: false,
  activeDevelopmentCardEffect: { type: 'RoadBuilding', freeTrailsPlaced: 0, maxFreeTrails: 2 },
  players: [poorPlayer, player({ playerId: 'p2' })]
});
includes(getFreeTrailTargetIds(freeTrailGame, 'p1', null), 'e-trail-target', 'Road Building stays direct and ignores paid-build resource costs, including before roll.');
assert(getFreeTrailTargetIds(game({ players: [player({ remainingTrails: 0 }), player({ playerId: 'p2' })], activeDevelopmentCardEffect: { type: 'RoadBuilding' } }), 'p1', null).length === 0, 'Road Building still respects physical Trail supply.');

console.log(`direct build tests passed (${assertions})`);

function assert(condition, message) {
  if (!condition) throw new Error(message);
  assertions++;
}
