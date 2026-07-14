import type { BoardEdge, BoardVertex, GameState, PlayerGameState, ResourceType } from '../types/game';
import { resources } from '../types/game';

export type DirectBuildType = 'Trail' | 'Camp' | 'Stronghold';

export interface DirectBuildSelection {
  type: DirectBuildType;
  targetId: string;
}

export interface DirectBuildActionAvailability {
  actionableTargetIds: string[];
  disabledReason: string | null;
  isAffordable: boolean;
  legalTargetIds: string[];
}

export interface DirectBuildAvailability {
  actions: Record<DirectBuildType, DirectBuildActionAvailability>;
  baseDisabledReason: string | null;
  hasAnyAction: boolean;
}

export const directBuildCosts: Record<DirectBuildType, Partial<Record<ResourceType, number>>> = {
  Trail: { Wood: 1, Clay: 1 },
  Camp: { Wood: 1, Clay: 1, Wool: 1, Grain: 1 },
  Stronghold: { Grain: 2, Stone: 3 }
};

export const directBuildLabels: Record<DirectBuildType, { action: string; target: string; tooltip: string }> = {
  Trail: {
    action: 'Build Trail',
    target: 'edge',
    tooltip: 'Build Trail \u00b7 1 Wood + 1 Clay'
  },
  Camp: {
    action: 'Build Camp',
    target: 'intersection',
    tooltip: 'Build Camp \u00b7 1 Wood + 1 Clay + 1 Wool + 1 Grain'
  },
  Stronghold: {
    action: 'Upgrade to Stronghold',
    target: 'Camp',
    tooltip: 'Upgrade to Stronghold \u00b7 2 Grain + 3 Stone'
  }
};

export function getDirectBuildAvailability(
  game: GameState,
  playerId: string | null,
  pendingAction: string | null = null
): DirectBuildAvailability {
  const me = game.players.find((player) => sameId(player.playerId, playerId)) ?? null;
  const baseDisabledReason = getBaseDisabledReason(game, playerId, pendingAction, me);
  const legalTargetIds = me ? getLegalTargets(game, me) : emptyTargets();
  const actions = {
    Trail: createActionAvailability('Trail', legalTargetIds.Trail, me, baseDisabledReason),
    Camp: createActionAvailability('Camp', legalTargetIds.Camp, me, baseDisabledReason),
    Stronghold: createActionAvailability('Stronghold', legalTargetIds.Stronghold, me, baseDisabledReason)
  } satisfies Record<DirectBuildType, DirectBuildActionAvailability>;

  return {
    actions,
    baseDisabledReason,
    hasAnyAction: Object.values(actions).some((action) => action.actionableTargetIds.length > 0)
  };
}

export function isDirectBuildSelectionAvailable(
  availability: DirectBuildAvailability,
  selection: DirectBuildSelection
) {
  return availability.actions[selection.type].actionableTargetIds.includes(selection.targetId);
}

export function getSetupCampTargetIds(game: GameState, playerId: string | null, pendingAction: string | null) {
  const me = game.players.find((player) => sameId(player.playerId, playerId));
  if (game.phase !== 'Setup' || !sameId(game.currentSetupPlayerId, playerId) || pendingAction || game.setupStep !== 'PlaceCamp' || !me || me.remainingCamps <= 0) {
    return [];
  }

  return game.board.vertices
    .filter((vertex) => isValidCampSpacingTarget(vertex, game.board.vertices, game.board.edges))
    .map((vertex) => vertex.vertexId);
}

export function getSetupTrailTargetIds(game: GameState, playerId: string | null, pendingAction: string | null) {
  const me = game.players.find((player) => sameId(player.playerId, playerId));
  if (game.phase !== 'Setup' || !sameId(game.currentSetupPlayerId, playerId) || pendingAction || game.setupStep !== 'PlaceTrail' || !game.lastSetupCampVertexId || !me || me.remainingTrails <= 0) {
    return [];
  }

  return game.board.edges
    .filter((edge) => !edge.ownerPlayerId && edgeTouchesVertex(edge, game.lastSetupCampVertexId!))
    .map((edge) => edge.edgeId);
}

export function getFreeTrailTargetIds(game: GameState, playerId: string | null, pendingAction: string | null) {
  const me = game.players.find((player) => sameId(player.playerId, playerId));
  const canPlace = game.phase === 'NormalTurn'
    && sameId(game.currentPlayerId, playerId)
    && game.pendingWardenAction === 'None'
    && game.activeDevelopmentCardEffect?.type === 'RoadBuilding'
    && !pendingAction
    && !!me
    && me.remainingTrails > 0;

  if (!canPlace) {
    return [];
  }

  return game.board.edges
    .filter((edge) => !edge.ownerPlayerId && isConnectedTrailTarget(game, playerId, edge))
    .map((edge) => edge.edgeId);
}

function createActionAvailability(
  type: DirectBuildType,
  legalTargetIds: string[],
  me: PlayerGameState | null,
  baseDisabledReason: string | null
): DirectBuildActionAvailability {
  const pieceReason = me ? getPieceReason(type, me) : 'Player state is unavailable.';
  const supplyReason = me ? getSupplyReason(directBuildCosts[type], me) : 'Player state is unavailable.';
  const targetReason = legalTargetIds.length === 0 ? getNoTargetReason(type) : null;
  const disabledReason = baseDisabledReason ?? pieceReason ?? supplyReason ?? targetReason;

  return {
    actionableTargetIds: disabledReason ? [] : legalTargetIds,
    disabledReason,
    isAffordable: !pieceReason && !supplyReason,
    legalTargetIds
  };
}

function getBaseDisabledReason(
  game: GameState,
  playerId: string | null,
  pendingAction: string | null,
  me: PlayerGameState | null
) {
  if (!me || !playerId) return 'Player state is unavailable.';
  if (game.status === 'Finished' || game.phase === 'Finished') return 'This match has finished.';
  if (game.phase !== 'NormalTurn') return 'Normal building is unavailable during setup.';
  if (!sameId(game.currentPlayerId, playerId)) return 'It is not your turn.';
  if (game.pendingWardenAction !== 'None') return 'Resolve the Warden action first.';
  if (game.activeDevelopmentCardEffect) return 'Resolve the current Development Card action first.';
  if (!game.hasRolledThisTurn) return 'Roll before building.';
  if (pendingAction) return 'Another action is resolving.';
  return null;
}

function getLegalTargets(game: GameState, me: PlayerGameState): Record<DirectBuildType, string[]> {
  return {
    Trail: game.board.edges
      .filter((edge) => !edge.ownerPlayerId && isConnectedTrailTarget(game, me.playerId, edge))
      .map((edge) => edge.edgeId),
    Camp: game.board.vertices
      .filter((vertex) => isValidCampSpacingTarget(vertex, game.board.vertices, game.board.edges))
      .filter((vertex) => game.board.edges.some((edge) => edgeTouchesVertex(edge, vertex.vertexId) && sameId(edge.ownerPlayerId, me.playerId)))
      .map((vertex) => vertex.vertexId),
    Stronghold: game.board.vertices
      .filter((vertex) => sameId(vertex.ownerPlayerId, me.playerId) && vertex.structureType === 'Camp')
      .map((vertex) => vertex.vertexId)
  };
}

function isValidCampSpacingTarget(vertex: BoardVertex, vertices: BoardVertex[], edges: BoardEdge[]) {
  if (vertex.ownerPlayerId || vertex.structureType) {
    return false;
  }

  return !edges
    .filter((edge) => edgeTouchesVertex(edge, vertex.vertexId))
    .some((edge) => {
      const otherVertexId = sameId(edge.startVertexId, vertex.vertexId) ? edge.endVertexId : edge.startVertexId;
      const otherVertex = vertices.find((candidate) => sameId(candidate.vertexId, otherVertexId));
      return !!otherVertex?.ownerPlayerId && !!otherVertex.structureType;
    });
}

function isConnectedTrailTarget(game: GameState, playerId: string | null, edge: BoardEdge) {
  return !!playerId && (
    canConnectTrailAtVertex(game, playerId, edge.startVertexId)
    || canConnectTrailAtVertex(game, playerId, edge.endVertexId)
  );
}

function canConnectTrailAtVertex(game: GameState, playerId: string, vertexId: string) {
  const vertex = game.board.vertices.find((candidate) => sameId(candidate.vertexId, vertexId));

  if (vertex?.ownerPlayerId && vertex.structureType && !sameId(vertex.ownerPlayerId, playerId)) {
    return false;
  }

  if (vertex?.structureType && sameId(vertex.ownerPlayerId, playerId)) {
    return true;
  }

  return game.board.edges
    .filter((edge) => edgeTouchesVertex(edge, vertexId))
    .some((edge) => sameId(edge.ownerPlayerId, playerId));
}

function getPieceReason(type: DirectBuildType, me: PlayerGameState) {
  if (type === 'Trail' && me.remainingTrails <= 0) return 'No Trail pieces remaining.';
  if (type === 'Camp' && me.remainingCamps <= 0) return 'No Camp pieces remaining.';
  if (type === 'Stronghold' && me.remainingStrongholds <= 0) return 'No Stronghold pieces remaining.';
  return null;
}

function getSupplyReason(cost: Partial<Record<ResourceType, number>>, me: PlayerGameState) {
  const missing = resources
    .map((resource) => ({ resource, amount: Math.max(0, (cost[resource] ?? 0) - (me.supplies[resource] ?? 0)) }))
    .filter((entry) => entry.amount > 0);

  if (missing.length === 0) return null;
  return `Need ${missing.map((entry) => `${entry.amount} ${entry.resource}`).join(' and ')}.`;
}

function getNoTargetReason(type: DirectBuildType) {
  if (type === 'Trail') return 'No legal Trail edges are available.';
  if (type === 'Camp') return 'No legal Camp intersections are available.';
  return 'No Camps are available to upgrade.';
}

function emptyTargets(): Record<DirectBuildType, string[]> {
  return { Trail: [], Camp: [], Stronghold: [] };
}

function edgeTouchesVertex(edge: BoardEdge, vertexId: string) {
  return sameId(edge.startVertexId, vertexId) || sameId(edge.endVertexId, vertexId);
}

function sameId(left: string | null | undefined, right: string | null | undefined) {
  return Boolean(left && right && left.toLocaleLowerCase() === right.toLocaleLowerCase());
}
