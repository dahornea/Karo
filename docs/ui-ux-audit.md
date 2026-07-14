# Karo Match UI/UX Audit

## Problems Found

- Setup and turn state were repeated in the header subtitle, setup banner, player badges, and right-side turn panel.
- The header permanently showed low-priority metadata such as region count, player count, deck count, and score target.
- Player cards showed too many small details at once: piece counts, Knight count, Longest Trail length, host/current/you badges, and award badges.
- The right-side panel behaved like a generic turn dashboard instead of answering the current gameplay question.
- The bottom command area opened useful drawers, but it did not provide a quick resource readout and its buttons did not clearly separate always-available information from locked actions.
- Debug Mode was already drawer-based, but the normal layout still left too much secondary information visible.
- The desktop layout used viewport height, but the setup banner and dense side panels reduced board space at 1366x768.
- Disabled states existed, but disabled reasons were scattered across components instead of following a shared context model.

## Components Involved

- `GamePage.tsx`: match shell, setup banner, board layout, debug integration.
- `GameHeader.tsx`: compact match identity and permanent metadata.
- `PlayerPanel.tsx`: left rail player summaries.
- `DevelopmentCardsPanel.tsx`: contextual turn panel, direct-build confirmation, command dock, drawers, Warden panel, Cards, Log.
- `BankTradePanel.tsx`: maritime and player trade drawer content.
- `styles.css`: match shell, board viewport, player rail, command dock, drawer surfaces, responsive behavior.

## Proposed Information Hierarchy

Permanent:

- Karo match identity, room code, phase, connection state.
- Compact player rail with VP, supply count, card count, and award icons.
- Main board viewport.
- One contextual action panel describing the required next action.
- Slim command dock with current player's resources and secondary-system entry points.

Contextual:

- Setup instructions live in the right contextual action panel.
- Warden discard/move/steal replaces the generic turn panel.
- Road Building progress appears only while the effect is active.
- Trade, Development Cards, Log, and Match Details open in one shared drawer system.
- Paid construction begins from legal board targets and confirms in the contextual action panel.
- Debug Mode remains a separate development drawer.

Moved To Drawers:

- Full build piece counts remain in Match Details; the selected piece cost and remaining count appear in the contextual confirmation.
- Maritime and player trade forms: Trade drawer.
- Development Card hand and deck controls: Cards drawer.
- Recent event feed: Log drawer.
- Room code metadata, player order, region/harbor counts, deck count, awards, and detailed piece counts: Match Details drawer.

## Proposed Component Structure

- `GamePage`: owns match-level drawer state, closes drawers during mandatory Warden/card-effect states, and composes the viewport shell.
- `GameHeader`: compact match bar with only high-value identity and a Match Details trigger.
- `PlayerPanel`: compact public player summaries.
- `TurnStatusPanel`: contextual action panel for setup, pre-roll, post-roll, active card effect, and finished states.
- `BottomActionTray`: slim command dock with resource counters plus Trade, Cards, and Game Log buttons.
- `GameDrawer`: implemented through the existing reusable drawer shell inside `BottomActionTray`.
- `MatchDetailsPanel`: secondary match metadata.
- `DebugModePanel`: unchanged in concept, stays collapsed by default and outside normal layout.

## Responsive Decisions

- Desktop uses a 100dvh app shell with no page-level scrolling.
- Left rail remains compact, right panel stays contextual, and the board uses the largest available middle column.
- Drawers overlay instead of pushing the board.
- Tablet/mobile collapse to a single-column flow with board first and internal drawer scrolling.

## Accessibility Decisions

- Drawer close buttons remain real buttons.
- Escape closes gameplay drawers.
- Dock buttons keep text labels and titles for disabled/action state hints.
- Header details is a button, not an inert pill.
- Player award icons include `title` text.
- The UI continues to use disabled attributes for unavailable actions where the control itself would otherwise perform a game action.

## Known Limitations After This Redesign

- There is still no dedicated frontend test suite for UI states; verification is build-based plus manual viewport/browser checks.
- Tooltips are implemented with native `title` text rather than a richer accessible tooltip component.
- The experimental 3D renderer remains intentionally separate and may still need future visual work.
- Mobile usability is improved through responsive layout, but a full mobile-specific bottom-sheet interaction model remains future polish.

## Focused Correction Notes

- The stable 2D board should not use perspective, skew, rotate, or asymmetric water-frame offsets. The corrected renderer uses a centered SVG viewport, `preserveAspectRatio="xMidYMid meet"`, and a symmetrical rounded water frame so the island reads straight rather than tilted.
- The bottom command dock should stay readable at desktop sizes: five Supply counters on the left, then three clear action buttons on the right: Trade, Cards, and Game Log.
- Match Details remains available from the header instead of competing with the primary command dock actions.
- Secondary systems still open through one drawer at a time; unavailable actions keep readable helper text and accessible disabled-state hints.

## Command Dock Replacement

Why the previous dock failed:

- Resource counters and action launchers competed inside the same compressed row.
- Helper text such as setup and locked-action state looked like debug output when it was permanently visible.
- Build, Trade, and Cards stayed visible during setup even though the contextual panel and board highlights already owned setup actions.
- Several older dock CSS rules had enough specificity to reintroduce overlap even after visual tweaks.

New structure:

- The command area is two clear sections: a compact Supply strip and a contextual action launcher.
- The Supply strip always shows Wood, Clay, Wool, Grain, and Stone with full labels, quantities, and subtle resource accents.
- The action launcher uses complete labels and icons for Trade, Cards, and Game Log when those actions are relevant. Build is intentionally absent.
- Disabled reasons live in tooltips and accessible labels instead of permanent helper copy below each button.

State-aware visibility:

- Setup: show Supplies and Game Log only.
- Normal turn before rolling: show Supplies, Game Log, and Cards only when the current player has a playable pre-roll Development Card.
- Normal turn after rolling: show Supplies plus Trade, Cards, and Game Log; construction is discovered on the board.
- Mandatory Warden or active Development Card resolution: show Supplies and Game Log only.
- Finished match: show Supplies and Game Log while match details remain available from the header.

Responsive decisions:

- The command area uses two compact rows by default so the board column can remain narrow without overlap.
- Desktop and tablet keep the dock around the intended compact height while preserving full labels.
- Mobile keeps resource labels through a horizontally scrollable Supply strip and keeps actions in a compact launcher row.
- Drawers still overlay the board area, scroll internally, and close on Escape.

Testing notes:

- `Karo.Client/scripts/commandDockTests.mjs` covers setup, pre-roll, post-roll, Warden, active Development Card effect, and finished-match action visibility.
- Manual browser checks cover no clipped dock labels, no dock overlap, and no horizontal page overflow at 1366x768, 1440x900, 1600x900, 1920x1080, tablet, and mobile widths.

Remaining limitations:

- The dock uses native `title` text for disabled reasons; a richer accessible tooltip component remains future polish.
- Mobile resource scrolling is intentionally compact rather than a full mobile bottom-sheet redesign.

## Side Panel Hierarchy Pass

Why the previous side panels were hard to scan:

- Player cards showed identity, turn status, host/self badges, award badges, and active-player messaging with similar visual weight.
- The active setup/current player was present, but the signal competed with repeated small badges.
- The right panel mixed the primary instruction with secondary player stats, making it slower to answer "what happens now?"
- Several small card-like surfaces inside the side panels made the layout feel busier than the board-game task required.

New player rail hierarchy:

- Row 1: avatar, player name, and one clear status label such as `Setting up now`, `Current player`, `Winner`, `You`, or `Waiting`.
- Row 2: three primary public stats only: VP, Supplies, and Cards.
- Row 3: only relevant badges, such as You, Host, Longest Trail, or Largest Army.
- Piece inventory and Knight count are moved out of the always-visible rail and into tooltip/detail surfaces.

New right-panel hierarchy:

- The right panel is now a `Current Action` surface.
- The strongest text is the required action: place a Camp, place a Trail, roll the dice, resolve the Warden, place free Trails, or review the winner.
- Helper copy is limited to one short sentence.
- Details are compact rows for active player, setup step, dice state, progress, or match target.
- Normal player stats stay in the left rail and command dock instead of competing with the current instruction.

Deduplication decisions:

- Header keeps room identity, connection state, and compact phase.
- Left rail owns player identity and public player status.
- Right panel owns the current required action.
- Bottom command area owns Supplies and secondary drawer launchers.
- Match Details remains the place for lower-priority metadata.

Testing notes:

- `Karo.Client/scripts/sidePanelTests.mjs` covers player status labels and right-panel copy for setup, pre-roll, post-roll, Warden, active Development Card effect, and finished match states.
- Browser checks confirmed no clipped side-panel text and no page-level scroll at 1366x768, 1440x900, 1600x900, and 1920x1080.

Remaining limitations:

- The existing tablet/mobile match layout still stacks the rails and board vertically, which can create page-level scrolling. A dedicated mobile side sheet remains future polish.
- Warden-specific resolution controls still use their existing panel structure, though the normal right panel now has the same current-action language for Warden state.

## Icon-First Side Panel Refinement

Previous text overload:

- Player summaries repeated `VP`, `Supplies`, and `Cards` below values that already had recognizable Karo assets.
- Self, host, turn, setup, and award states competed as equally weighted text badges.
- The right rail repeated active player, dice, step, and progress as generic label/value rows.
- Piece inventory and performance statistics were permanently visible even when they were not needed for the current decision.

Implemented hierarchy:

- Player cards keep identity plus three icon/value statistics: Victory Points, Supplies, and Development Cards.
- One primary state remains visible (`Turn`, `Setup`, `Winner`, or `You`); host, self, Largest Army, and Longest Trail use compact focusable status icons.
- Player details expand on demand to show remaining Trails, Camps, Strongholds, Knights played, Longest Trail length, harbor access, and score detail.
- The right rail uses one contextual Karo asset for Camp setup, Trail setup, roll required, dice result, Warden, Road Building, or match completion.
- Generic detail rows are replaced by compact icon/value status chips.
- A selected construction target uses piece artwork, icon-based costs, remaining-piece counts, and explicit Confirm/Cancel actions.

## Direct Board Construction

Why the Build drawer was removed:

- Choosing a piece in a drawer and then returning to the board added a mode-selection step before the meaningful spatial decision.
- The Build launcher competed with Trade, Cards, and Game Log even though construction belongs to the island itself.
- A persistent build mode encouraged broad target overlays and made the board feel like an editor rather than a tabletop surface.

Implemented interaction:

- After rolling, an affordable legal empty edge can be selected for a Trail.
- An affordable legal open intersection connected to the player's Trail network can be selected for a Camp.
- An affordable owned Camp can be selected for a Stronghold upgrade.
- Selection opens a compact confirmation in the Current Action panel. Supplies and pieces are not spent until Confirm calls the existing authoritative server action.
- Escape, Cancel, an outside pointer action, a turn change, a pending action, or a stale synchronized state clears the selection.

Discoverability and accessibility:

- The first eligible turn presents a dismissible icon-led hint; the same instructions remain available in Match Details.
- Actionable SVG targets expose button semantics, keyboard focus, explicit accessible names, and Enter/Space activation.
- Unavailable targets are not highlighted, do not use a pointer cursor, and are excluded from the tab order.
- Touch uses the same first-tap selection and contextual confirmation without relying on hover.

Special flows:

- Setup Camp and Trail targets remain free, automatically highlighted, and directly placeable.
- Road Building remains a separate free-Trail flow, ignores Wood and Clay, and still respects connectivity and finite Trail pieces.
- Warden targeting remains exclusive while its mandatory action is active.

Remaining limitations:

- The main 2D SVG board provides the complete keyboard semantics. The optional 3D renderer supports pointer-based direct construction but remains experimental and canvas-based.
- Native SVG `title` tooltips are used for target costs; a richer shared tooltip could improve pointer positioning later.
- Mobile uses the responsive Current Action panel rather than a target-anchored popover; a dedicated bottom-sheet transition remains optional polish.

## Information Hierarchy Polish

Duplicate information removed:

- The Current Action panel now owns the phase label, primary instruction, and one helper sentence. Post-roll dice values appear in the primary title only, not again in artwork or a detail chip.
- Setup, pre-roll, post-roll, and Warden states no longer repeat active-player, step, or dice metadata already visible in the header and player rail.
- Road Building retains only the progress detail because it is the one value needed to finish the active effect.

Final player-card hierarchy:

- Player name and one concise primary state are first: `Your turn`, `Current turn`, `Your setup`, `Setting up`, `Waiting`, or `Winner`.
- VP, Supply count, and Development Card count stay as icon-plus-value statistics with keyboard-focusable labels and tooltips.
- Host, self, and award indicators remain compact secondary icons. Piece inventory, Knights, Longest Trail, score breakdown, and harbor access stay in the on-demand details surface.

Resource and action grouping:

- The dock keeps the five icon-first Supply counters as one quiet inventory group.
- Trade, Cards, and Game Log form a separate action group with a divider, full labels, minimum 40px targets, selected state, and readable disabled feedback.
- The dock intentionally contains no Build control; direct construction stays on the board and its help remains contextual.

Board and responsive refinements:

- The stable 2D board header now uses a compact `Island` label, smaller resource legend, and a quiet but explicit 2D/3D Experimental control.
- Desktop preserves the header, player rail, central board, right action panel, and dock without introducing page-level scroll. Narrow layouts keep the action labels and allow dock overflow rather than clipping content.

Known limitations:

- The 3D renderer remains experimental and separately composed, so its visual toolbar is intentionally more explicit than the stable 2D board toolbar.
- Tablet and mobile use the responsive stacked layout rather than a dedicated side-sheet design.
- Development Cards retain artwork and names while adding check, clock, lock, or passive-point status markers.
- Activity entries retain readable sentences and add one event icon for faster scanning.

Text intentionally retained:

- Primary instructions remain a short title plus one concise helper sentence.
- Primary actions remain icon plus full text, including Roll Dice, End Turn, Confirm Discard, and card Play actions.
- Native resource selects and player-facing validation messages keep text where icon-only presentation would be ambiguous.

Accessibility approach:

- Compact stats expose complete accessible names such as `2 Victory Points` and `5 Supplies`.
- Icon markers provide `aria-label`, native tooltip text, keyboard focus, and visible focus rings.
- Decorative Karo artwork is hidden from assistive technology when a surrounding control already provides the meaning.
- Color is reinforced by icons, labels, selection state, and borders rather than carrying meaning alone.

Remaining limitations:

- Native `title` tooltips and accessible descriptions are used instead of a custom visual tooltip portal.
- Mobile currently uses compact full-width stacked panels; a dedicated modal sheet interaction remains a future refinement.
- First-use onboarding for unfamiliar Karo-specific status icons is not implemented yet.
