import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../src');
const panel = readFileSync(resolve(root, 'components/DevelopmentCardsPanel.tsx'), 'utf8');
const theme = readFileSync(resolve(root, 'premium-theme.css'), 'utf8');
let assertions = 0;

assert(panel.includes('data-drawer={activeDrawerDetails.id}'), 'Utility drawers must expose their active drawer type for scoped layouts.');
assert(panel.includes("activeDrawer === 'cards' ? 'Development Cards'"), 'The Cards drawer must use the full Development Cards title.');
assert(panel.includes('Buy from the shared deck and manage your private hand.'), 'The drawer needs concise supporting context.');
assert(panel.includes('id="development-purchase-title"') && panel.includes('Buy Development Card'), 'The real purchase section is missing.');
assert(panel.includes('<ResourceCost compact cost={developmentCardCost} />'), 'Purchase cost must use the shared Supply assets.');
assert(panel.includes('data-affordable={owned >= required}'), 'Owned purchase Supplies must expose affordability state.');
assert(panel.includes('<h3><Hand aria-hidden="true" size={17} /> Private hand</h3>'), 'The private hand heading is missing.');
assert(panel.includes("availability.playableCards.filter((card) => card.canPlay).length"), 'The private hand must summarize cards playable now.');
assert(panel.includes('label="First Supply"') && panel.includes('label="Second Supply"'), 'Year of Plenty must retain two labeled Supply selectors.');
assert(panel.includes('<ResourceInlineSummary values={yearSelection} />'), 'Year of Plenty must show its current selection.');
assert(panel.includes('label="Supply to collect"') && panel.includes('<ResourceInlineSummary values={monopolySelection} />'), 'Monopoly must show its selected Supply.');
assert(panel.includes("onPlayKnight(game.roomCode, card.cardId, '', null)"), 'Knight must continue into the authoritative Warden flow.');
assert(panel.includes('onStartRoadBuilding(game.roomCode, card.cardId)'), 'Road Building must continue into board placement.');
assert(panel.includes('+1 hidden Victory Point') && !panel.includes('Play Victory Point'), 'Victory Point cards must remain passive without a fake action.');
assert(panel.includes("label: 'Bought this turn'") && panel.includes('Locked until your next turn.'), 'Same-turn card locks need a concise status and reason.');
assert(panel.includes('className="development-card-body"'), 'Development Cards need a distinct identity and action body beneath the hero artwork.');
assert(panel.includes("type === 'VictoryPoint' ? 'Private merit' : 'Action card'"), 'Card category labels must distinguish private merit from playable actions.');
assert(!panel.includes('development-card-kind'), 'Artwork must not carry a duplicate floating card-kind label.');
assert(panel.includes('aria-label={label}') && theme.includes('.development-card-action:focus-visible'), 'Development Card selectors and actions need explicit accessible focus behavior.');
assert(theme.split('grid-template-columns: repeat(2, minmax(0, 430px));').length - 1 >= 2, 'Desktop and medium Development Cards must keep the balanced two-column grid.');
assert(theme.includes('grid-template-columns: minmax(0, 320px);'), 'Mobile Development Cards must use a one-column grid.');
console.log(`development card UI tests passed (${assertions})`);

function assert(condition, message) {
  if (!condition) throw new Error(message);
  assertions++;
}
