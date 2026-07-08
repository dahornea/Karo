# Karo

Karo is a browser-based multiplayer hex-board strategy game project. This repository currently implements **Milestone 1: Online Lobby Foundation**, **Milestone 2: Hex Board Renderer + Shared Board State**, and the first server-authoritative setup/turn-flow layer.

The app uses original Karo naming, theme, and UI. It does not use Catan branding, names, assets, exact board layout, copyrighted text, or direct rule wording.

## Tech Stack

- Backend: ASP.NET Core Web API + SignalR
- Frontend: React + TypeScript + Vite
- Styling: Tailwind CSS
- State: in-memory lobby and game state
- Database: not used yet

## Local Run Instructions

Restore and build the API:

```powershell
dotnet restore Karo.slnx --configfile NuGet.Config
dotnet build Karo.slnx --no-restore
```

Install and build the client:

```powershell
cd Karo.Client
pnpm install
pnpm build
```

Run the backend:

```powershell
dotnet run --project Karo.Api\Karo.Api.csproj --urls http://localhost:5193
```

Run the frontend in another terminal:

```powershell
cd Karo.Client
pnpm dev
```

Local URLs:

- Frontend: [http://127.0.0.1:5173](http://127.0.0.1:5173)
- Backend health: [http://localhost:5193/api/health](http://localhost:5193/api/health)
- SignalR hub: `http://localhost:5193/hubs/lobby`

## Milestone 1 Completed

- Create room
- Join room by 6-character room code
- Live player list
- Host detection
- Host-only start game action
- Start game event broadcast through SignalR
- Game screen transition after start
- Disconnect handling
- Host reassignment when host disconnects
- Empty room cleanup
- Clear lobby errors for missing name, invalid room code, room already in game, and non-host start attempts

## Milestone 2 Completed

- Shared generated board
- Compact 19-tile radius-2 axial hex map
- Backend-authoritative board generation
- Synchronized board for all players through the `GameStarted` SignalR event
- React SVG board renderer
- Distinct resource tiles for Wood, Clay, Wool, Grain, Stone, and neutral Desert
- Number tokens on producing tiles, excluding 7
- Game screen with match header, board area, player panel, and development-card panel
- Deterministic board vertices and edges for Camps, Strongholds, Trails, harbor access, and setup placement
- 9 deterministic coastal harbor slots attached to valid board vertices
- 9 visible coastal harbors around the shared board

## Resource Economy

Final Karo supplies:

- Wood
- Clay
- Wool
- Grain
- Stone

Official-style Karo costs:

- Trail: 1 Wood + 1 Clay
- Camp: 1 Wood + 1 Clay + 1 Wool + 1 Grain
- Stronghold: 2 Grain + 3 Stone
- Development Card: 1 Wool + 1 Grain + 1 Stone

## Harbors And Maritime Trading

The board topology includes 9 fixed coastal `HarborSlot` records. Each slot has a stable slot ID, two adjacent coastal vertex IDs, a coastal edge ID, tile edge coordinates, board-space render coordinates, and orientation. During backend board generation, Karo shuffles the harbor assignment list once per match and assigns exactly one Wood, Clay, Wool, Grain, and Stone 2:1 harbor plus four Generic 3:1 harbors to those fixed slots. All clients receive the same assigned `harborType` and `tradeRate` values from the shared board state. The renderer places harbor tokens in the surrounding water and connects each harbor back to its coastal nodes.

Karo boards include 9 backend-generated coastal harbors:

- 1 Wood 2:1 harbor
- 1 Clay 2:1 harbor
- 1 Wool 2:1 harbor
- 1 Grain 2:1 harbor
- 1 Stone 2:1 harbor
- 4 generic 3:1 harbors

Harbors are stored on the shared `BoardState` with a coastal tile edge and two adjacent board vertex IDs. A player gains access when they own a Camp or Stronghold on one of those adjacent vertices. Trails do not grant harbor access, and non-adjacent coastal structures do not count. The current best maritime trade rate is calculated server-side for every resource:

- Specific resource harbor: 2:1 for that resource
- Generic harbor: 3:1 for any resource
- Default bank: 4:1

`MaritimeTrade(roomCode, giveResource, receiveResource)` is server-authoritative and is available only during normal turns after the current player has rolled. Trading is blocked during setup, while Warden actions are pending, for non-current players, for same-resource trades, and when the player lacks the required supplies. The match UI shows harbor markers around the board and a Maritime Trade panel with per-resource rates, source labels, disabled states when supplies are insufficient, and the player's accessible harbor slots.

## Development Cards

Implemented card types:

- Knight
- Road Building
- Year of Plenty
- Monopoly
- Victory Point

Development Card cost:

- 1 Wool
- 1 Grain
- 1 Stone

The backend creates a shuffled 25-card development deck when a match starts:

- 14 Knight
- 2 Road Building
- 2 Year of Plenty
- 2 Monopoly
- 5 Victory Point

Development card hands are private. A player can see their own card types, while opponents see only card count and played Knight count. Victory Point cards count as hidden points for the owner and reveal through win/match-end state.

Current Karo MVP Development Card rules:

- Development Cards cannot be bought or played during setup.
- Buying a Development Card is allowed only on the current player's normal turn after rolling.
- Playing Knight, Road Building, Year of Plenty, or Monopoly also requires the current player to roll first.
- Non-Victory Point cards cannot be played on the same turn they were bought.
- A player can play at most one non-Victory Point card per turn.
- Victory Point cards are passive, hidden, do not use the play action, and count toward the owner's win calculation.

## Setup And Turn Flow

Implemented setup flow:

- Backend randomizes player order once when the host starts a match.
- Setup round 1 uses forward order.
- Setup round 2 uses reverse order.
- Each setup turn requires a free Camp, then a connected free Trail.
- Setup Camps must use empty nodes and follow the one-empty-node distance rule.
- A player's second setup Camp grants starting supplies from adjacent producing regions, excluding Desert and the blocked Warden region.
- After setup, the first randomized player begins turn 1 with no dice roll yet.

Normal-turn gating:

- The current player must roll dice before ending the turn, trading with the bank, or using development-card actions.
- Dice rolls are server-authoritative 2d6 rolls.
- Resource production is calculated on the backend from the shared board state.
- Camps produce 1 supply and Strongholds produce 2 supplies from matching unblocked producing regions.
- The next player starts with the roll gate closed again.

## Warden

The Warden is the Karo board blocker:

- The Warden starts on the Desert/None region.
- Regions occupied by the Warden do not produce supplies.
- Rolling 7 triggers the Warden flow instead of normal production.
- Players with more than 7 supplies must discard half of their supplies, rounded down.
- After required discards are complete, the current player moves the Warden to a different region.
- If opponents with adjacent Camps or Strongholds have supplies, the current player chooses one eligible victim and steals 1 random supply.
- If no eligible victim is available, the Warden flow completes automatically.
- Normal actions and End Turn remain blocked until discard, move, and steal resolution is complete.
- Knight cards use the same Warden move and steal flow without triggering the discard step.

## Debug Mode

Debug Mode is for local development only. It is disabled by default and backend debug actions are rejected unless `ASPNETCORE_ENVIRONMENT=Development`.

To enable it locally:

```powershell
dotnet run --project Karo.Api\Karo.Api.csproj --urls http://localhost:5193
cd Karo.Client
pnpm dev
```

Open the frontend with:

```text
http://127.0.0.1:5173/?debug=true
```

You can also set `localStorage.karoDebugMode=true` in the browser. Use `?debug=false` or the panel close button to disable it.

Debug Mode can:

- Add +1 or +5 resources to the selected player
- Set testing resources or clear resources
- Force a player's turn
- Force dice results from 2 through 12, including 7
- Reset roll state for the current turn
- Move the Warden or clear pending Warden state
- Skip the setup phase for fast local testing
- Force the setup placement step for a selected player
- Inspect tile, node, harbor, coordinate, Warden, and harbor-edge labels on the board
- Give or clear Development Cards
- Reset the Development Card play limit
- Load the remaining Development Card deck composition
- Set victory points, trigger win checks, and restart the match with the same players
- Inspect harbor assignments, accessible harbors, and current best maritime trade rates

Debug Mode is not for production, public play, or deployment. The frontend only renders the debug panel in Vite development builds, and the backend enforces the Development environment for every debug SignalR action.

## Experimental 3D Board Renderer

Karo includes an experimental visual-only 3D board renderer. The existing SVG board is the main supported board presentation, remains the default, and is the safe fallback if 3D is unavailable or fails.

Enable the 3D board locally with:

```text
http://127.0.0.1:5173/?board=3d
```

You can also switch between `2D` and `3D` from the board toolbar during a match. Debug Mode shows the active renderer in the Board section.

The 3D renderer:

- Uses the same backend-generated `GameState` and board topology as the 2D board
- Renders the 19 regions as a minimal, correctness-focused set of simple hex prisms
- Shows number tokens, harbors, the Warden, Trails, Camps, and Strongholds
- Uses backend harbor slot positions for coastal harbor markers
- Supports current setup placement and Warden tile selection through the existing handlers
- Is lazy-loaded so the default 2D experience does not load the Three.js stack

Current limitations:

- This is an experimental visual direction, not a gameplay rewrite.
- 2D remains the supported and most polished renderer.
- 3D is intentionally kept minimal for future visual exploration.
- 3D avoids decorative water, tray, and island-base experiments for now.
- 3D models are simple geometric placeholders, not final art assets.

## Not Implemented Yet

- Player-to-player trading
- Paid normal-turn building actions for Trails, Camps, and Strongholds
- Full Road Building Trail placement
- Authentication
- Database persistence
- Bots

## Known Limitations

- No official artwork/assets are used.
- This is a local-first MVP with in-memory state.
- Advanced rule parity is incomplete until paid building, player-to-player trading, and full Road Building Trail placement are implemented.
- Road Building can be played and tracked as an active effect, but free Trail placement still returns a clear limitation error.
- Knight uses the full Warden move/steal flow without the roll-7 discard step.
- Harbor access is fully modeled and tested through board vertices.

## Manual Test Checklist

- Start a match with 2 players.
- Complete setup by placing Camp + Trail for each player in forward order, then reverse order.
- Confirm second setup Camps grant starting supplies.
- Confirm normal actions are disabled before rolling.
- Roll dice and confirm normal turn actions unlock.
- Force or roll 7 and confirm Warden discard, move, and steal resolution blocks normal actions until complete.
- Buy a Development Card.
- Confirm opponents cannot see the exact card type.
- Play Year of Plenty.
- Play Monopoly.
- Play Knight and confirm it starts the Warden move/steal flow without discards.
- Confirm Strongest Guard is awarded after 3 Knights.
- Confirm a Victory Point card can help reach 10 Victory Points and trigger a win.
- Confirm the board displays 9 coastal harbors.
- Use the Maritime Trade panel to trade 4:1 without a harbor.
- In backend tests, confirm generic 3:1 and specific 2:1 harbor rates apply from adjacent Camp/Stronghold vertices.

## Project Structure

```text
Karo.Api/
  DTOs/       Lobby and game DTOs sent to the client
  Hubs/       GameLobbyHub SignalR endpoint
  Models/     Room, player, board, tile, and game state models
  Services/   In-memory LobbyService, GameService, DebugGameService, and BoardGenerator

Karo.Client/
  src/components/  Landing, lobby, match, player panel, and SVG board components
  src/hooks/       useLobbyConnection SignalR hook and debug activation hook
  src/types/       TypeScript lobby, game, and debug contracts

Karo.Tests/
  Program.cs       Lightweight backend rule checks
```
