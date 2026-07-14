import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import ts from 'typescript';

const __dirname = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(__dirname, '../src/utils/sidePanels.ts');
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
  require: () => {
    throw new Error('sidePanels.ts should not require runtime dependencies in tests.');
  }
};
sandbox.exports = sandbox.module.exports;
vm.runInNewContext(compiled, sandbox, { filename: 'sidePanels.ts' });

const { getPlayerStatusLabel, getTurnPanelContext } = sandbox.module.exports;

function game(overrides = {}) {
  return {
    status: 'InProgress',
    phase: 'NormalTurn',
    turnNumber: 3,
    hasRolledThisTurn: false,
    lastDiceRoll: null,
    pendingWardenAction: 'None',
    activeDevelopmentCardEffect: null,
    winningVictoryPoints: 10,
    ...overrides
  };
}

function context(overrides = {}) {
  return getTurnPanelContext({
    currentTurnPlayerName: 'Mira',
    game: game(overrides.game),
    isFinished: false,
    isMyTurn: true,
    setupPiece: 'Camp',
    setupPlayerName: 'Codex',
    setupRound: 'Round 1',
    wardenFlowActive: false,
    winnerName: 'Mira',
    ...overrides
  });
}

function assertEqual(actual, expected, label) {
  if (actual !== expected) {
    throw new Error(`${label}\nExpected: ${expected}\nActual:   ${actual}`);
  }
}

assertEqual(getPlayerStatusLabel({ isActive: true, isSelf: false, isWinner: false, phase: 'Setup' }), 'Setting up now', 'active setup player label');
assertEqual(getPlayerStatusLabel({ isActive: true, isSelf: true, isWinner: false, phase: 'Setup' }), 'Your setup', 'self setup player label');
assertEqual(getPlayerStatusLabel({ isActive: true, isSelf: false, isWinner: false, phase: 'NormalTurn' }), 'Current turn', 'active turn player label');
assertEqual(getPlayerStatusLabel({ isActive: true, isSelf: true, isWinner: false, phase: 'NormalTurn' }), 'Your turn', 'self active turn label');
assertEqual(getPlayerStatusLabel({ isActive: false, isSelf: true, isWinner: false, phase: 'NormalTurn' }), 'You', 'self waiting label');
assertEqual(getPlayerStatusLabel({ isActive: false, isSelf: false, isWinner: true, phase: 'Finished' }), 'Winner', 'winner label');

assertEqual(context({ game: { phase: 'Setup' } }).primaryInstruction, 'Place a Camp', 'setup instruction');
assertEqual(context({ game: { phase: 'Setup' } }).visual, 'setup-camp', 'setup Camp visual');
assertEqual(context({ game: { phase: 'Setup' } }).details.length, 0, 'setup does not repeat state in detail chips');
assertEqual(context({ game: { phase: 'Setup' }, setupPiece: 'Trail' }).visual, 'setup-trail', 'setup Trail visual');
assertEqual(context().primaryInstruction, 'Roll the dice', 'pre-roll instruction');
assertEqual(context().visual, 'before-roll', 'pre-roll dice visual');
assertEqual(context().details.length, 0, 'pre-roll does not repeat active-player or dice details');
assertEqual(context({ game: { hasRolledThisTurn: true, lastDiceRoll: 8 } }).primaryInstruction, 'Rolled 8', 'post-roll instruction');
assertEqual(context({ game: { hasRolledThisTurn: true, lastDiceRoll: 8 } }).visual, 'after-roll', 'post-roll dice visual');
assertEqual(context({ game: { hasRolledThisTurn: true, lastDiceRoll: 8 } }).details.length, 0, 'post-roll result appears only in the primary title');
assertEqual(context({ game: { hasRolledThisTurn: true, pendingWardenAction: 'MoveWarden' }, wardenFlowActive: true }).primaryInstruction, 'Move the Warden', 'warden instruction');
assertEqual(context({ game: { hasRolledThisTurn: true, pendingWardenAction: 'MoveWarden' }, wardenFlowActive: true }).visual, 'warden', 'Warden visual');
assertEqual(context({ game: { hasRolledThisTurn: true, pendingWardenAction: 'MoveWarden' }, wardenFlowActive: true }).phaseLabel, 'Warden', 'Warden context label');
const roadBuildingContext = context({ game: { activeDevelopmentCardEffect: { type: 'RoadBuilding', cardId: 'c1', freeTrailsPlaced: 1, maxFreeTrails: 2 } } });
assertEqual(roadBuildingContext.primaryInstruction, 'Place free Trails', 'development card instruction');
assertEqual(roadBuildingContext.visual, 'road-building', 'Development Card visual');
assertEqual(roadBuildingContext.details.length, 1, 'Road Building keeps only progress detail');
assertEqual(context({ game: { status: 'Finished', phase: 'Finished' }, isFinished: true }).phaseLabel, 'Match finished', 'finished phase label');
assertEqual(context({ game: { status: 'Finished', phase: 'Finished' }, isFinished: true }).primaryInstruction, 'Match finished', 'finished title');
assertEqual(context({ game: { status: 'Finished', phase: 'Finished' }, isFinished: true }).visual, 'finished', 'finished visual');

console.log('side panel tests passed (25)');
