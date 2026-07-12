# Multiplayer Lifecycle Manual Playtest

Run the API and client locally, then use two normal browser windows plus an optional third window. Use a private/incognito window for a distinct player session.

## Ready and Start

1. Create a room in window A and join it from window B.
2. Confirm the host cannot start until both players select **Ready**.
3. Confirm the lobby shows each player's connection and ready state.
4. Start the match and confirm ready badges reset.

Expected: the room starts only once all connected eligible players are ready.

## Refresh Recovery

1. In a live match, refresh window A.
2. Wait for the connection indicator to return to connected.
3. Confirm A returns as the same player with the same pieces, Supplies, private cards, and room membership.
4. Confirm no duplicate player appears in window B.

Expected: the existing player session resumes from `sessionStorage`; the room and game state stay synchronized.

## Current-Player Pause

1. Make player A the active setup or normal-turn player.
2. Close or stop the client connection for A.
3. In B, confirm the match displays a paused reconnect state and normal actions are blocked.
4. Reopen/refresh A before the grace period ends.

Expected: the pause identifies A and clears when A resumes. The exact setup/turn/Warden action remains unchanged.

## Non-Current Disconnect

1. Make player A the player required to act.
2. Close window B.
3. Continue the required action in A.

Expected: B is shown as reconnecting, but the match does not pause because B is not required for the current action.

## Grace Expiry and Host Migration

1. Disconnect the host while another connected player remains.
2. Wait past `Karo:Lifecycle:ReconnectGracePeriod` (temporarily reduce it in local configuration if needed).
3. Confirm the host becomes timed out and the next eligible connected player receives the host badge.
4. If the timed-out player is required to act, use **Continue without player** or **End paused match** from the host controls.

Expected: host transfer happens after, not before, the grace period. Continuing without a player forfeits only that player and leaves their placed board pieces.

## Leave and Forfeit

1. In a waiting room, select **Leave room**.
2. Confirm the player disappears and cannot refresh back into that room.
3. In an active match, attempt to leave and confirm the UI/rule flow requires an explicit forfeit.
4. Forfeit a player with at least one placed Camp or Trail.

Expected: an active player is removed from future turns and loses loose Supplies/private cards; existing pieces remain on the board.

## Post-Game Rematch

1. Finish a match or use the host-only paused-match end control.
2. Confirm the room is in post-game state.
3. Return to lobby.
4. Confirm connected, non-forfeited players remain under the same code, ready state resets, and a new ready/start sequence can launch a rematch.

Expected: the rematch uses a fresh match state; it does not reuse the previous game state.

## Stale Tab Protection

1. Open the same browser session in another tab while it is still connected.
2. Let the new tab resume the stored session.
3. Try an action from the old tab.

Expected: the old connection receives a session-replaced state and cannot perform actions. The new tab is the sole active connection for that player.

## Restart Boundary

1. Create or join a room.
2. Stop and restart the API.
3. Refresh the browser.

Expected: the in-memory room/session is gone. The player receives a friendly recovery error and must create or join a new room.
