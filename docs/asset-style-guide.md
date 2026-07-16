# Karo Game Asset Style Guide

Karo uses a hybrid local asset system. Format follows behavior: SVG for icons and recolorable silhouettes, WebP for illustrated surfaces, and optional GLB only for the experimental 3D renderer.

## Visual language

- Warm, tactile tabletop illustration with clean silhouettes and restrained detail.
- Dark neutral outlines preserve contrast across terrain and player colors.
- Resource identity comes from shape, texture, value, and color together.
- Artwork must remain readable at compact board and drawer sizes.
- No emoji, copied board-game imagery, external runtime URLs, or photographic terrain.

## Resources

Resource SVGs share a 32 by 32 view box and the `karo-resource-v2` filled illustration language: a dark restrained outline, a colored body, one internal highlight layer, and comparable silhouette density.

- Wood: a compact timber bundle with a visible cut-log face.
- Clay: stacked dimensional terracotta bricks.
- Wool: a sheep silhouette, intentionally distinct from Wood.
- Grain: a tied wheat sheaf.
- Stone: a layered rock cluster.

Use `ResourceIcon`, `ResourceAmount`, `ResourceStripItem`, `ResourceInlineSummary`, and `ResourceCost` instead of component-local icon switches. Persistent inventory strips should show the resource symbol and count; names remain available through accessible labels and tooltips. Native select options and explanatory validation text may keep visible resource names where icons alone would reduce clarity.

## Terrain

Terrain art is authored as versioned SVG source under `terrain/source` and exported to reusable WebP. Existing color gradients and SVG overlays remain in place so number tokens and resource symbols stay readable if a texture is unavailable. Permanent resource-name labels are intentionally omitted from the normal board; names remain in SVG titles, tooltips, accessible labels, and Debug Mode.

The source art is intentionally stylized and non-photographic. Number tokens remain the highest-contrast object on each tile; resource icons and names are secondary.

## Pieces

- Trail remains SVG so it can rotate and accept player-color treatment.
- Camp, Stronghold, and Warden use transparent WebP exported from maintainable SVG source. The Warden is an original hooded geometric sentinel rather than a chess or generic pawn silhouette.
- Camp and Stronghold use a separate player-color base/accent rather than one raster per player.
- Empty placement nodes remain procedural because their state and interaction are dynamic.

## Development Cards

Development Card artwork uses the `karo-card-v3` symbolic family: a shared 480 by 300 landscape frame, one dominant emblem, one mechanic cue, bold restrained outlines, and a limited card-specific palette. The set deliberately avoids miniature narrative scenes and decorative filler:

- Knight: a helmeted Warden crest, position path, and captured Supply token.
- Year of Plenty: one harvest basket beneath two highlighted Supply medallions.
- Road Building: exactly two connected timber Trail pieces and one construction mark.
- Victory Point: a single gold prestige seal with a private-score lock.
- Monopoly: matching Supply tokens converging into one controlled stack.

Card artwork supports recognition but never replaces the card name, effect summary, status, or accessible label. Keep the hero symbol inside the central crop-safe band, preserve readable contrast at compact drawer sizes, and use `<title>` plus `<desc>` in every source SVG. Hidden cards use the original Karo hex-pattern card back.

Camp, Stronghold, and terrain raster illustrations are version-one repository artwork. They are production-safe and replaceable through the manifest, but they are not claimed as final commissioned art. Development Cards, resource symbols, action symbols, harbor symbols, and the Warden silhouette are the current refined illustration and iconography baseline.

## Board integration

- Number tokens retain the strongest on-tile contrast.
- Medium resource symbols identify terrain without repeating visible resource names.
- The toolbar legend is icon-only with keyboard-focusable labels and tooltips, including a dedicated Desert dune symbol.
- Harbor plaques are compact physical markers: their icon is primary, the rate is secondary, and only Generic harbors retain the short `Any` label.
- Setup keeps Supplies, Camp and Trail progress, and Game Log visible while paid board construction, Trade, and Card actions stay inactive.

## 3D assets

The manifest reserves optional GLB mappings for Trail, Camp, Stronghold, and Warden. No GLB files are currently shipped. The experimental renderer continues to use procedural geometry and must fall back cleanly through `Model3DFallback`; 2D never loads GLB assets.

## Exporting WebP

From `Karo.Client`:

```powershell
pnpm render:assets
```

This runs `scripts/renderGameAssets.mjs`, which uses Sharp to export every `*.source.svg` file to an optimized WebP beside its category. Source SVGs and exported WebPs are both versioned.

## Accessibility

- Decorative assets use `aria-hidden` or empty alt text.
- Meaningful artwork includes a resource, piece, harbor, or card name.
- Icon-only commands retain an accessible name.
- Icon-only legend entries are keyboard focusable and retain resource names through labels and tooltips.
- Icon-first Supply amounts expose explicit labels such as `Wood: 1`; resource inputs identify the resource in their accessible name.
- Resource information is never communicated by color alone.
- Player pieces retain neutral outlines plus a separate player-color accent.
- Side-panel stats use icon plus value with a complete accessible label; primary actions never rely on icon meaning alone.
- Karo-specific status icons remain keyboard focusable and expose a tooltip or accessible description.

## Adding an asset

1. Add an original source file in the matching category.
2. Export WebP where the category calls for raster art.
3. Add the mapping to `gameAssets.ts`; use `satisfies Record<...>` so missing enum members fail TypeScript compilation.
4. Add or update the asset mapping tests.
5. Record provenance and license details in `asset-licenses.md`.
