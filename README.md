# Karo

Real-time multiplayer hex strategy board game built with React, TypeScript, ASP.NET Core, and SignalR.

**Status:** MVP in active development.

Karo is an original portfolio project with its own naming, UI, assets, and implementation. It is inspired by the general genre of resource-trading hex-board strategy games, but it does not use CATAN branding, official artwork, assets, copied rulebook text, or copyrighted presentation.

## Overview

Karo is a browser-based multiplayer board game where players create private rooms, invite friends, place Camps and Trails, collect supplies, trade with the bank, harbors, and other players, use Development Cards, move the Warden, and compete for Victory Points.

The current project is local-first and free to run. The backend keeps an in-memory authoritative game state, while the React client renders the shared board and sends player actions over SignalR. The goal is a playable, portfolio-quality multiplayer board game that can grow into a fuller production-style architecture over time.

## Tech Stack

- Frontend: React, TypeScript, Vite
- Styling: Tailwind CSS plus custom CSS for the board/game UI
- Backend: ASP.NET Core Web API
- Realtime: SignalR
- State/storage: in-memory lobby and game state for the current MVP
- Database: not implemented yet
- Board rendering: stable polished 2D SVG renderer, experimental lazy-loaded 3D renderer
- Local development: Docker Compose or manual .NET/Node tooling

## Visual Direction

Karo uses a **modern illustrated tabletop** art direction: soft neutral application backgrounds, off-white and sage-gray surfaces, deep teal primary actions, warm gold highlights, charcoal typography, illustrated terrain tiles, cream physical number tokens, calm teal water, and compact board-game harbor markers.

The stable 2D renderer is the primary visual presentation. It uses a hybrid local asset system: original SVG icons for scalable resources and actions, transparent WebP illustrations for pieces and Development Cards, and reusable WebP terrain surfaces layered beneath the existing SVG board geometry. Versioned SVG sources remain beside the raster exports. See the [asset style guide](docs/asset-style-guide.md) and [asset license ledger](docs/asset-licenses.md).

The experimental 3D renderer remains available for future exploration, but it is not the supported visual baseline. Optional GLB mappings are reserved in the manifest; the current 3D scene still uses procedural fallbacks and is documented honestly as experimental.

## Features

### Multiplayer Lobby

- ✅ Implemented: private rooms with readable room codes
- ✅ Implemented: create room and join room flows
- ✅ Implemented: live player list updates through SignalR
- ✅ Implemented: host detection and host-only game start
- ✅ Implemented: disconnect handling, host reassignment, and empty room cleanup
- ⏳ Planned: durable reconnect support across server restarts

### Multiplayer Lifecycle

- [Implemented] Stable per-player room sessions with server-side hashed reconnect tokens and refresh recovery for the current server process
- [Implemented] SignalR reconnect/resume flow with stale-tab replacement protection and connection-state visibility
- [Implemented] Ready checks before match start, reconnect grace periods, current-action pause/resume, and deterministic host migration after timeout
- [Implemented] Explicit lobby leave, in-match forfeit, host continuation/end controls for timed-out players, and post-game return-to-lobby/rematch flow
- [Implemented] Room/game state versions, lifecycle cleanup, and public room status that do not expose reconnect tokens or private hands
- [Partial] Session recovery is intentionally in-memory only; a server restart invalidates active sessions and match state

### Board

- ✅ Implemented: backend-generated shared 19-region hex board
- ✅ Implemented: Wood, Clay, Wool, Grain, Stone, and Desert/None regions
- ✅ Implemented: number tokens excluding 7
- ✅ Implemented: deterministic board vertices and edges for Camps, Trails, Strongholds, and harbor access
- ✅ Implemented: 9 fixed coastal harbor slots with randomized harbor types
- ✅ Implemented: Warden starts on the Desert/None region and blocks production
- [Implemented] Cohesive modern illustrated 2D board art with terrain patterns, calm water, physical number tokens, subtle build nodes, and compact harbor markers
- [Implemented] Straight, centered 2D board presentation with a symmetrical water frame and stable SVG viewport
- [Implemented] Seeded backend-authoritative generation with reproducible terrain, tokens, topology, and harbor assignments
- [Implemented] BoardIntegrityValidator blocks invalid 19-tile layouts, terrain/token distributions, 6/8 adjacency, topology, harbor attachments, and Warden starts before a match is broadcast

### Gameplay

- ✅ Implemented: initial setup phase with forward then reverse placement order
- ✅ Implemented: setup Camp placement, setup Trail placement, and one-empty-node Camp spacing
- ✅ Implemented: second setup Camp grants starting supplies from adjacent productive regions
- ✅ Implemented: normal turn roll gate
- ✅ Implemented: server-authoritative 2d6 dice roll and supply production
- ✅ Implemented: Camps produce 1 supply and Strongholds produce 2 supplies during production
- ✅ Implemented: finite player construction-piece supplies: 15 Trails, 5 Camps, and 4 Strongholds
- ✅ Implemented: paid normal-turn building actions for Trails, Camps, and Strongholds
- ✅ Implemented: Stronghold upgrades return the upgraded Camp piece to the player supply
- ✅ Implemented: Warden flow on 7, including discard, move, victim selection, and random steal
- ✅ Implemented: Victory Point scoring and win detection
- ✅ Implemented: Largest Army awards +2 VP after 3 played Knights and transfers only to a strict leader
- ✅ Implemented: Longest Trail awards +2 VP for the longest continuous Trail of at least 5
- ✅ Implemented: user-friendly validation messages for expected rule failures
- ⏳ Planned: polished match-end screen

Trail connectivity note: paid Trails, Road Building Trails, and valid-edge previews use the same rule. A new Trail can connect through one of your own Camps or Strongholds, can extend from an empty node connected to one of your Trails, and cannot continue through an opponent's Camp or Stronghold.

### Development Cards

- ✅ Implemented: finite server-side shuffled 25-card Development Card deck
- ✅ Implemented: Development Card purchase cost and private hands
- ✅ Implemented: deck exhaustion blocks card purchases
- ✅ Implemented: same-turn play restriction for action cards
- ✅ Implemented: one non-Victory Point Development Card per turn
- ✅ Implemented: Knight starts the Warden move/steal flow without discard
- ✅ Implemented: Year of Plenty adds exactly 2 selected supplies
- ✅ Implemented: Monopoly transfers the selected supply from opponents
- ✅ Implemented: Victory Point cards stay hidden and count toward win calculation
- ✅ Implemented: Road Building can place up to 2 free Trails, consumes physical Trail pieces, and updates Longest Trail

### Trading

- ✅ Implemented: default 4:1 maritime/bank trade
- ✅ Implemented: generic 3:1 harbor rate
- ✅ Implemented: resource-specific 2:1 harbor rate
- ✅ Implemented: harbor access through adjacent Camp or Stronghold vertices
- ✅ Implemented: server-side trade validation and frontend disabled states
- ✅ Implemented: player-to-player Supply trade offers between the current player and one target opponent

### Match Experience

- ✅ Implemented: compact top match header with room, phase, turn, and connection context
- ✅ Implemented: scannable left player rail with identity, active status, VP, Supply count, Development Card count, and only relevant badges
- ✅ Implemented: board-first match layout that keeps the 2D board as the main visual focus
- ✅ Implemented: right-side current-action panel for setup, roll gate, mandatory actions, active card effects, and end turn
- [Implemented] Direct contextual construction from legal board edges, open intersections, and owned Camps
- [Implemented] State-aware player control area with a compact Supply strip plus Trade, Cards, and Game Log drawers
- [Implemented] Centralized modern tabletop visual theme with teal/gold accents, off-white surfaces, calmer toasts, and consistent interactive states
- [Implemented] Icon-first side panels with accessible player stats, compact turn/setup/award markers, expandable player details, and state-specific Karo artwork
- [Implemented] Refined match hierarchy: single dice-result presentation, concise current-action states, and clearer active-player status
- [Implemented] Distinct compact groups for icon-first Supplies and secondary actions in the command dock
- [Implemented] Reduced board-toolbar weight while keeping the 2D/3D renderer choice visible
- [Implemented] Primary gameplay instructions and actions retain readable text; compact icons expose tooltips, focus states, and accessible names
- [Implemented] Separate UI/UX audit at `docs/ui-ux-audit.md`
- [Implemented] Separate visual design audit at `docs/visual-design-audit.md`

### Debug Mode

- ✅ Implemented: developer-only debug panel in Vite development mode
- ✅ Implemented: backend debug action guard for the Development environment
- ✅ Implemented: resource, dice, Warden, setup, turn, Development Card, win-check, and match restart debug controls
- ✅ Implemented: board debug overlays for tile, node, harbor, coordinate, and Warden inspection

### Board Generation Integrity

The supported map is the compact 19-region axial layout. Board generation is backend-authoritative, seeded, and validated before it is stored or broadcast. The validator checks terrain and number distributions, high-probability token spacing, full tile/node/edge topology, fixed coastal harbor slots and harbor types, and the Desert Warden start. See [the board-generation audit](docs/board-generation-audit.md) for the algorithm, reproduction details, and test coverage.

### 2D / 3D Board Renderers

- ✅ Implemented: 2D SVG board renderer as the stable default
- 🧪 Experimental: optional 3D renderer behind the `?board=3d` flag and board toggle
- 🧪 Experimental: 3D renderer is correctness-focused and intentionally not final art
- ✅ Implemented: fallback to 2D if the 3D renderer fails

### Developer Experience

- ✅ Implemented: Docker Compose local development stack
- ✅ Implemented: containerized API and Vite client startup with one command
- ✅ Implemented: Docker-friendly SignalR hub URL and configurable API CORS origins

## Gameplay Summary

Karo uses original terminology:

- Trail = road equivalent
- Camp = settlement equivalent
- Stronghold = city equivalent
- Supplies = resources
- Warden = robber equivalent
- Victory Points = win score

Karo supplies:

- Wood
- Clay
- Wool
- Grain
- Stone

Current costs:

- Trail = 1 Wood + 1 Clay
- Camp = 1 Wood + 1 Clay + 1 Wool + 1 Grain
- Stronghold = 2 Grain + 3 Stone
- Development Card = 1 Wool + 1 Grain + 1 Stone

Finite construction-piece supplies per player:

- 15 Trails
- 5 Camps
- 4 Strongholds

Setup Camps and Trails use these physical piece supplies. Paid normal-turn builds also consume pieces. Upgrading a Camp into a Stronghold consumes one Stronghold piece and returns the upgraded Camp piece to that player's supply. Road Building free Trails still consume available Trail pieces.

### Direct Construction

Normal paid construction starts on the board instead of in a Build drawer. After the current player rolls and resolves any mandatory action:

- Select an available edge to review and confirm a Trail.
- Select an available open intersection to review and confirm a Camp.
- Select one of your Camps to review and confirm a Stronghold upgrade.

The client only exposes targets that are currently legal, affordable, and supported by the player's remaining physical pieces. The compact confirmation shows the piece, Supply cost, and remaining piece count before any request is sent. The backend repeats every phase, ownership, connectivity, spacing, Supply, and piece check atomically.

Setup remains a free direct-placement flow and does not use paid-build affordability. Road Building also keeps direct edge placement without Wood or Clay, while still enforcing normal Trail connectivity and finite Trail pieces.

### Setup Phase

When the host starts a match, the backend creates the shared board and randomizes player order. Setup runs in two rounds:

- Round 1: players place a free Camp and connected Trail in forward order.
- Round 2: players place a free Camp and connected Trail in reverse order.
- The second setup Camp grants starting supplies from every adjacent productive region.

After setup completes, the first randomized player begins turn 1. Eligible Development Card action cards may be played before or after rolling, but building, trading, buying Development Cards, and ending the turn stay locked until the player rolls.

### Turn Flow

Normal turns currently follow this flow:

1. Current player may play one eligible Development Card action before rolling.
2. Current player rolls dice.
3. Backend resolves production from matching number tokens.
4. The Warden blocks production on its current region.
5. After rolling, the current player may use normal actions such as building, trading, buying Development Cards, or playing an eligible Development Card if they have not already used one this turn.
6. Current player ends the turn.
7. The next player starts with the roll gate closed.

### Trading

Karo supports two trade paths during a normal turn after the current player has rolled:

- Maritime/bank trade: the current player trades with the bank using the default 4:1 rate, a generic 3:1 harbor rate, or a matching resource-specific 2:1 harbor rate.
- Player trade: the current player may offer Supplies to one opponent and request Supplies in return. The target player may accept or reject the pending offer, and the proposer may cancel it.

Player trade offers exchange Supplies only. Development Cards, Victory Points, pieces, harbor access, and future promises are not tradeable. Pending offers expire when the current player ends the turn, Warden flow starts, or the match finishes. The backend revalidates both players' Supplies at acceptance time.

Trade controls use proactive availability checks before opening or submitting a flow. Maritime options show the best server-provided rate per Supply, its source, the owned quantity, and the required quantity. Player offers require a target plus at least one offered and requested Supply. Incoming Accept actions update from the latest viewer-aware game snapshot and disable when either side can no longer complete the exchange. These client checks improve feedback; the backend remains authoritative and performs the final atomic validation.

### Development Cards

Implemented Development Card types:

- Knight
- Road Building
- Year of Plenty
- Monopoly
- Victory Point

Development Cards cannot be bought or played during setup. Action Development Cards may be played before or after rolling during the current player's normal turn. Buying a Development Card still requires rolling first. Non-Victory Point action cards cannot be played on the same turn they were bought, and only one non-Victory Point Development Card can be played per turn.

The Development Card deck is finite and server-owned: 14 Knights, 2 Road Building, 2 Year of Plenty, 2 Monopoly, and 5 Victory Point cards. Buying a card removes the top card from the deck. If the deck is empty, purchases are blocked. Victory Point cards are passive, stay hidden from opponents, and count toward the owner's score immediately.

The Cards drawer shows exact purchase affordability for Wool, Grain, and Stone, deck availability, and one clear playability state per owned card. Invalid cards cannot open Warden, resource-selection, or free-Trail flows. Road Building also requires a remaining physical Trail and at least one legal placement before the card is consumed. Victory Point cards have no Play action and remain private. As with Trade, frontend availability is advisory and every purchase or play is revalidated atomically by the server.

Largest Army scoring is tied to played Knight cards. The first player with at least 3 played Knights gains +2 Victory Points. Another player can take Largest Army only by playing strictly more Knights than the current holder; ties do not transfer the bonus. Bought but unplayed Knight cards do not count.

Longest Trail scoring is tied to continuous connected Trails. The first player with a single continuous Trail path of at least 5 segments gains +2 Victory Points. Another player must exceed the current holder's length to take the bonus; ties do not transfer it. Your own Camps and Strongholds keep your network connected. Opponent Camps and Strongholds interrupt Trail traversal, though a Trail segment ending at that occupied node can still count as an endpoint.

### Warden

The Warden starts on the Desert/None region. A region occupied by the Warden produces no supplies.

When a 7 is rolled:

- No production happens.
- Players with more than 7 supplies discard half, rounded down.
- The current player moves the Warden to a different region.
- If an opponent has a Camp or Stronghold adjacent to the new Warden region and has supplies, the current player chooses that victim.
- The backend randomly transfers 1 supply from the victim to the current player.
- Normal actions stay blocked until the Warden flow is resolved.

Knight cards use the same Warden move and steal flow, but do not trigger the discard step.

## Architecture

The backend owns game state and rule validation. The frontend renders the current state, presents allowed actions, and sends player intent to the SignalR hub.

### Hybrid game assets

`Karo.Client/src/assets/game/gameAssets.ts` is the typed source of truth for resource, action, status, piece, card, terrain, and harbor artwork. Reusable `ResourceIcon`, `TerrainSymbol`, `ResourceAmount`, `ResourceStripItem`, `ResourceInlineSummary`, `ResourceCost`, `ActionIcon`, `PieceAsset`, `DevelopmentCardArtwork`, and `HarborIcon` components provide accessible labels and production-safe fallbacks. Exhaustive TypeScript mappings make missing enum assets a build failure instead of a silent blank.

WebP files are exported from maintainable `*.source.svg` files with `pnpm render:assets`. The five Supply symbols now share a filled illustrated style. Supply strips, inventory drawers, costs, trade summaries, Development Card purchase states, Warden discard controls, and development-only resource controls use the same icon-first presentation with accessible names and tooltips. Native select options and clear validation messages intentionally retain resource text. Board tiles use larger symbols without duplicate permanent names; both renderer legends are icon-only and accessible; and the Warden uses an original hooded sentinel silhouette. The stable 2D renderer never loads 3D models.

The asset system is production-safe but the art pipeline remains iterative. Camp, Stronghold, terrain surfaces, and Development Card illustrations are version-one replaceable repository art, not final commissioned artwork. The asset manifest intentionally keeps future replacement isolated from gameplay code.

### Visual system

The supported 2D experience uses a premium digital-tabletop theme defined in `Karo.Client/src/premium-theme.css`. The theme is loaded after the historical component stylesheet and centralizes the warm-stone background, parchment surfaces, carved-wood board frame, deep-teal interaction color, bronze highlights, typography hierarchy, compact side rails, and state-aware command strip.

The visual direction favors calm physical materials and clear game state over dashboard cards, pastel pills, or decorative UI noise. Player cards show icon/value summaries for Victory Points, Supplies, and Development Cards, with piece inventory and secondary statistics available through expansion. The contextual right rail uses one state-specific Karo asset, one short instruction, and icon/value chips; primary actions always retain their full label. Setup uses a dedicated Camp/Trail progress strip, while normal turns use a compact Supplies-and-actions command strip. The layout has been browser-checked from 1366x768 through 1920x1080 and stacks the board ahead of support panels below the desktop breakpoint.

```text
Player action
  -> SignalR hub
  -> Game service validation
  -> GameState update
  -> Broadcast to room
  -> React UI update
```

Important backend pieces:

- `Karo.Api/Hubs/GameLobbyHub.cs`: SignalR hub actions and room broadcasts
- `Karo.Api/Services/LobbyService.cs`: in-memory room/lobby management
- `Karo.Api/Services/RoomLifecycleService.cs`: session resume, reconnect pauses, host decisions, and post-game lifecycle coordination
- `Karo.Api/Services/RoomCleanupHostedService.cs`: periodic timeout and stale-room cleanup broadcasts
- `Karo.Api/Services/GameService.cs`: authoritative game rules and game state mutation
- `Karo.Api/Services/BoardGenerator.cs`: seeded shared board, vertices, edges, harbors, and ports
- `Karo.Api/Services/BoardIntegrityValidator.cs`: board invariant validation before match state is published
- `Karo.Api/Services/BoardGeometry.cs`: shared axial/corner geometry and normalized topology keys
- `Karo.Api/Services/DebugGameService.cs`: development-only debug actions
- `Karo.Api/DTOs/LobbyDtos.cs`: SignalR DTOs sent to the client
- `Karo.Api/Dockerfile`: backend container build for local Docker runs

Important frontend pieces:

- `Karo.Client/src/hooks/useLobbyConnection.ts`: SignalR connection and client actions
- `Karo.Client/src/services/playerSessionStore.ts`: sessionStorage-backed room identity for refresh/reconnect recovery
- `Karo.Client/src/components/GamePage.tsx`: match screen composition
- `Karo.Client/src/components/GameHeader.tsx`: compact match identity, room, phase, connection, and details trigger
- `Karo.Client/src/components/PlayerPanel.tsx`: scannable player rail and public player summaries
- `Karo.Client/src/components/MatchIconUI.tsx`: accessible icon stats, status markers, piece counts, contextual state artwork, and icon-plus-label action buttons
- `Karo.Client/src/components/GameBoard.tsx`: stable 2D SVG board
- `Karo.Client/src/components/HexTile.tsx`: terrain labels and illustrated resource motifs
- `Karo.Client/src/components/NumberToken.tsx`: physical number token rendering
- `Karo.Client/src/components/BoardRendererSwitch.tsx`: 2D/3D renderer switch and fallback boundary
- `Karo.Client/src/components/DevelopmentCardsPanel.tsx`: contextual turn controls, direct-build confirmation, state-aware command area, utility drawers, Warden panel, Trade, Game Log, and Development Cards
- `Karo.Client/src/utils/directBuild.ts`: centralized direct-construction gating and legal target derivation shared by the 2D and experimental 3D renderers
- `Karo.Client/src/utils/commandDock.ts`: command-area action visibility rules for setup, pre-roll, post-roll, mandatory actions, and finished matches
- `Karo.Client/src/utils/sidePanels.ts`: player status labels and right-panel current-action copy
- `Karo.Client/src/components/BankTradePanel.tsx`: maritime/bank trade and player-to-player trade UI
- `Karo.Client/src/types/game.ts`: TypeScript game contracts
- `Karo.Client/src/utils/gameErrors.ts`: user-facing SignalR/game validation error mapping
- `Karo.Client/Dockerfile`: frontend Vite container for local Docker runs
- `docker-compose.yml`: one-command full-stack local setup

## Running with Docker

Docker is the recommended setup for new contributors because it does not require a local .NET SDK or local Node.js installation.

Prerequisite:

- Docker Desktop

From the repository root:

```powershell
docker compose up --build
```

Local URLs:

- Frontend: [http://localhost:5173](http://localhost:5173)
- Backend: [http://localhost:5000](http://localhost:5000)
- Backend health: [http://localhost:5000/api/health](http://localhost:5000/api/health)
- SignalR hub: `http://localhost:5000/hubs/lobby`

Stop the stack:

```powershell
docker compose down
```

Rebuild from a clean image cache:

```powershell
docker compose build --no-cache
```

Follow logs:

```powershell
docker compose logs -f
```

The Docker frontend is configured to call the backend through the browser-facing URL `http://localhost:5000`, which keeps SignalR/WebSockets working from the host browser.

## Local Setup

Use this section if you prefer to run the apps directly on your machine instead of Docker.

### Prerequisites

- .NET SDK with `net10.0` support
- Node.js
- pnpm

If `pnpm` is not available, enable it with Corepack or install it globally:

```powershell
corepack enable
```

### Restore and Build

From the repository root:

```powershell
dotnet restore Karo.slnx --configfile NuGet.Config
dotnet build Karo.slnx --no-restore
```

Install frontend dependencies:

```powershell
cd Karo.Client
pnpm install
```

### Run the Backend

From the repository root:

```powershell
dotnet run --project Karo.Api\Karo.Api.csproj --urls http://localhost:5193
```

### Run the Frontend

In another terminal:

```powershell
cd Karo.Client
pnpm dev
```

Local URLs:

- Frontend: [http://127.0.0.1:5173](http://127.0.0.1:5173)
- Backend health: [http://localhost:5193/api/health](http://localhost:5193/api/health)
- SignalR hub: `http://localhost:5193/hubs/lobby`

### Run Checks

Backend rule harness:

```powershell
dotnet run --no-restore --configuration Release --project Karo.Tests\Karo.Tests.csproj -p:UseSharedCompilation=false
```

Frontend build:

```powershell
cd Karo.Client
pnpm build
```

## Docker Troubleshooting

- Port 5000 already in use: stop the process using that port or change the `5000:8080` mapping in `docker-compose.yml`.
- Port 5173 already in use: stop the existing Vite server or change the `5173:5173` mapping in `docker-compose.yml`.
- Docker Desktop not running: start Docker Desktop, wait for the engine to be ready, then rerun `docker compose up --build`.
- Frontend cannot connect to backend: confirm [http://localhost:5000/api/health](http://localhost:5000/api/health) responds and that `VITE_SIGNALR_HUB_URL` points to `http://localhost:5000/hubs/lobby`.
- SignalR connection issues: make sure the frontend is opened at `http://localhost:5173`; the API CORS policy allows `localhost:5173` and `127.0.0.1:5173` with credentials.
- Stale Docker build cache: run `docker compose build --no-cache`, then `docker compose up`.

## Debug Mode

Debug Mode is for local development only. The frontend only shows it in Vite development builds, and the backend rejects debug actions unless the API is running in the Development environment.

The Board Inspector can copy the active `boardSeed`, regenerate a match from a supplied seed, show tile/node/edge/harbor topology, and run the BoardIntegrityValidator on demand. Regeneration resets the match state and is development-only.

Enable Debug Mode with:

```text
http://127.0.0.1:5173/?debug=true
```

You can also set `localStorage.karoDebugMode=true` in the browser. Use `?debug=false` or the panel close button to disable it.

Debug Mode currently helps test:

- adding, setting, and clearing supplies
- forcing dice results, including 7
- resetting roll state
- forcing the current player
- moving or clearing the Warden
- skipping setup or forcing setup steps
- giving and clearing Development Cards
- resetting Development Card play limits
- inspecting Development Card deck composition
- drawing Development Cards from the real finite deck
- inspecting and recalculating Longest Trail
- setting Victory Points and triggering win checks
- restarting the match with the same players
- inspecting board IDs, nodes, harbors, Warden state, and trade rates

## 2D and 3D Board

The 2D SVG board is the stable default and the supported board renderer for the current MVP.

The 3D renderer is experimental. It uses the same backend-generated board state and is kept behind an explicit toggle for future visual exploration.

Open 3D directly with:

```text
http://127.0.0.1:5173/?board=3d
```

During a match, use the board toolbar to switch between `2D` and `3D Exp.`. If the 3D renderer fails, the app falls back to the 2D board.

## Multiplayer Lifecycle

Karo keeps one stable player identity for each room session. The browser stores the room code, player ID, and reconnect token in `sessionStorage`; the API stores only a SHA-256 token hash and never includes the token in public room or game broadcasts. On a refresh or SignalR reconnect, the client resumes the existing player rather than adding a duplicate player.

If the player currently required to act disconnects, the match pauses for the configured two-minute reconnect grace period. A non-required player can reconnect without interrupting the active action. After the grace period, a disconnected host transfers to the next eligible player in join order. The host can continue without a timed-out player, which forfeits that player while leaving their placed board pieces as neutral abandoned pieces, or end the paused match.

Players must mark themselves ready before the host can start a waiting room. In Development, Karo permits one to four connected players for local testing; non-Development environments enforce the configured three-to-four player range. A completed or ended match moves to `PostGame`, where the host can return connected, non-forfeited players to the same room code for a fresh rematch. This clears the old in-memory match and requires players to confirm readiness again.

Lifecycle timing is configurable in `Karo.Api/appsettings.json` under `Karo:Lifecycle`:

- `ReconnectGracePeriod` (default `00:02:00`)
- `FullyDisconnectedRoomTtl` (default `00:15:00`)
- `PostGameRoomTtl` (default `00:00:45`)
- `CleanupInterval` (default `00:00:15`)

See [the multiplayer lifecycle audit](docs/multiplayer-lifecycle-audit.md) and [manual playtest checklist](docs/multiplayer-lifecycle-playtest.md) for expected state transitions and test scenarios.

## Roadmap

Board generation hardening is implemented: the supported map is the standard compact 19-region layout with deterministic seeds, validation before broadcast, and 500-seed fuzz coverage. Custom maps and expansions remain planned.

### Phase 1 — Core Multiplayer Foundation

- ✅ Lobby rooms
- ✅ Live player sync
- ✅ Host start
- ✅ Shared board state

### Phase 2 — Base Game Rules

- ✅ Setup phase
- ✅ Turn flow
- ✅ Dice production
- ✅ Building validation and finite piece supplies
- ✅ Maritime trading
- ✅ Player-to-player Supply trading
- ✅ Warden on 7
- ✅ Development Cards

### Phase 3 — Rule Completion

- ✅ Largest Army after 3 played Knights
- ✅ Longest Trail after 5 connected Trails
- ✅ 10 Victory Point win validation
- ⏳ Match end screen

### Phase 4 — UX / Polish

- ✅ Progressive-disclosure match layout with compact rails, contextual actions, and on-demand drawers
- ✅ UI/UX audit document for the match experience
- [Implemented] Modern illustrated tabletop visual identity and 2D board art pass
- [Implemented] Premium tabletop theme layer with refined tokens, flatter panels, stronger hierarchy, and compact command strips
- [Implemented] Responsive browser validation at 1366x768, 1440x900, 1536x864, and 1920x1080
- [Implemented] Focused 2D board alignment and readable command dock correction
- [Implemented] State-aware command area that separates Supplies from contextual drawer actions
- [Implemented] Side-panel information hierarchy pass for player rail and current-action panel
- [Implemented] Icon-first side-panel redesign with expandable player details and context-specific setup, roll, Warden, card-effect, and finished-state artwork
- [Implemented] Proactive Trade and Development Card availability with exact disabled reasons
- [Implemented] Direct board construction with proactive legality, affordability, and physical-piece gating plus compact confirmation
- [Implemented] Focused match hierarchy polish for player summaries, current-action states, board toolbar, and command dock grouping
- [Implemented] Hybrid SVG and WebP game-asset manifest, cohesive Supply/status symbols, icon-first resource presentation across gameplay controls, refined Warden and harbor integration, reusable components, source art, and license documentation
- [Partial] Camp, Stronghold, terrain, and Development Card illustrations are version-one replaceable art pending a future final illustration pass
- [Experimental] Optional GLB model slots; the current 3D renderer still uses procedural fallbacks
- [Implemented] Visual design audit document for theme and board artwork
- 🟡 Board interaction polish
- ✅ User-facing validation and error feedback
- ⏳ Animations and feedback
- ⏳ Screenshots

### Phase 5 — Persistence / Portfolio Polish

- ⏳ SQL Server persistence if desired
- ⏳ Match history
- ⏳ Player stats
- 🟡 Tests: backend rule harness exists; broader automated test coverage planned
- 🟡 Architecture docs

### Phase 6 — Future Ideas

- ⏳ Bots
- ⏳ Spectator mode
- ⏳ Reconnect support
- ⏳ Replay/event history
- ⏳ Custom Karo cards
- ⏳ Expansions/custom modes

## Collaboration Guide

Suggested branch naming:

- `feature/...`
- `fix/...`
- `ui/...`
- `docs/...`

PR checklist:

- App builds successfully.
- Backend validation exists for new rules.
- Frontend disabled states match backend rules.
- SignalR room/game update flow still works.
- README is updated when behavior changes.
- Screenshots are included for UI changes when useful.
- New terminology follows Karo naming: Trail, Camp, Stronghold, Supplies, Warden, Victory Points.

## Known Limitations

- Game state is in memory and resets when the server restarts.
- No production deployment is configured yet.
- No database persistence yet.
- No authentication yet.
- Refresh/reconnect recovers only within the configured grace period while the same API process remains running; server restarts clear sessions and matches.
- Finite supply bank limits for loose resources are not implemented yet.
- Custom map sizes and expansion boards are not implemented; the supported map is the standard compact 19-region layout.
- The 3D board renderer is experimental and not final art.
- The 2D board combines procedural SVG/CSS with local SVG and WebP assets. Several raster illustrations are intentionally version-one and may be replaced by dedicated final art without changing gameplay or asset consumers.
- The premium theme is optimized for the supported 2D renderer. Future 3D art-direction work remains isolated and experimental.
- Some edge cases may still be incomplete as the MVP evolves.

## Legal Note

Karo is an original portfolio project inspired by the general genre of resource-trading hex-board strategy games. It does not use CATAN branding, official artwork, assets, or copied rulebook text.

## Project Structure

```text
Karo.Api/
  DTOs/       Lobby and game DTOs sent to the client
  Hubs/       SignalR lobby/game hub
  Models/     Room, player, board, tile, and game state models
  Services/   Lobby, game rules, board generation, and debug services

Karo.Client/
  src/components/  Landing, lobby, match, board, player, debug, and action UI
  src/hooks/       SignalR connection, debug mode, and renderer mode hooks
  src/types/       TypeScript lobby, game, debug, and renderer contracts

Karo.Tests/
  Program.cs       Lightweight backend rule checks
```
