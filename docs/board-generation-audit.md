# Board Generation Audit

## Scope

Karo creates one backend-authoritative compact land board per match. This audit covers the standard radius-2 axial map, terrain and token assignment, topology, harbors, and the initial Warden placement. It does not add custom maps, expansion boards, a finite Supply bank, or client-side game-rule generation.

## Current Implementation

`BoardGenerator` creates all axial coordinates where `max(abs(q), abs(r), abs(-q-r)) <= 2`, ordered by row. This produces the required 3-4-5-4-3 layout and exactly 19 connected land tiles.

One `System.Random` instance is created from `BoardState.BoardSeed` and is used for the terrain shuffle, valid number-token placement, and harbor-type shuffle. Development Card deck and player-order randomness are deliberately separate from the board seed.

The generated board exposes authoritative relationships used by gameplay:

- `HexTile.AdjacentTileIds` comes from the six axial neighbor directions.
- `BoardVertex.AdjacentTileIds` comes from every tile corner that resolves to that physical position.
- `BoardEdge.AdjacentTileIds` records whether the edge is coastal (one tile) or interior (two tiles).
- `BoardVertex.AdjacentVertexIds` and `AdjacentEdgeIds` are derived from board edges.
- Harbor slots reference one fixed coastal edge and its two exact coastal vertices.

The stable 2D renderer derives tile positions from `q`/`r` and consumes backend vertex, edge, harbor, and Warden IDs. It does not recreate topology for rule decisions.

## Problems Found

The original generator had a few correctness risks:

- terrain, number tokens, and harbors used `Random.Shared`, so a board could not be reproduced from a match value;
- random number assignment could place a 6 beside a 6 or 8;
- tile, node, and edge relationships were implicit rather than a validated shared contract;
- the corner key formatted floating-point `-0` and `0` differently. That split some identical physical corners into duplicate nodes, which caused incomplete node-to-tile adjacency and invalid edge connectivity;
- an invalid generated board had no complete validation gate before game state publication.

The signed-zero defect was fixed by normalizing rounded point coordinates to integer key components before deduplication.

## Invariants

`BoardIntegrityValidator` validates a board immediately after generation and again after the game initializes the Warden. It checks:

- 19 unique radius-2 axial coordinates and a connected tile graph;
- terrain distribution: 4 Wood, 3 Clay, 4 Wool, 4 Grain, 3 Stone, and 1 Desert;
- exactly 18 production tokens with the required 2-12 frequency map, no 7, and no Desert token;
- no 6 or 8 sharing a tile edge with another 6 or 8;
- symmetric, duplicate-free tile adjacency;
- unique physical vertices with correct touching-tile sets, degrees, and coastal status;
- unique physical edges with valid endpoints, one/two touching tiles, and symmetric node connectivity;
- nine unique coastal harbor slots, their fixed coastal attachments, and the required 5 specific 2:1 plus 4 Generic 3:1 distribution;
- matching legacy port records used by current game consumers;
- the initialized Warden referencing and blocking the unique Desert tile.

When validation fails, `BoardGenerator` throws a seed-bearing `BoardGenerationException`. `GameService` logs the seed and errors, rejects the start with a controlled player-facing error, and does not add the temporary game to in-memory state. The hub rolls the room back to `Waiting` rather than leaving an invalid match in progress.

## Generation Algorithm

1. Create the fixed radius-2 axial coordinate set.
2. Shuffle the exact terrain bag with the board-seeded random source.
3. Derive symmetric tile adjacency by axial lookup.
4. Use backtracking to place 6, 6, 8, and 8 on non-adjacent productive tiles; shuffle the remaining required number tokens onto the remaining productive tiles.
5. Resolve all six tile corners into normalized physical vertices, then derive unique physical edges from normalized endpoint pairs.
6. Populate node-to-node and node-to-edge relationships from those edges.
7. Identify the 30 coastal edges. Select the existing fixed nine coastal slot geometries and shuffle only their harbor types.
8. Validate the temporary `BoardState` before it can leave the generator.
9. Initialize the Warden on Desert and validate the resulting starting state before `GameService` stores or broadcasts `GameState`.

## Seed Reproduction

Every board carries an integer `boardSeed` in the shared board DTO. Calling the generator with the same seed produces the same terrain placement, number tokens, vertices, edges, harbor types, and harbor slots.

In Development, the Board Inspector can copy the current seed, regenerate the match from a supplied seed, and request a fresh integrity result. Regeneration intentionally creates a fresh match state with the same room players; it is a developer tool, not a reconnect or persistence mechanism.

## Verification

The lightweight backend rule harness includes:

- a deterministic same-seed snapshot test;
- targeted tile/node/edge/coast topology checks;
- resource string serialization checks;
- starting-Supply, dice-production, and harbor-access regressions in gameplay tests;
- a 500-seed deterministic fuzz run asserting validator success, distributions, topology, harbors, and Desert Warden placement.

Run it with:

```powershell
dotnet run --project Karo.Tests/Karo.Tests.csproj
```

## Remaining Limitations

- The board shape is intentionally fixed at the standard compact 19-region map.
- Board state is in memory and disappears on API restart.
- Board seed reproduction does not reproduce Development Card deck order or player order.
- The stable 2D renderer is the supported presentation. The optional 3D renderer remains experimental and is outside this audit.
