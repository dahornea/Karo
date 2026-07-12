import { readFileSync, statSync } from 'node:fs';
import { dirname, extname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../src');
const manifestPath = resolve(root, 'assets/game/gameAssets.ts');
const manifest = readFileSync(manifestPath, 'utf8');
const components = readFileSync(resolve(root, 'components/GameAsset.tsx'), 'utf8');
const board = readFileSync(resolve(root, 'components/GameBoard.tsx'), 'utf8');
const hexTile = readFileSync(resolve(root, 'components/HexTile.tsx'), 'utf8');
const cardPanel = readFileSync(resolve(root, 'components/DevelopmentCardsPanel.tsx'), 'utf8');
const gamePage = readFileSync(resolve(root, 'components/GamePage.tsx'), 'utf8');
const commandDock = readFileSync(resolve(root, 'utils/commandDock.ts'), 'utf8');
const tradePanel = readFileSync(resolve(root, 'components/BankTradePanel.tsx'), 'utf8');
const debugPanel = readFileSync(resolve(root, 'components/DebugModePanel.tsx'), 'utf8');
const board3d = readFileSync(resolve(root, 'components/Board3DRenderer.tsx'), 'utf8');
const playerPanel = readFileSync(resolve(root, 'components/PlayerPanel.tsx'), 'utf8');
const matchIconUi = readFileSync(resolve(root, 'components/MatchIconUI.tsx'), 'utf8');
const mainEntry = readFileSync(resolve(root, 'main.tsx'), 'utf8');
const premiumTheme = readFileSync(resolve(root, 'premium-theme.css'), 'utf8');
const campSource = readFileSync(resolve(root, 'assets/game/pieces/source/camp.source.svg'), 'utf8');
const strongholdSource = readFileSync(resolve(root, 'assets/game/pieces/source/stronghold.source.svg'), 'utf8');
const wardenSource = readFileSync(resolve(root, 'assets/game/pieces/source/warden.source.svg'), 'utf8');
const resourceTypes = ['Wood', 'Clay', 'Wool', 'Grain', 'Stone'];
const cardTypes = ['Knight', 'RoadBuilding', 'YearOfPlenty', 'Monopoly', 'VictoryPoint'];
const pieceTypes = ['Trail', 'Camp', 'Stronghold', 'Warden'];
const harborTypes = ['Generic', ...resourceTypes];
let assertions = 0;

for (const resource of resourceTypes) {
  assert(mappingContains('resourceAssets', resource), `${resource} resource asset mapping is missing.`);
  assert(mappingContains('terrainAssets', resource), `${resource} terrain mapping is missing.`);
  const resourceSource = readFileSync(resolve(root, `assets/game/resources/${resource.toLowerCase()}.svg`), 'utf8');
  assert(resourceSource.includes('data-style="karo-resource-v2"'), `${resource} does not use the shared illustrated resource style.`);
}
assert(mappingContains('terrainAssets', 'None'), 'Desert terrain mapping is missing.');
assert(mappingContains('tileSymbolAssets', 'None'), 'Desert symbol mapping is missing.');

for (const type of cardTypes) assert(mappingContains('cardAssets', type), `${type} card artwork is missing.`);
for (const type of pieceTypes) assert(mappingContains('pieceAssets', type), `${type} piece asset is missing.`);
for (const type of harborTypes) assert(mappingContains('harborAssets', type), `${type} harbor asset is missing.`);

const imports = [...manifest.matchAll(/import\s+\w+\s+from\s+'(\.\/[^']+)'/g)].map((match) => match[1]);
assert(imports.length >= 30, 'Expected the manifest to import the complete local asset set.');

for (const relativePath of imports) {
  const assetPath = resolve(dirname(manifestPath), relativePath);
  const info = statSync(assetPath);
  assert(info.size > 0, `${relativePath} is empty.`);
  assert(info.size < 250_000, `${relativePath} exceeds the lightweight asset budget.`);

  if (extname(assetPath) === '.webp') {
    const header = readFileSync(assetPath).subarray(0, 12).toString('ascii');
    assert(header.startsWith('RIFF') && header.endsWith('WEBP'), `${relativePath} is not a valid WebP.`);
  }
}

assert(components.includes('aria-label={`Costs ${label}`}'), 'ResourceCost must expose an accessible cost label.');
assert(components.includes('export function ResourceStripItem'), 'The shared icon-first Supply strip primitive is missing.');
assert(components.includes('export function ResourceInlineSummary'), 'The shared inline Supply summary primitive is missing.');
assert(components.includes('aria-label={`${type}: ${amount}`}'), 'Supply amounts must retain an explicit accessible name.');
assert(components.includes('gameAssets.cardBack'), 'Hidden cards must resolve to the Karo card back.');
assert(components.includes("'--piece-color': playerColor"), 'Piece assets must expose player-color styling.');
assert(components.includes('fallback={gameAssets.cardBack}'), 'Card artwork must use a production-safe fallback.');
assert(board.includes('gameAssets.terrain.Wood.src') && board.includes('gameAssets.terrain.None.src'), 'Board patterns must use typed terrain assets.');
assert(hexTile.includes('gameAssets.symbols[tile.resourceType].src'), 'Board tiles must render the typed resource or Desert symbol.');
assert(hexTile.includes('height="36"') && hexTile.includes('width="36"'), 'Board resource symbols must remain readable at board scale.');
assert(hexTile.includes('<title>{isWardenTarget ? `Move Warden to ${resourceLabels[tile.resourceType]}`'), 'Resource names must remain available to assistive technology and tooltips.');
assert(!hexTile.includes('<text className="tile-resource-label"'), 'Board tiles must not duplicate resource names as permanent labels.');
assert(hexTile.includes('gameAssets.pieces.Warden.src'), 'The board must render the typed Warden asset.');
assert(wardenSource.includes('id="hood"') && !wardenSource.includes('harbor'), 'The Warden source must use the original hooded sentinel artwork.');
assert(board.includes('gameAssets.harbors[slot.harborType].src'), 'Harbors must render the typed icon for their assigned harbor type.');
assert(board.includes("const isGeneric = slot.harborType === 'Generic'"), 'Only the generic harbor should render an Any label.');
assert(cardPanel.includes('<ResourceCost cost={directBuildCosts[pieceType]} />'), 'Direct build confirmation must render resource icons and values.');
assert(cardPanel.includes('<ResourceStripItem'), 'The command dock and Supplies drawer must use the shared icon-first Supply strip.');
assert(!cardPanel.includes('className="dock-resource-name"'), 'The command dock must not duplicate persistent Supply names.');
assert(cardPanel.includes('<ResourceCost compact cost={developmentCardCost} />'), 'Development Card purchase cost must use shared resource assets.');
assert(tradePanel.includes('<ResourceInlineSummary values={selection} />'), 'Player trade summaries must use shared resource assets.');
assert(tradePanel.includes('aria-label={`${title} ${resource}`}'), 'Player trade inputs must retain resource-specific accessible labels.');
assert(debugPanel.includes('aria-label={`${resource}: ${targetPlayer?.supplies[resource] ?? 0}`}'), 'Debug resource controls must retain accessible resource labels.');
assert(board3d.includes('<TerrainSymbol decorative size="sm" type={item.resource} />'), 'The experimental 3D legend must use the typed terrain symbols.');
assert(cardPanel.includes('className="setup-dock-progress"'), 'The setup command dock must show construction progress.');
assert(cardPanel.includes('Camp {Math.min(me.campsBuilt, 2)}/2'), 'Setup Camp progress is missing.');
assert(cardPanel.includes('Trail {Math.min(me.trailsBuilt, 2)}/2'), 'Setup Trail progress is missing.');
assert(playerPanel.includes('type="VictoryPoint"'), 'Player cards must use the Victory Point asset.');
assert(playerPanel.includes('type="Supplies"'), 'Player cards must use the Supplies asset.');
assert(playerPanel.includes('<DevelopmentCardArtwork decorative hidden />'), 'Player cards must use the private Karo card-back asset.');
assert(playerPanel.includes('<IconStat') && playerPanel.includes('label="Victory Points"') && playerPanel.includes('label="Development Cards"'), 'Player public stats must use accessible icon-stat primitives.');
assert(playerPanel.includes('aria-expanded={isExpanded}') && playerPanel.includes('player-detail-popover'), 'Secondary player information must be available on demand.');
assert(playerPanel.includes('<PieceCount') && playerPanel.includes('label="Accessible harbors"'), 'Expanded player details must contain piece and harbor information.');
assert(matchIconUi.includes('export function AccessibleIconTooltip'), 'Accessible icon tooltip primitive is missing.');
assert(matchIconUi.includes('export function IconActionButton'), 'Icon-plus-label action primitive is missing.');
assert(matchIconUi.includes('export function ContextStateIcon'), 'Context state asset primitive is missing.');
assert(matchIconUi.includes('title={accessibleValue}') && matchIconUi.includes('tabIndex={0}'), 'Compact icon stats must remain keyboard discoverable.');
assert(matchIconUi.includes("kind === 'setup-camp'") && matchIconUi.includes("kind === 'setup-trail'") && matchIconUi.includes("kind === 'warden'"), 'Setup and Warden contexts must use Karo piece assets.');
assert(cardPanel.includes('label="Roll Dice"') && cardPanel.includes('label="End Turn"'), 'Primary turn actions must retain full text labels.');
assert(cardPanel.includes('<ContextStateIcon kind={context.visual}') && cardPanel.includes('<ContextStatusIcon'), 'The right rail must use contextual artwork and compact status chips.');
assert(cardPanel.includes('value={context.visual === \'after-roll\' ? null : game.lastDiceRoll}') && cardPanel.includes('context.details.length > 0'), 'Current Action must not repeat the rolled value in artwork and detail chips.');
assert(cardPanel.includes('direct-build-confirmation') && cardPanel.includes('label={pendingAction ? \'Building...\' : \'Confirm Build\'}'), 'Direct construction must use an explicit confirmation surface.');
assert(!cardPanel.includes("activeDrawer === 'build'") && !cardPanel.includes('function BuildPanel'), 'The manual Build drawer must be removed.');
assert(!commandDock.includes("'build'") && !cardPanel.includes("id: 'build'"), 'The normal Build command entry must be absent.');
assert(board.includes('aria-label={canPlace ? actionLabel : undefined}') && board.includes("'Build Trail on edge'") && board.includes("'Build Camp on intersection'"), 'Direct board construction targets need accessible labels.');
assert(gamePage.includes("selectedBuildTarget.type === 'Trail'") && gamePage.includes('await onBuildCamp') && gamePage.includes('await onBuildStronghold'), 'Confirm must route each target type to its authoritative server action.');
assert(gamePage.includes("event.key === 'Escape'") && gamePage.includes('cancelOnOutsidePointer'), 'Direct build selection must support Escape and outside-pointer cancellation.');
assert(gamePage.includes("karo.direct-build-hint.dismissed") && cardPanel.includes('direct-build-hint'), 'Direct building onboarding must be dismissible and remembered.');
assert(!playerPanel.includes('<small>VP</small>') && !playerPanel.includes('<small>Supplies</small>') && !playerPanel.includes('<small>Cards</small>'), 'Player cards must not repeat text labels beneath recognizable icon stats.');
assert(playerPanel.includes('aria-label={`${player.playerName}: ${statusLabel}') && matchIconUi.includes("label: isSelf ? 'Your turn' : 'Current turn'"), 'Player summaries must expose a clear active status and understandable public-stat labels.');
assert(!board.includes('Shared Map') && board.includes('className="board-identity"'), 'The supported board header must remain compact.');
assert(mainEntry.includes("import './premium-theme.css';"), 'The premium tabletop theme must load after the legacy stylesheet.');
for (const token of ['--karo-bg:', '--karo-surface:', '--karo-board-frame:', '--karo-bronze:', '--karo-disabled:']) {
  assert(premiumTheme.includes(token), `Premium theme token ${token} is missing.`);
}
assert(premiumTheme.includes('.game-screen .board-table'), 'Premium theme must style the supported board surface.');
assert(premiumTheme.includes('.game-screen .context-action-panel'), 'Premium theme must style the contextual action surface.');
assert(premiumTheme.includes('.game-screen .command-dock:not([data-phase="setup"])'), 'Normal-turn command strip styling is missing.');
assert(premiumTheme.includes('@media (max-width: 940px)'), 'Premium theme must retain a responsive stacked layout.');
assert(premiumTheme.includes('.player-card-toggle:focus-visible'), 'Expandable player cards need a visible keyboard focus state.');
assert(premiumTheme.includes('.context-state-icon'), 'Contextual state artwork styling is missing.');
assert(premiumTheme.includes('.context-status-chips'), 'Compact contextual status styling is missing.');
assert(premiumTheme.includes('.dock-actions') && premiumTheme.includes('margin-left: 8px;') && premiumTheme.includes('.dock-resources'), 'Resources and secondary actions must be visually separated.');
assert(premiumTheme.includes('mix-blend-mode: normal;'), 'SVG tile layers must avoid overlay compositing artifacts.');
assert(campSource.includes('id="camp-wood"'), 'Camp must use the refined carved-piece source artwork.');
assert(strongholdSource.includes('id="fort-stone"'), 'Stronghold must use the refined stone-piece source artwork.');

const productionUi = [
  readFileSync(resolve(root, 'components/BankTradePanel.tsx'), 'utf8'),
  readFileSync(resolve(root, 'components/DebugModePanel.tsx'), 'utf8'),
  cardPanel,
  board
].join('\n');
assert(!/[😀-🙏🌀-🫿]/u.test(productionUi), 'Production game UI still contains emoji placeholders.');

console.log(`game asset tests passed (${assertions})`);

function mappingContains(mappingName, key) {
  const start = manifest.indexOf(`const ${mappingName} = {`);
  const end = manifest.indexOf('} satisfies Record', start);
  return start >= 0 && end > start && new RegExp(`\\n\\s*${key}:`).test(manifest.slice(start, end));
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
  assertions++;
}
