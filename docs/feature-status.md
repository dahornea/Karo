# Karo Feature Status

Updated for the current MVP.

## Completed

- Private SignalR rooms, readable room codes, live presence, ready checks, host start, reconnect grace, forfeit, and rematch lifecycle
- Backend-authoritative 19-region board, topology, harbors, Warden start, seeded generation, and integrity validation
- Setup, roll gate, Supply production, finite construction pieces, direct Trail/Camp/Stronghold building, maritime trade, player trade, Warden flow, Development Cards, Largest Army, Longest Trail, Victory Points, and win validation
- Stable 2D SVG renderer and progressive-disclosure match UI
- Responsive Development Cards drawer with a compact purchase surface, private illustrated hand, exact availability states, Supply selectors, and passive Victory Point treatment
- Premium symbolic Development Card artwork and a balanced two-column private-hand layout for Knight, Year of Plenty, Road Building, Monopoly, and Victory Point
- Curated six-image portfolio screenshot gallery

## Partial

- In-memory reconnect works during the current server process; durable recovery after a server restart requires persistence
- Match-end presentation works functionally but can receive a dedicated visual polish pass
- Version-one terrain and construction-piece artwork remains replaceable; Development Cards now use the refined symbolic v3 family
- Backend rule coverage is broad in the executable test harness; browser interaction coverage remains focused and should expand over time

## Experimental

- Optional 3D renderer behind the 2D/3D toggle or `?board=3d`
- Optional GLB asset slots that currently fall back to procedural 3D pieces

## Planned

- Database persistence, match history, player profiles, and statistics
- Dedicated match-end screen polish and richer feedback animation
- Bots, spectator mode, replay/event history, and custom modes
