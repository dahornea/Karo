# Karo

Real-time multiplayer hex strategy board game built with React, TypeScript, ASP.NET Core, and SignalR.

**Status:** MVP in active development.

Karo is an original portfolio project with its own naming, UI, assets, and implementation. It is inspired by the general genre of resource-trading hex-board strategy games, but it does not use CATAN branding, official artwork, assets, copied rulebook text, or copyrighted presentation.

## Overview

Karo is a browser-based multiplayer board game where players create private rooms, invite friends, place Camps and Trails, collect supplies, trade through the bank and coastal harbors, use Development Cards, move the Warden, and compete for Victory Points.

The current project is local-first and free to run. The backend keeps an in-memory authoritative game state, while the React client renders the shared board and sends player actions over SignalR. The goal is a playable, portfolio-quality multiplayer board game that can grow into a fuller production-style architecture over time.

## Tech Stack

- Frontend: React, TypeScript, Vite
- Styling: Tailwind CSS plus custom CSS for the board/game UI
- Backend: ASP.NET Core Web API
- Realtime: SignalR
- State/storage: in-memory lobby and game state for the current MVP
- Database: not implemented yet
- Board rendering: stable polished 2D SVG renderer, experimental lazy-loaded 3D renderer

## Features

### Multiplayer Lobby

- ✅ Implemented: private rooms with readable room codes
- ✅ Implemented: create room and join room flows
- ✅ Implemented: live player list updates through SignalR
- ✅ Implemented: host detection and host-only game start
- ✅ Implemented: disconnect handling, host reassignment, and empty room cleanup
- ⏳ Planned: durable reconnect support across server restarts

### Board

- ✅ Implemented: backend-generated shared 19-region hex board
- ✅ Implemented: Wood, Clay, Wool, Grain, Stone, and Desert/None regions
- ✅ Implemented: number tokens excluding 7
- ✅ Implemented: deterministic board vertices and edges for Camps, Trails, Strongholds, and harbor access
- ✅ Implemented: 9 fixed coastal harbor slots with randomized harbor types
- ✅ Implemented: Warden starts on the Desert/None region and blocks production

### Gameplay

- ✅ Implemented: initial setup phase with forward then reverse placement order
- ✅ Implemented: setup Camp placement, setup Trail placement, and one-empty-node Camp spacing
- ✅ Implemented: second setup Camp grants starting supplies from adjacent productive regions
- ✅ Implemented: normal turn roll gate
- ✅ Implemented: server-authoritative 2d6 dice roll and supply production
- ✅ Implemented: Camps produce 1 supply and Strongholds produce 2 supplies during production
- ✅ Implemented: Warden flow on 7, including discard, move, victim selection, and random steal
- ✅ Implemented: Victory Point scoring and win detection
- 🟡 Partial: Strongest Guard is awarded after 3 played Knights
- ⏳ Planned: paid normal-turn building actions for Trails, Camps, and Strongholds
- ⏳ Planned: Longest Trail-style scoring
- ⏳ Planned: polished match-end screen

### Development Cards

- ✅ Implemented: server-side shuffled Development Card deck
- ✅ Implemented: Development Card purchase cost and private hands
- ✅ Implemented: same-turn play restriction for action cards
- ✅ Implemented: one non-Victory Point Development Card per turn
- ✅ Implemented: Knight starts the Warden move/steal flow without discard
- ✅ Implemented: Year of Plenty adds exactly 2 selected supplies
- ✅ Implemented: Monopoly transfers the selected supply from opponents
- ✅ Implemented: Victory Point cards stay hidden and count toward win calculation
- 🟡 Partial: Road Building can be played and tracked as an active effect, but free Trail placement is not complete yet

### Trading

- ✅ Implemented: default 4:1 maritime/bank trade
- ✅ Implemented: generic 3:1 harbor rate
- ✅ Implemented: resource-specific 2:1 harbor rate
- ✅ Implemented: harbor access through adjacent Camp or Stronghold vertices
- ✅ Implemented: server-side trade validation and frontend disabled states
- ⏳ Planned: player-to-player trading

### Debug Mode

- ✅ Implemented: developer-only debug panel in Vite development mode
- ✅ Implemented: backend debug action guard for the Development environment
- ✅ Implemented: resource, dice, Warden, setup, turn, Development Card, win-check, and match restart debug controls
- ✅ Implemented: board debug overlays for tile, node, harbor, coordinate, and Warden inspection

### 2D / 3D Board Renderers

- ✅ Implemented: 2D SVG board renderer as the stable default
- 🧪 Experimental: optional 3D renderer behind the `?board=3d` flag and board toggle
- 🧪 Experimental: 3D renderer is correctness-focused and intentionally not final art
- ✅ Implemented: fallback to 2D if the 3D renderer fails

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

### Setup Phase

When the host starts a match, the backend creates the shared board and randomizes player order. Setup runs in two rounds:

- Round 1: players place a free Camp and connected Trail in forward order.
- Round 2: players place a free Camp and connected Trail in reverse order.
- The second setup Camp grants starting supplies from every adjacent productive region.

After setup completes, the first randomized player begins turn 1 and must roll before normal actions unlock.

### Turn Flow

Normal turns currently follow this flow:

1. Current player rolls dice.
2. Backend resolves production from matching number tokens.
3. The Warden blocks production on its current region.
4. After rolling, the current player may use available actions such as maritime trade or Development Cards.
5. Current player ends the turn.
6. The next player starts with the roll gate closed.

### Development Cards

Implemented Development Card types:

- Knight
- Road Building
- Year of Plenty
- Monopoly
- Victory Point

Development Cards cannot be bought or played during setup. The current MVP requires the player to roll before buying or playing Development Cards. Non-Victory Point action cards cannot be played on the same turn they were bought, and only one non-Victory Point Development Card can be played per turn.

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
- `Karo.Api/Services/GameService.cs`: authoritative game rules and game state mutation
- `Karo.Api/Services/BoardGenerator.cs`: shared board, vertices, edges, harbors, and ports
- `Karo.Api/Services/DebugGameService.cs`: development-only debug actions
- `Karo.Api/DTOs/LobbyDtos.cs`: SignalR DTOs sent to the client

Important frontend pieces:

- `Karo.Client/src/hooks/useLobbyConnection.ts`: SignalR connection and client actions
- `Karo.Client/src/components/GamePage.tsx`: match screen composition
- `Karo.Client/src/components/GameBoard.tsx`: stable 2D SVG board
- `Karo.Client/src/components/BoardRendererSwitch.tsx`: 2D/3D renderer switch and fallback boundary
- `Karo.Client/src/components/DevelopmentCardsPanel.tsx`: turn controls, utility drawers, Warden panel, and Development Cards
- `Karo.Client/src/types/game.ts`: TypeScript game contracts

## Local Setup

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

## Debug Mode

Debug Mode is for local development only. The frontend only shows it in Vite development builds, and the backend rejects debug actions unless the API is running in the Development environment.

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

## Roadmap

### Phase 1 — Core Multiplayer Foundation

- ✅ Lobby rooms
- ✅ Live player sync
- ✅ Host start
- ✅ Shared board state

### Phase 2 — Base Game Rules

- ✅ Setup phase
- ✅ Turn flow
- ✅ Dice production
- 🟡 Building validation: setup placement implemented; paid normal-turn building planned
- ✅ Maritime trading
- ✅ Warden on 7
- ✅ Development Cards, with Road Building placement still partial

### Phase 3 — Rule Completion

- ✅ Strongest Guard after 3 Knights
- ⏳ Longest Trail
- ✅ 10 Victory Point win validation
- ⏳ Match end screen

### Phase 4 — UX / Polish

- 🟡 Cleaner action panels
- 🟡 Board interaction polish
- ⏳ Animations and feedback
- ⏳ Screenshots

### Phase 5 — Persistence / Portfolio Polish

- ⏳ SQL Server persistence if desired
- ⏳ Match history
- ⏳ Player stats
- 🟡 Tests: backend rule harness exists; broader automated test coverage planned
- ⏳ Architecture docs

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
- Refresh/reconnect behavior is limited by the current in-memory lobby architecture.
- Paid normal-turn building actions are not complete yet.
- Road Building free Trail placement is not complete yet.
- Player-to-player trading is not implemented yet.
- The 3D board renderer is experimental and not final art.
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
