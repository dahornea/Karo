# Multiplayer Lifecycle Audit

## Scope

This audit covers the current in-memory multiplayer lifecycle: stable room identity, reconnect behavior, disconnect consequences, host ownership, match pause/continue decisions, post-game rematches, and cleanup. It does not add database persistence, authentication, or cross-server coordination.

## Ownership Boundaries

| Area | Authority |
| --- | --- |
| Room membership, ready state, session validation, connection state, host migration, cleanup | `LobbyService` |
| Pause state, turn/setup/Warden requirement, forfeit effects, game state version | `GameService` |
| Coordination between lobby and match state | `RoomLifecycleService` |
| Timeout checks and room/game broadcasts | `RoomCleanupHostedService` |
| Transport, group membership, and DTO broadcasts | `GameLobbyHub` |
| Refresh recovery, reconnect UI, version-aware state application | `useLobbyConnection` |

The authoritative flow is:

```text
Browser action or connection change
  -> GameLobbyHub
  -> RoomLifecycleService / LobbyService / GameService
  -> authoritative room or match update
  -> versioned SignalR broadcast to the room
  -> React state update when the version is current
```

## Stable Session Contract

- Creating or joining a room creates a server-side `PlayerSession` keyed by stable `playerId`.
- The API generates a cryptographically random reconnect token and stores only its SHA-256 hash.
- The raw token is returned only by the create, join, or resume invocation and is never included in `RoomDto`, `GameStateDto`, room broadcasts, or other players' data.
- The client keeps `{ roomCode, playerId, reconnectToken, playerName }` in browser `sessionStorage` and invokes `ResumeRoomSession` after a refresh or SignalR reconnect.
- A successful resume replaces an older active connection for the same player and invalidates the old connection for further hub actions.
- Leaving a waiting/post-game room, forfeiting an active match, expiry, or room cleanup revokes the session token.

## Connection and Match State

Public player connection states are `Connected`, `Reconnecting`, `TimedOut`, `Left`, and `Forfeited`. The live game also exposes a nullable `pause` object and a monotonic `gameStateVersion`; rooms expose `roomStateVersion`.

When a player disconnects, the player remains in the room as `Reconnecting` until the configured grace period expires. Pending player-trade offers that involve that player are expired. The match pauses only when that player is currently required to act:

- the active setup player;
- the normal-turn current player;
- the player resolving a Warden movement or victim choice; or
- the next required Warden discard.

A reconnecting non-required player does not pause the match. A successful resume restores `Connected` state and clears a pause created for that player.

## Timeout, Host, Leave, and Forfeit Policy

`RoomCleanupHostedService` runs at the configured interval. Once a reconnect grace period expires, the player becomes `TimedOut` and cannot resume with the old token.

- A disconnected host remains host during the grace period.
- When the host times out, host ownership moves deterministically to the next eligible player by connection state then join time.
- In a waiting or post-game room, `LeaveRoom` removes the player immediately and revokes their session.
- In an active game, `LeaveRoom` is rejected; `ForfeitMatch` is explicit instead.
- Forfeit removes the player from turn/setup order, clears their loose Supplies and private Development Card hand, and preserves already placed board pieces as abandoned neutral pieces.
- The host can continue without a timed-out player or end a paused match. The active-player floor is one in Development for local testing and the configured minimum in non-Development environments.

## Post-Game and Cleanup Policy

When a match finishes or is ended, the room moves to `PostGame`. The host can return the room to `Waiting`:

- only connected, non-forfeited players remain;
- ready state resets;
- the old in-memory `GameState` is removed;
- the room code is retained; and
- starting again creates a new `matchId` and board state.

Rooms are removed when empty, after the fully-disconnected TTL, or after the post-game TTL. Removal revokes remaining session tokens and removes the game state.

## Configuration

`Karo.Api/appsettings.json` defines the defaults under `Karo:Lifecycle`:

| Setting | Default |
| --- | --- |
| `ReconnectGracePeriod` | 2 minutes |
| `FullyDisconnectedRoomTtl` | 15 minutes |
| `PostGameRoomTtl` | 45 seconds |
| `CleanupInterval` | 15 seconds |
| `MinimumPlayers` | 3 outside Development |
| `MaximumPlayers` | 4 |

## Verification Coverage

`Karo.Tests` covers session resume without duplicate players, readiness and player-count start gates, required-player pause/resume, non-required disconnect continuity, delayed host migration, explicit forfeit behavior, and return-to-lobby/rematch state. The existing gameplay tests continue to cover board generation, setup, Warden, Development Cards, trading, piece supplies, and scoring.

## Current Limits

- State, session hashes, and reconnect tokens are in memory. Restarting the API clears them.
- Lifecycle coordination uses process-local locking and is not designed for multi-instance deployment.
- The frontend applies newer room/game snapshots by version, but rule validation remains the final protection for simultaneous actions.
- There is no authenticated account recovery, durable match history, spectator state, or production deployment yet.
