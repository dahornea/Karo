# Karo Portfolio Website Brief

Use this document as source context when asking ChatGPT to write portfolio website copy for Karo. It separates verified product facts from suggested positioning, defines the language that should be used, and identifies the screens and game states that are worth showing.

## Suggested instruction for ChatGPT

```text
Write portfolio case-study copy for the Karo project using only the verified facts in this brief.

The copy should sound like a thoughtful software engineering and product-design case study, not a sales page and not a school-project summary. Emphasize the real-time multiplayer architecture, server-authoritative rules, deterministic board generation, original tabletop visual identity, and the challenge of turning a complex strategy game into a clear browser experience.

Do not describe planned, partial, or experimental work as complete. Do not call Karo a CATAN clone, and do not use CATAN branding or copied rule wording. Use Karo's own terminology throughout.

Produce:
1. A concise project card description of 35-50 words.
2. A portfolio hero title and subtitle.
3. A 150-250 word project overview.
4. A case-study section covering the problem, approach, technical architecture, and outcome.
5. Five concise contribution or engineering highlight bullets.
6. Captions and alt text for the recommended screenshots.
7. A short honest status and limitations note.

Write for recruiters, engineering managers, product designers, and potential collaborators. Keep the language concrete, confident, and technically credible.
```

## Project identity

**Project name:** Karo

**Project type:** Original browser-based, real-time multiplayer hex-board strategy game.

**Portfolio-ready description:** Karo is an original multiplayer strategy board game built around a shared hex-map island, server-authoritative rules, and real-time match synchronization. Players expand across a 19-region board by building Trails, Camps, and Strongholds, managing supply production, trading through banks and harbors, using Development Cards, and competing for Victory Points. The application combines an interactive React interface with ASP.NET Core and SignalR to coordinate turn flow, validate actions, and keep every connected player in sync.

**Recommended project bullets:**

- Built a server-authoritative multiplayer game loop where ASP.NET Core services validate construction, trading, card actions, and scoring before broadcasting state through SignalR.
- Modeled the island as a deterministic graph of regions, vertices, edges, and harbor routes that drives placement rules, connectivity, and board interactions.
- Implemented core gameplay systems including room-based multiplayer setup, turn flow, supply production, building upgrades, maritime trade, and Development Cards.

**Current status:** Playable MVP in active development. The main multiplayer game loop is implemented. Persistence, production deployment, and some final presentation work remain future work.

**Portfolio positioning:** Present Karo as an end-to-end product engineering project that combines real-time systems, server-authoritative game rules, graph and board modeling, responsive interaction design, and an original tabletop-inspired visual system.

## The project story

Karo began as a real-time lobby foundation and grew into a playable multiplayer strategy game. The central engineering challenge was not simply drawing a hex board. The application needed one authoritative match state shared by every browser, deterministic and valid board topology, rule-safe construction and trading, private player information, reconnect-aware room lifecycle behavior, and a UI that made a rules-heavy turn understandable without becoming an admin dashboard.

The resulting application treats the ASP.NET Core backend as the source of truth. Players send actions through SignalR, services validate those actions against the current game state, and the updated state is broadcast back to the room. The React client focuses on presentation, interaction affordances, and explaining what the player can do next.

The visual direction is an original modern tabletop game: illustrated terrain, physical-looking number tokens, coastal harbor markers, compact player information, contextual actions, and progressive disclosure for secondary systems such as trading, cards, and the game log.

## What makes Karo portfolio-worthy

- It is a working full-stack multiplayer product, not a static board mockup.
- The backend owns the match state and validates rules instead of trusting the browser.
- SignalR keeps the lobby, board, turns, actions, and private player views synchronized in real time.
- The board is generated on the server as a seeded 19-region axial hex map with deterministic nodes, edges, harbors, number tokens, and integrity validation.
- Construction uses real graph connectivity and placement rules for Trails, Camps, and Strongholds.
- The implementation includes multi-step game flows such as initial placement, rolling and production, Warden resolution, Development Cards, maritime trade, and player-to-player trade offers.
- Viewer-aware DTOs keep private Development Card identities hidden from opponents.
- The game includes finite physical piece supplies, a finite Development Card deck, Victory Point calculation, Largest Army, Longest Trail, and win detection.
- The UI was deliberately refactored around a board-first hierarchy, direct contextual construction, compact status panels, drawers for secondary systems, and clear disabled-state reasons.

## Verified technology stack

### Frontend

- React
- TypeScript
- Vite
- Tailwind CSS plus custom game and board styling
- SVG-based 2D board renderer
- Experimental lazy-loaded 3D renderer

### Backend

- ASP.NET Core Web API
- SignalR hub for lobby and match actions
- In-memory room, session, and game state
- Server-side game services and rule validation

### Local development

- Manual .NET and Node development workflow
- Docker Compose support
- Backend and frontend test scripts

## Architecture narrative

Use this flow when explaining how the application works:

```text
Player interaction
  -> React sends a typed SignalR action
  -> GameLobbyHub delegates to a game or lobby service
  -> The backend validates identity, phase, turn, cost, placement, and rule constraints
  -> The authoritative in-memory GameState is updated
  -> Viewer-aware DTOs are broadcast to the room
  -> Every React client renders the synchronized result
```

Important architectural points:

- SignalR hubs are transport boundaries; game rules live in services rather than in one large hub method.
- The backend generates the board once per match. Clients never create their own random board.
- Board vertices and edges have deterministic IDs and drive placement, connectivity, harbors, and Longest Trail.
- Reconnect tokens are hashed server-side and support refresh recovery while the current server process remains alive.
- Public room state does not expose reconnect tokens or private Development Card hands.
- The current MVP deliberately uses in-memory state, so a server restart clears active rooms and matches.

## Implemented player experience

### Lobby and multiplayer lifecycle

- Create and join private rooms using readable six-character room codes.
- Live player list, host identity, ready checks, and host-only start.
- Disconnect grace periods, host migration, refresh recovery, forfeit, leave, and room cleanup behavior.
- Return-to-lobby and rematch flow after a match.

### Board and setup

- Shared server-generated 19-region hex board.
- Wood, Clay, Wool, Grain, Stone, and one non-producing Desert/None region.
- Number tokens, deterministic vertices and edges, nine coastal harbors, and the Warden.
- Forward and reverse initial placement order.
- Two starting Camps and Trails per player, with starting Supplies granted from productive regions touching the second Camp.

### Normal turn and construction

- Mandatory dice roll before normal actions.
- Server-authoritative 2d6 production.
- Camps produce one Supply and Strongholds produce two.
- Direct contextual placement of Trails and Camps from legal board targets.
- Stronghold upgrades on owned Camps.
- Finite piece inventories: 15 Trails, 5 Camps, and 4 Strongholds per player.

### Trading and cards

- Default 4:1 bank trade.
- Generic 3:1 and resource-specific 2:1 harbor rates based on coastal structure access.
- Player-to-player Supply trade offers with accept, reject, cancel, and lifecycle rules.
- Finite 25-card Development Card deck.
- Knight, Road Building, Year of Plenty, Monopoly, and Victory Point cards.
- Private card hands and rule-safe purchase/play timing.

### Scoring and advanced flows

- Victory Point scoring and win detection.
- Largest Army and Longest Trail awards.
- Full Warden flow after a roll of 7: discard when required, move, select an eligible opponent, and steal a random Supply.
- Debug tools for deterministic rule testing in development only.

## Karo terminology

Use these names consistently:

| Karo term | Meaning |
| --- | --- |
| Trail | A player's route piece |
| Camp | A basic structure |
| Stronghold | An upgraded Camp |
| Supplies | The five collectible resource types |
| Warden | The piece that blocks production and enables a steal |
| Victory Points | Match score |

The five Supplies are **Wood, Clay, Wool, Grain, and Stone**.

Current construction costs:

- Trail: 1 Wood + 1 Clay
- Camp: 1 Wood + 1 Clay + 1 Wool + 1 Grain
- Stronghold: 2 Grain + 3 Stone
- Development Card: 1 Wool + 1 Grain + 1 Stone

## Writing direction

### Tone

- Product-minded and technically grounded.
- Confident about completed work without pretending the project is production-hosted.
- Clear enough for a non-game engineer to follow.
- Specific about engineering decisions and outcomes.
- Warm and visual when describing the tabletop experience.

### Themes to emphasize

- Translating physical tabletop rules into deterministic server-side software.
- Synchronizing multiple players without giving the client authority over game outcomes.
- Modeling a hex board as tiles, vertices, and edges instead of treating it as decoration.
- Making complex turn states understandable through contextual actions and progressive disclosure.
- Balancing product polish with maintainable React components and backend services.
- Testing hard-to-reproduce multiplayer states while preserving server authority.

### Claims to avoid

- Do not call Karo production-ready or commercially released.
- Do not claim database persistence; the current game state is in memory.
- Do not imply that active matches survive an API restart.
- Do not present the experimental 3D renderer as the main or finished board.
- Do not claim automated browser screenshot capture or visual regression testing.
- Do not describe the planned polished match-end screen as complete.
- Do not use CATAN branding, artwork, copied text, or a "clone" framing.

## Recommended portfolio page structure

1. **Hero:** Karo name, one-sentence value proposition, stack, and the mid-game board screenshot.
2. **Challenge:** Explain the synchronization, rule-validation, and information-hierarchy problem.
3. **Product experience:** Show landing/lobby briefly, then focus on the match board and contextual actions.
4. **Technical approach:** Use the SignalR action flow and server-authoritative architecture.
5. **Rules and systems:** Feature construction, production, trading, Development Cards, Warden, and scoring.
6. **Design evolution:** Explain the move from dashboard-like panels toward a board-first tabletop interface.
7. **Outcome and learning:** Summarize the playable MVP and the engineering lessons.
8. **Current limitations and next steps:** Persistence, deployment, final match-end presentation, testing depth, and optional future 3D exploration.

## Screenshot strategy

Karo is a single-page application with three main product surfaces: the landing page, the lobby, and the match screen. Most portfolio value is inside the match screen, so different match states deserve separate screenshots even though they use the same page.

### Curated six-image set

#### 1. Multiplayer lobby

Use once near the beginning to establish room-based multiplayer. The saved frame contains Rowan, Mira, Kellan, and Dorian as distinct connected players with ready states and Rowan as Host.

**Capture:** Recreate a normal four-player lobby and capture it through the standard client flow.

**Filename:** `karo-multiplayer-lobby.png`

#### 2. Mid-game match overview - portfolio hero

This is the strongest single image and the default for the project card, case-study hero, featured-project placement, and social preview. It shows the full board, player rail, active turn, Supplies, scores, structures, and harbors without an open drawer.

**Capture:** Recreate a representative mid-game state through normal gameplay and use the stable 2D renderer.

**Filename:** `karo-mid-game-overview.png`

#### 3. Trail construction

The deliberate close crop shows a legal selected edge and the confirmation panel together. It demonstrates graph-backed placement validation and a complete action flow rather than another generic board overview.

**Capture:** Select a legal Trail edge during a normal build action, then capture the board and confirmation together.

**Filename:** `karo-trail-construction.png`

#### 4. Maritime trade

This frame uses a real owned Wood harbor and an authoritative 2:1 route. Keep the specific rate, coastal ownership, Supply conversion controls, and player-trade entry point visible.

**Capture:** Open Maritime Trade during a normal match where the player owns a resource-specific harbor.

**Filename:** `karo-maritime-trade.png`

#### 5. Development Cards

The centered drawer shows labeled Wool, Grain, and Stone purchase costs plus Knight, Year of Plenty, Road Building, and Victory Point cards with playable, same-turn locked, and passive states.

**Capture:** Open the private Development Cards drawer during a normal match with a visually varied hand.

**Filename:** `karo-development-cards.png`

#### 6. Supply production - distinct extra

This is the one additional system frame. Its highlighted producing regions, dice result, updated inventories, and left-side Game Log make it more visually and mechanically distinct than another build or late-game overview.

**Capture:** Roll a productive number in a normal match and open the Game Log after inventories update.

**Filename:** `karo-supply-production.png`

### Optional documentation-only screenshots

- Landing page when a broader product journey is more important than a tight gameplay case study.
- Camp placement or Stronghold upgrade when discussing node topology and finite construction pieces.
- Warden movement or victim selection when discussing multi-step server-controlled actions.
- Player-to-player trade viewed from proposer and recipient roles.
- Debug Mode only in an engineering-process section.
- Experimental 3D only in a clearly labeled exploration section.

### Screens that do not deserve primary portfolio space

- The experimental 3D board as the first or largest image.
- Empty or one-player lobby states.
- Generic loading screens.
- Debug overlays with node and edge IDs, unless specifically illustrating topology tooling.
- Error toasts or disconnected states, unless the case study is about lifecycle resilience.
- Multiple screenshots that differ only by an open drawer and tell the same story.

## Recommended screenshot set by page length

### Compact project card

Use only the Mid-game Overview. Add Maritime Trade when a second image is supported.

### Standard case study

Use Lobby, Mid-game Overview, Trail Construction, Maritime Trade, and Development Cards.

### Detailed engineering case study

Use the complete six-image sequence and finish with Supply Production. Do not add another card drawer or full-board overview unless the surrounding section needs that exact system.

## Capture guidance

- Prefer the stable 2D renderer.
- Capture representative match states at `1600x1000` and 100% browser zoom.
- Wait for the board, local artwork, player state, and any requested drawer to finish rendering before capturing.
- Keep the board centered and include enough of the player and action panels to explain the state.
- Use the same viewport, browser zoom, and crop family across the case study.
- Avoid personal browser chrome, unrelated tabs, open developer tools, and temporary error messages.
- Capture PNG for text-heavy UI. Use WebP only when the portfolio pipeline controls compression carefully.
- Write alt text that explains the visible game state, not just "Karo screenshot."

## Suggested screenshot captions

- **Multiplayer lobby:** "Multiplayer lobby for room-based match setup, player seating, and ready-state flow."
- **Mid-game overview:** "Mid-game match overview showing the shared board, player standings, resources, and active turn context."
- **Trail construction:** "Trail construction flow showing placement validation and action confirmation on the island graph."
- **Maritime trade:** "Maritime trade interface showing bank and harbor routing through an owned 2:1 Wood harbor, plus resource conversion decisions."
- **Development Cards:** "Development Cards surface showing purchase costs and playable, same-turn locked, and passive card-driven actions."
- **Supply production:** "Supply production preview connecting a server-side dice result to highlighted regions, updated inventories, and the shared event feed."

## Suggested alt text

- **Multiplayer lobby:** "Karo multiplayer lobby showing Rowan, Mira, Kellan, and Dorian connected in a private room with a readable code, host badge, and ready controls."
- **Mid-game overview:** "Karo mid-game match showing three players around a shared 19-region island with Trail networks, structures, harbors, Supplies, scores, and turn controls."
- **Trail construction:** "Close Karo board view with a legal Trail edge highlighted and a construction confirmation showing cost and remaining pieces."
- **Maritime trade:** "Karo Maritime Trade drawer showing an owned Wood harbor, a 2-to-1 Wood route, Supply selectors, and player trade controls."
- **Development Cards:** "Karo Development Cards drawer showing purchase costs and private Knight, Year of Plenty, Road Building, and Victory Point cards with distinct states."
- **Supply production:** "Karo match after a dice roll with producing regions highlighted, inventory totals updated, and ordered production events shown in the Game Log."

## Honest current limitations

- Rooms, sessions, and active matches are stored in memory and reset when the API restarts.
- There is no production cloud deployment or database-backed match history yet.
- The stable 2D renderer is the supported presentation; 3D remains experimental.
- Refresh recovery works during the current server process, but durable reconnect across restarts is not implemented.
- The repository retains a curated static screenshot gallery under `docs/screenshots/`; refresh it manually when presentation changes materially.
- Some final portfolio polish remains, including a richer match-end screen and a future final illustration pass for selected assets.

## Legal and originality note

Karo is an original portfolio project inspired by the general resource-trading hex-board strategy genre. It uses original naming, UI, artwork, assets, visual direction, and implementation. Portfolio copy should not use CATAN branding, official artwork, copied rulebook language, or imply affiliation with another game publisher.
