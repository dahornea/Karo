# Karo Visual Design Audit

## Implementation Status

The premium tabletop direction described here is now implemented as a final theme layer in `Karo.Client/src/premium-theme.css`. It is imported after the historical stylesheet so the current design system has one explicit source of truth without risking a broad rewrite of working gameplay surfaces.

Implemented outcomes:

- warm stone application background with parchment and carved-wood surfaces
- deep teal interaction color with restrained bronze highlights
- flatter match header and side panels with fewer pill-shaped controls
- darker physical board frame, calmer sea, quieter tile highlights, and subtler empty nodes
- compact carved harbor markers and refined Camp, Stronghold, Trail, and Warden silhouettes
- a setup-specific progress strip and a compact single-row normal-turn command strip
- icon-first Supply strips, costs, trade summaries, card purchase states, and debug controls backed by the shared asset manifest
- responsive desktop rails with board-first stacking below the tablet breakpoint

Camp, Stronghold, terrain, and Development Card raster illustrations remain version-one replaceable repository art. The 2D renderer is the supported visual baseline; 3D remains experimental.

## Current Problems

- The product palette leans too heavily on brown, beige, parchment, and muddy yellow surfaces.
- Several generations of CSS coexist, so panels, drawers, board frames, landing cards, and command controls use different surface colors, border strengths, and shadows.
- The application background and board frame feel like separate objects instead of one cohesive game table.
- Many supporting panels use similar visual weight, which makes hierarchy depend on boxes rather than spacing, typography, and intent.
- Borders are often dark brown and repeated across nested elements, making the UI feel older and heavier than the game needs.
- Badges and pills are used broadly, so important match state competes with metadata and secondary controls.
- Board terrain patterns are functional but repetitive; resource identity still relies too much on text labels.
- Water reads as a framed tray in places instead of a calm illustrated sea around the island.
- Harbors are improved but still partially read as UI plaques rather than compact physical coastal tokens.
- Empty build nodes and placement rings can feel like debug overlays in normal play.
- Tile labels, harbor labels, and number tokens do not always share the same physical board-game language.
- Toasts and validation surfaces still use strong red/brown styling and bubble-like shapes that do not match the calmer tabletop direction.

## Final Art Direction

Karo should use a **modern illustrated tabletop** visual language:

- soft neutral application background
- off-white and sage-gray surfaces
- deep teal primary accent
- warm gold secondary accent
- dark charcoal typography
- natural wood only for semantic board pieces such as Trails and docks
- illustrated terrain rather than flat resource colors
- cream physical number tokens
- subtle depth, minimal borders, and clean iconography

The target feel is a polished digital board game, not an admin dashboard, parchment website, or brown prototype.

## Implemented Design Tokens

- `--karo-bg`: application background, warm gray/ivory
- `--karo-surface`: primary off-white surface
- `--karo-surface-2`: secondary sage-gray neutral
- `--karo-surface-3`: drawer/popover surface
- `--karo-ink`: dark charcoal text
- `--karo-muted`: gray-green muted text
- `--karo-line`: low-contrast neutral border
- `--karo-teal`: primary action/accent
- `--karo-teal-dark`: hover/selected accent
- `--karo-gold`: game highlight accent
- `--karo-danger`: restrained brick red
- `--karo-success`: natural green
- `--karo-shadow-sm`, `--karo-shadow-md`, `--karo-shadow-lg`: consistent depth scale
- Resource tokens: Wood, Clay, Wool, Grain, Stone, Desert, and Water colors.

## Implemented Surface System

- Level 0: soft application background with a very subtle illustrated grid/noise effect.
- Level 1: main board/game surface with minimal framing and high board priority.
- Level 2: player rail, contextual panel, lobby cards, and action controls.
- Level 3: drawers, toasts, popovers, and temporary overlays.

The redesign should avoid panel-inside-panel stacking where spacing and typography can do the job.

## Board Art System

- Land hexes remain edge-to-edge in the existing 19-tile geometry.
- Terrain uses cohesive SVG patterns with subtle illustrated motifs:
  - Wood: dense dark forest silhouettes.
  - Wool: pale open meadow/hill marks.
  - Grain: warm field rows and wheat-like strokes.
  - Clay: terracotta earth ridges.
  - Stone: cool rocky facets.
  - Desert: warm sand and dunes.
- Number tokens stay cream, circular, physical, and highly readable.
- Resource text remains available but becomes secondary to terrain and tokens.
- Water becomes desaturated teal-blue with calm wave texture and low contrast.
- Harbors become smaller physical coastal tokens with short dock connectors.
- Empty build nodes become subtle in normal play and prominent only during valid placement/build states.
- Trails, Camps, Strongholds, and the Warden should feel like placed tabletop pieces, using layered SVG shapes and shadows.

## Components And Files Updated

- `Karo.Client/src/styles.css`: existing layout and component behavior styles.
- `Karo.Client/src/premium-theme.css`: final visual tokens and premium tabletop overrides loaded last.
- `Karo.Client/src/components/HexTile.tsx`: refined terrain icon positioning and token hierarchy if needed.
- `Karo.Client/src/components/GameBoard.tsx`: board water, harbor, node, edge, and piece visuals if needed.
- `README.md`: updated to document the visual direction and stable 2D renderer status.
- `docs/visual-design-audit.md`: this audit and implementation guide.

## Remaining Limitations

- The stable 2D renderer is the supported presentation; the 3D renderer remains experimental and correctness-focused.
- The board uses a hybrid local SVG/WebP and procedural SVG pipeline; several raster illustrations remain version-one rather than commissioned final art.
- Responsive behavior should remain app-like on desktop, but a mobile-specific bottom-sheet interaction model may still be worth a future pass.
- Future accessibility work can add richer tooltips and reduced-motion visual alternatives beyond the current native labels and focus states.

## Focused Board Correction

- The 2D board composition should remain visually straight and symmetric. Avoid adding visual perspective, skew, rotation, or asymmetric water-frame transforms to the supported board renderer.
- The calm water frame now uses a simple rounded shape behind the island instead of an uneven tray shape, keeping the land board centered and stable in screenshots.
- Command dock visuals should support the board instead of crowding it: full supply names and counts, compact icons, and four readable action labels.
