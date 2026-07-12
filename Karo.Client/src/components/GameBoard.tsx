import { useMemo } from 'react';
import type { CSSProperties, ReactNode } from 'react';
import type { BoardEdge, BoardVertex, GameState, HarborSlot, TileResourceType } from '../types/game';
import { gameAssets } from '../assets/game/gameAssets';
import { TerrainSymbol } from './GameAsset';
import type { BoardDebugOptions } from '../types/debug';
import { defaultBoardDebugOptions } from '../types/debug';
import { HexTile } from './HexTile';
import type { DirectBuildSelection } from '../utils/directBuild';
import {
  directBuildLabels,
  getDirectBuildAvailability,
  getFreeTrailTargetIds,
  getSetupCampTargetIds,
  getSetupTrailTargetIds
} from '../utils/directBuild';

export interface GameBoardProps {
  game: GameState;
  playerId: string | null;
  debugOptions?: BoardDebugOptions;
  pendingAction?: string | null;
  toolbarAction?: ReactNode;
  selectedBuildTarget?: DirectBuildSelection | null;
  onSelectBuildTarget?: (selection: DirectBuildSelection) => void;
  onPlaceSetupCamp?: (roomCode: string, vertexId: string) => Promise<void>;
  onPlaceSetupTrail?: (roomCode: string, edgeId: string) => Promise<void>;
  onPlaceFreeTrail?: (roomCode: string, edgeId: string) => Promise<void>;
  onMoveWarden?: (roomCode: string, targetTileId: string) => Promise<void>;
}

const hexSize = 72;
const backendHexSize = 100;
const sqrtThree = Math.sqrt(3);
const boardScale = hexSize / backendHexSize;

const resourceLegend: Array<{ resource: TileResourceType; label: string }> = [
  { resource: 'Wood', label: 'Wood' },
  { resource: 'Clay', label: 'Clay' },
  { resource: 'Wool', label: 'Wool' },
  { resource: 'Grain', label: 'Grain' },
  { resource: 'Stone', label: 'Stone' },
  { resource: 'None', label: 'Desert' }
];

const playerColors = ['#d95f43', '#2f7f75', '#4269b2', '#d6a230'];

export function GameBoard({
  game,
  playerId,
  debugOptions = defaultBoardDebugOptions,
  pendingAction = null,
  toolbarAction,
  selectedBuildTarget = null,
  onSelectBuildTarget,
  onPlaceSetupCamp,
  onPlaceSetupTrail,
  onPlaceFreeTrail,
  onMoveWarden
}: GameBoardProps) {
  const layout = useMemo(() => {
    const positionedTiles = game.board.tiles.map((tile) => {
      const center = axialToPixel(tile.q, tile.r);
      return {
        tile,
        ...center,
        points: hexPoints(center.x, center.y)
      };
    });

    const landPoints = positionedTiles.flatMap((tile) =>
      tile.points.split(' ').map((point) => {
        const [x, y] = point.split(',').map(Number);
        return { x, y };
      })
    );
    const landBounds = getBounds(landPoints);
    const verticesById = new Map(
      game.board.vertices.map((vertex) => [
        vertex.vertexId,
        toScaledVertex(vertex)
      ])
    );
    const vertexLayouts = game.board.vertices.map((vertex) => ({
      vertex,
      ...toScaledVertex(vertex)
    }));
    const edgeLayouts = game.board.edges
      .map((edge) => {
        const start = verticesById.get(edge.startVertexId);
        const end = verticesById.get(edge.endVertexId);

        return start && end
          ? {
              edge,
              start,
              end,
              midpoint: {
                x: round((start.x + end.x) / 2),
                y: round((start.y + end.y) / 2)
              }
            }
          : null;
      })
      .filter((edge): edge is EdgeLayout => edge !== null);
    const currentPlayer = game.players.find((player) => player.playerId === playerId);
    const accessiblePortIds = new Set(currentPlayer?.accessiblePortIds ?? []);
    const accessibleHarborSlotIds = new Set(currentPlayer?.accessibleHarborSlotIds ?? []);
    const accessiblePortEdges = new Set(
      game.board.ports
        .filter((port) => accessiblePortIds.has(port.id))
        .map((port) => edgeKey(port.tileQ, port.tileR, port.edgeIndex))
    );
    const slotLayouts = game.board.harborSlots.map((slot) => {
      const rotation = (Math.PI / 180) * slot.orientationDegrees;
      const outward = {
        x: Math.cos(rotation),
        y: Math.sin(rotation)
      };
      const inward = {
        x: -outward.x,
        y: -outward.y
      };
      const fallbackEdge = getFallbackEdgePoints(slot);
      const start = verticesById.get(slot.adjacentVertexIds[0]) ?? fallbackEdge.start;
      const end = verticesById.get(slot.adjacentVertexIds[1]) ?? fallbackEdge.end;
      const midpoint = {
        x: (start.x + end.x) / 2,
        y: (start.y + end.y) / 2
      };
      const marker = {
        x: round(slot.renderX * boardScale + inward.x * 16),
        y: round(slot.renderY * boardScale + inward.y * 16)
      };
      const dock = {
        x: round(midpoint.x + outward.x * 18),
        y: round(midpoint.y + outward.y * 18)
      };
      const bridgeEnd = {
        x: round(marker.x + inward.x * 16),
        y: round(marker.y + inward.y * 16)
      };

      return {
        slot,
        start,
        end,
        dock,
        bridgeEnd,
        marker,
        artRotation: slot.orientationDegrees + 180,
        isOwned: accessibleHarborSlotIds.has(slot.harborSlotId)
          || accessiblePortIds.has(slot.harborSlotId)
          || accessiblePortEdges.has(edgeKey(slot.tileQ, slot.tileR, slot.edgeIndex))
      };
    });

    const allPoints = landPoints.concat(
      slotLayouts.flatMap((slot) => [
        slot.start,
        slot.end,
        slot.dock,
        slot.bridgeEnd,
        { x: slot.marker.x - 50, y: slot.marker.y - 40 },
        { x: slot.marker.x + 50, y: slot.marker.y + 40 }
      ])
    );

    const contentBounds = getBounds(allPoints);
    const waterBounds = expandBounds(contentBounds, 42, 38);
    const viewBounds = expandBounds(waterBounds, 18, 18);

    return {
      positionedTiles,
      vertexLayouts,
      edgeLayouts,
      slotLayouts,
      landBounds,
      waterBounds,
      viewBox: `${viewBounds.minX} ${viewBounds.minY} ${viewBounds.width} ${viewBounds.height}`
    };
  }, [game.board.edges, game.board.harborSlots, game.board.ports, game.board.tiles, game.board.vertices, game.players, playerId]);

  const canMoveWarden = game.phase === 'NormalTurn'
    && game.pendingWardenAction === 'MoveWarden'
    && game.currentWardenPlayerId === playerId
    && !pendingAction
    && !!onMoveWarden;
  const wardenTileId = game.wardenTileId ?? game.robberTileId;
  const setupCampTargets = useMemo(() => new Set(getSetupCampTargetIds(game, playerId, pendingAction)), [game, pendingAction, playerId]);
  const setupTrailTargets = useMemo(() => new Set(getSetupTrailTargetIds(game, playerId, pendingAction)), [game, pendingAction, playerId]);
  const freeTrailTargets = useMemo(() => new Set(getFreeTrailTargetIds(game, playerId, pendingAction)), [game, pendingAction, playerId]);
  const directBuildAvailability = useMemo(
    () => getDirectBuildAvailability(game, playerId, pendingAction),
    [game, pendingAction, playerId]
  );
  const buildTrailTargets = new Set(directBuildAvailability.actions.Trail.actionableTargetIds);
  const buildCampTargets = new Set(directBuildAvailability.actions.Camp.actionableTargetIds);
  const buildStrongholdTargets = new Set(directBuildAvailability.actions.Stronghold.actionableTargetIds);

  return (
    <section className="board-table">
      <div className="board-toolbar">
        <div className="board-identity">
          <h2><span className="sr-only">Karo </span>Island</h2>
        </div>
        <div className="board-toolbar-tools">
          <div className="resource-legend" aria-label="Resource legend">
            {resourceLegend.map((item) => (
              <span
                aria-label={item.label}
                className={`legend-chip legend-${item.resource.toLowerCase()}`}
                key={item.resource}
                role="img"
                tabIndex={0}
                title={item.label}
              >
                <TerrainSymbol decorative size="sm" type={item.resource} />
                <span className="sr-only">{item.label}</span>
              </span>
            ))}
          </div>
          {toolbarAction}
        </div>
      </div>

      <svg className="game-board-svg" viewBox={layout.viewBox} preserveAspectRatio="xMidYMid meet" role="img" aria-label="Generated Karo hex board">
        <TerrainDefs />
        <BoardWaterLayer bounds={layout.waterBounds} landBounds={layout.landBounds} />
        <g className="board-shadow">
          <ellipse
            cx={layout.landBounds.minX + layout.landBounds.width / 2}
            cy={layout.landBounds.maxY + 24}
            rx={layout.landBounds.width * 0.56}
            ry="46"
          />
        </g>
        <g className="coastline-layer">
          {layout.positionedTiles.map(({ points, tile }) => (
            <polygon className="hex-coastline" points={points} key={`coast-${tile.tileId}`} />
          ))}
        </g>
        <g>
          {layout.positionedTiles.map(({ tile, x, y, points }) => (
            <HexTile
              centerX={x}
              isWardenTarget={canMoveWarden && tile.tileId !== wardenTileId}
              key={tile.tileId}
              onMoveWarden={onMoveWarden}
              points={points}
              roomCode={game.roomCode}
              tile={tile}
              centerY={y}
            />
          ))}
        </g>
        <g className="trail-layer">
          {layout.edgeLayouts.map((edgeLayout) => (
            <TrailEdge
              game={game}
              isBuildTarget={buildTrailTargets.has(edgeLayout.edge.edgeId)}
              isSelectedBuildTarget={selectedBuildTarget?.type === 'Trail' && selectedBuildTarget.targetId === edgeLayout.edge.edgeId}
              isFreeTrailTarget={freeTrailTargets.has(edgeLayout.edge.edgeId)}
              isSetupTarget={setupTrailTargets.has(edgeLayout.edge.edgeId)}
              key={edgeLayout.edge.edgeId}
              onSelectBuildTarget={onSelectBuildTarget}
              onPlaceFreeTrail={onPlaceFreeTrail}
              onPlaceSetupTrail={onPlaceSetupTrail}
              {...edgeLayout}
            />
          ))}
        </g>
        <g className="build-node-layer">
          {layout.vertexLayouts.map((vertexLayout) => (
            <BuildNode
              game={game}
              isBuildCampTarget={buildCampTargets.has(vertexLayout.vertex.vertexId)}
              isBuildStrongholdTarget={buildStrongholdTargets.has(vertexLayout.vertex.vertexId)}
              isSelectedBuildTarget={selectedBuildTarget?.type !== 'Trail' && selectedBuildTarget?.targetId === vertexLayout.vertex.vertexId}
              isSetupTarget={setupCampTargets.has(vertexLayout.vertex.vertexId)}
              key={vertexLayout.vertex.vertexId}
              onSelectBuildTarget={onSelectBuildTarget}
              onPlaceSetupCamp={onPlaceSetupCamp}
              {...vertexLayout}
            />
          ))}
        </g>
        <g className="harbor-connector-layer">
          {layout.slotLayouts.map((slotLayout) => (
            <HarborConnector key={`connector-${slotLayout.slot.harborSlotId}`} {...slotLayout} />
          ))}
        </g>
        <g className="harbor-marker-layer">
          {layout.slotLayouts.map((slotLayout) => (
            <HarborMarker key={slotLayout.slot.harborSlotId} {...slotLayout} />
          ))}
        </g>
        <BoardDebugOverlay game={game} layout={layout} options={debugOptions} />
      </svg>
    </section>
  );
}

function axialToPixel(q: number, r: number) {
  return {
    x: hexSize * sqrtThree * (q + r / 2),
    y: hexSize * 1.5 * r
  };
}

function hexPoints(centerX: number, centerY: number) {
  return hexCorners(centerX, centerY)
    .map((point) => `${round(point.x)},${round(point.y)}`)
    .join(' ');
}

function hexCorners(centerX: number, centerY: number) {
  return Array.from({ length: 6 }, (_, index) => {
    const angle = (Math.PI / 180) * (30 + 60 * index);
    const x = centerX + hexSize * Math.cos(angle);
    const y = centerY + hexSize * Math.sin(angle);
    return { x: round(x), y: round(y) };
  });
}

function round(value: number) {
  return Math.round(value * 100) / 100;
}

function getBounds(points: Array<{ x: number; y: number }>) {
  const minX = Math.min(...points.map((point) => point.x));
  const maxX = Math.max(...points.map((point) => point.x));
  const minY = Math.min(...points.map((point) => point.y));
  const maxY = Math.max(...points.map((point) => point.y));

  return {
    minX: round(minX),
    maxX: round(maxX),
    minY: round(minY),
    maxY: round(maxY),
    width: round(maxX - minX),
    height: round(maxY - minY)
  };
}

function expandBounds(bounds: Bounds, xPadding: number, yPadding: number): Bounds {
  const minX = round(bounds.minX - xPadding);
  const maxX = round(bounds.maxX + xPadding);
  const minY = round(bounds.minY - yPadding);
  const maxY = round(bounds.maxY + yPadding);

  return {
    minX,
    maxX,
    minY,
    maxY,
    width: round(maxX - minX),
    height: round(maxY - minY)
  };
}

function toScaledVertex(vertex: BoardVertex) {
  return {
    x: round(vertex.x * boardScale),
    y: round(vertex.y * boardScale)
  };
}

function getFallbackEdgePoints(slot: HarborSlot) {
  const center = axialToPixel(slot.tileQ, slot.tileR);
  const corners = hexCorners(center.x, center.y);
  const startCorner = (5 - slot.edgeIndex + corners.length) % corners.length;

  return {
    start: corners[startCorner],
    end: corners[(startCorner + 1) % corners.length]
  };
}

function edgeKey(tileQ: number, tileR: number, edgeIndex: number) {
  return `${tileQ}:${tileR}:${edgeIndex}`;
}

function midpoint(start: { x: number; y: number }, end: { x: number; y: number }) {
  return {
    x: round((start.x + end.x) / 2),
    y: round((start.y + end.y) / 2)
  };
}

function normalizeVector(vector: { x: number; y: number }) {
  const length = Math.hypot(vector.x, vector.y);

  if (length === 0) {
    return { x: 1, y: 0 };
  }

  return {
    x: vector.x / length,
    y: vector.y / length
  };
}

function offsetPoint(point: { x: number; y: number }, vector: { x: number; y: number }, amount: number) {
  return {
    x: round(point.x + vector.x * amount),
    y: round(point.y + vector.y * amount)
  };
}

function lerpPoint(start: { x: number; y: number }, end: { x: number; y: number }, amount: number) {
  return {
    x: round(start.x + (end.x - start.x) * amount),
    y: round(start.y + (end.y - start.y) * amount)
  };
}

function pointsPath(points: Array<{ x: number; y: number }>) {
  return `${points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`).join(' ')} Z`;
}

function playerColor(game: GameState, ownerPlayerId: string | null) {
  if (!ownerPlayerId) {
    return '#8f7a58';
  }

  const playerIndex = game.players.findIndex((player) => player.playerId === ownerPlayerId);
  return playerColors[Math.max(0, playerIndex) % playerColors.length];
}

interface TrailEdgeProps extends EdgeLayout {
  game: GameState;
  isSetupTarget: boolean;
  isFreeTrailTarget: boolean;
  isBuildTarget: boolean;
  isSelectedBuildTarget: boolean;
  onPlaceSetupTrail?: (roomCode: string, edgeId: string) => Promise<void>;
  onPlaceFreeTrail?: (roomCode: string, edgeId: string) => Promise<void>;
  onSelectBuildTarget?: (selection: DirectBuildSelection) => void;
}

function TrailEdge({ game, edge, start, end, midpoint, isSetupTarget, isFreeTrailTarget, isBuildTarget, isSelectedBuildTarget, onPlaceSetupTrail, onPlaceFreeTrail, onSelectBuildTarget }: TrailEdgeProps) {
  const ownerColor = playerColor(game, edge.ownerPlayerId);
  const canPlace = (isSetupTarget && !!onPlaceSetupTrail) || (isFreeTrailTarget && !!onPlaceFreeTrail) || (isBuildTarget && !!onSelectBuildTarget);
  const placeTrail = () => {
    if (isSetupTarget) {
      void onPlaceSetupTrail?.(game.roomCode, edge.edgeId);
      return;
    }

    if (isFreeTrailTarget) {
      void onPlaceFreeTrail?.(game.roomCode, edge.edgeId);
      return;
    }

    onSelectBuildTarget?.({ type: 'Trail', targetId: edge.edgeId });
  };

  const actionLabel = isSetupTarget
    ? 'Place setup Trail on edge'
    : isFreeTrailTarget
      ? 'Place free Trail on edge'
      : 'Build Trail on edge';

  return (
    <g
      className="trail-edge"
      data-direct-build-selected={isSelectedBuildTarget}
      data-direct-build-target={isBuildTarget}
      data-owned={!!edge.ownerPlayerId}
      data-setup-target={isSetupTarget || isFreeTrailTarget}
      style={isBuildTarget ? ({ '--build-target-color': playerColor(game, game.currentPlayerId) } as CSSProperties) : undefined}
    >
      <title>{edge.ownerPlayerId ? `Trail owned by ${edge.ownerPlayerId}` : isSetupTarget ? 'Place setup Trail' : isFreeTrailTarget ? 'Place free Trail' : isBuildTarget ? directBuildLabels.Trail.tooltip : edge.edgeId}</title>
      <rect
        aria-label={canPlace ? actionLabel : undefined}
        className="trail-edge-hitbox"
        onClick={canPlace ? placeTrail : undefined}
        role={canPlace ? 'button' : undefined}
        tabIndex={canPlace ? 0 : undefined}
        onKeyDown={canPlace ? (event) => {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            placeTrail();
          }
        } : undefined}
        height="26"
        transform={`translate(${midpoint.x} ${midpoint.y}) rotate(${Math.atan2(end.y - start.y, end.x - start.x) * 180 / Math.PI})`}
        width={Math.hypot(end.x - start.x, end.y - start.y)}
        x={-Math.hypot(end.x - start.x, end.y - start.y) / 2}
        y="-13"
      />
      <line className="trail-edge-shadow" x1={start.x} y1={start.y + 2} x2={end.x} y2={end.y + 2} />
      <line className="trail-edge-base" x1={start.x} y1={start.y} x2={end.x} y2={end.y} style={{ stroke: ownerColor }} />
      <line className="trail-edge-highlight" x1={start.x} y1={start.y - 1.5} x2={end.x} y2={end.y - 1.5} />
    </g>
  );
}

interface BuildNodeProps {
  game: GameState;
  vertex: BoardVertex;
  x: number;
  y: number;
  isSetupTarget: boolean;
  isBuildCampTarget: boolean;
  isBuildStrongholdTarget: boolean;
  isSelectedBuildTarget: boolean;
  onPlaceSetupCamp?: (roomCode: string, vertexId: string) => Promise<void>;
  onSelectBuildTarget?: (selection: DirectBuildSelection) => void;
}

function BuildNode({ game, vertex, x, y, isSetupTarget, isBuildCampTarget, isBuildStrongholdTarget, isSelectedBuildTarget, onPlaceSetupCamp, onSelectBuildTarget }: BuildNodeProps) {
  const isBuildTarget = isBuildCampTarget || isBuildStrongholdTarget;
  const canPlace = (isSetupTarget && !!onPlaceSetupCamp) || (isBuildTarget && !!onSelectBuildTarget);
  const isOccupied = !!vertex.ownerPlayerId && !!vertex.structureType;
  const ownerColor = playerColor(game, vertex.ownerPlayerId);
  const placeNode = () => {
    if (isSetupTarget) {
      void onPlaceSetupCamp?.(game.roomCode, vertex.vertexId);
      return;
    }

    if (isBuildCampTarget) {
      onSelectBuildTarget?.({ type: 'Camp', targetId: vertex.vertexId });
      return;
    }

    onSelectBuildTarget?.({ type: 'Stronghold', targetId: vertex.vertexId });
  };

  const actionLabel = isSetupTarget
    ? 'Place setup Camp on intersection'
    : isBuildStrongholdTarget
      ? 'Upgrade Camp to Stronghold'
      : 'Build Camp on intersection';

  return (
    <g
      className="build-node"
      aria-label={canPlace ? actionLabel : undefined}
      data-coastal={vertex.isCoastal}
      data-direct-build-selected={isSelectedBuildTarget}
      data-direct-build-target={isBuildTarget}
      data-occupied={isOccupied}
      data-setup-target={isSetupTarget}
      style={isBuildTarget ? ({ '--build-target-color': playerColor(game, game.currentPlayerId) } as CSSProperties) : undefined}
      transform={`translate(${x} ${y})`}
      onClick={canPlace ? placeNode : undefined}
      role={canPlace ? 'button' : undefined}
      tabIndex={canPlace ? 0 : undefined}
      onKeyDown={canPlace ? (event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          placeNode();
        }
      } : undefined}
    >
      <title>
        {`${isBuildStrongholdTarget ? directBuildLabels.Stronghold.tooltip : isOccupied ? `${vertex.structureType} owned by ${vertex.ownerPlayerId}` : isSetupTarget ? 'Place setup Camp' : isBuildCampTarget ? directBuildLabels.Camp.tooltip : vertex.vertexId}
Adjacent tiles: ${getVertexAdjacentTileSummary(game, vertex)}`}
      </title>
      <circle className="build-node-ring" r={isSetupTarget ? 10.5 : isBuildTarget ? 7 : isOccupied ? 8.5 : 4.2} />
      {isOccupied ? <ellipse className="build-piece-color-base" cx="0" cy="8" rx={vertex.structureType === 'Stronghold' ? 12 : 10} ry="4.5" style={{ fill: ownerColor }} /> : null}
      {isOccupied ? (
        <image
          className={`build-piece-asset build-piece-asset-${vertex.structureType?.toLowerCase()}`}
          href={vertex.structureType === 'Stronghold' ? gameAssets.pieces.Stronghold.src : gameAssets.pieces.Camp.src}
          height={vertex.structureType === 'Stronghold' ? 34 : 29}
          width={vertex.structureType === 'Stronghold' ? 34 : 29}
          x={vertex.structureType === 'Stronghold' ? -17 : -14.5}
          y={vertex.structureType === 'Stronghold' ? -20 : -17}
        />
      ) : <circle className="build-node-camp" r={isSetupTarget ? 4.8 : isBuildTarget ? 3.4 : 2} />}
      {isSetupTarget || isSelectedBuildTarget ? <circle className="build-node-pulse" r="13" /> : null}
    </g>
  );
}

interface Bounds {
  minX: number;
  maxX: number;
  minY: number;
  maxY: number;
  width: number;
  height: number;
}

interface BoardWaterLayerProps {
  bounds: Bounds;
  landBounds: Bounds;
}

function BoardWaterLayer({ bounds, landBounds }: BoardWaterLayerProps) {
  const centerX = landBounds.minX + landBounds.width / 2;
  const centerY = landBounds.minY + landBounds.height / 2;
  const waterShape = waterFramePath(bounds);

  return (
    <g className="board-water-layer">
      <path className="water-frame-shadow" d={waterShape} />
      <path className="water-frame-base" d={waterShape} />
      <path className="water-ripple-fill" d={waterShape} />
      <ellipse
        className="island-water-glow"
        cx={centerX}
        cy={centerY}
        rx={landBounds.width * 0.62}
        ry={landBounds.height * 0.56}
      />
      <path
        className="water-current water-current-top"
        d={`M ${bounds.minX + 64} ${bounds.minY + 78} C ${bounds.minX + 150} ${bounds.minY + 36}, ${bounds.maxX - 170} ${bounds.minY + 42}, ${bounds.maxX - 70} ${bounds.minY + 82}`}
      />
      <path
        className="water-current water-current-bottom"
        d={`M ${bounds.minX + 74} ${bounds.maxY - 74} C ${bounds.minX + 190} ${bounds.maxY - 28}, ${bounds.maxX - 190} ${bounds.maxY - 38}, ${bounds.maxX - 76} ${bounds.maxY - 82}`}
      />
    </g>
  );
}

function waterFramePath(bounds: Bounds) {
  const { minX, minY, maxX, maxY, width, height } = bounds;
  const radius = Math.min(34, Math.max(22, Math.min(width, height) * 0.065));

  return [
    `M ${round(minX + radius)} ${minY}`,
    `H ${round(maxX - radius)}`,
    `Q ${maxX} ${minY} ${maxX} ${round(minY + radius)}`,
    `V ${round(maxY - radius)}`,
    `Q ${maxX} ${maxY} ${round(maxX - radius)} ${maxY}`,
    `H ${round(minX + radius)}`,
    `Q ${minX} ${maxY} ${minX} ${round(maxY - radius)}`,
    `V ${round(minY + radius)}`,
    `Q ${minX} ${minY} ${round(minX + radius)} ${minY}`,
    'Z'
  ].join(' ');
}

interface HarborLayout {
  slot: HarborSlot;
  start: { x: number; y: number };
  end: { x: number; y: number };
  dock: { x: number; y: number };
  bridgeEnd: { x: number; y: number };
  marker: { x: number; y: number };
  artRotation: number;
  isOwned: boolean;
}

function HarborConnector({ slot, start, end, dock, bridgeEnd, marker, isOwned }: HarborLayout) {
  const coastMidpoint = midpoint(start, end);
  const dockDirection = normalizeVector({
    x: marker.x - dock.x,
    y: marker.y - dock.y
  });
  const perpendicular = {
    x: -dockDirection.y,
    y: dockDirection.x
  };
  const dockStartLeft = offsetPoint(dock, perpendicular, 6.4);
  const dockStartRight = offsetPoint(dock, perpendicular, -6.4);
  const dockEndLeft = offsetPoint(bridgeEnd, perpendicular, 4.4);
  const dockEndRight = offsetPoint(bridgeEnd, perpendicular, -4.4);
  const mountPoint = offsetPoint(bridgeEnd, dockDirection, 6);
  const plankOne = lerpPoint(dock, bridgeEnd, 0.35);
  const plankTwo = lerpPoint(dock, bridgeEnd, 0.68);

  return (
    <g className="harbor-connector" data-owned={isOwned}>
      <title>{`${slot.harborSlotId} connects ${slot.adjacentVertexIds.join(' and ')}`}</title>
      <path
        className="harbor-shore-brace"
        d={`M ${start.x} ${start.y} Q ${dock.x} ${dock.y} ${end.x} ${end.y}`}
      />
      <path
        className="harbor-dock-shadow"
        d={pointsPath([
          offsetPoint(dockStartLeft, { x: 0, y: 1 }, 3),
          offsetPoint(dockEndLeft, { x: 0, y: 1 }, 3),
          offsetPoint(dockEndRight, { x: 0, y: 1 }, 3),
          offsetPoint(dockStartRight, { x: 0, y: 1 }, 3)
        ])}
      />
      <path
        className="harbor-dock-board"
        d={pointsPath([dockStartLeft, dockEndLeft, dockEndRight, dockStartRight])}
      />
      <line className="harbor-dock-board-edge" x1={dockStartLeft.x} y1={dockStartLeft.y} x2={dockEndLeft.x} y2={dockEndLeft.y} />
      <line className="harbor-dock-board-edge" x1={dockStartRight.x} y1={dockStartRight.y} x2={dockEndRight.x} y2={dockEndRight.y} />
      {[plankOne, plankTwo].map((plank, index) => (
        <line
          className="harbor-dock-plank"
          key={`${slot.harborSlotId}-plank-${index}`}
          x1={offsetPoint(plank, perpendicular, 6.6).x}
          y1={offsetPoint(plank, perpendicular, 6.6).y}
          x2={offsetPoint(plank, perpendicular, -6.6).x}
          y2={offsetPoint(plank, perpendicular, -6.6).y}
        />
      ))}
      <line className="harbor-mount-line" x1={bridgeEnd.x} y1={bridgeEnd.y} x2={mountPoint.x} y2={mountPoint.y} />
      <circle className="harbor-coastal-post" cx={start.x} cy={start.y} r="5.2" />
      <circle className="harbor-coastal-post" cx={end.x} cy={end.y} r="5.2" />
      <circle className="harbor-dock-cap" cx={coastMidpoint.x} cy={coastMidpoint.y} r="4.8" />
      <circle className="harbor-dock-cap harbor-dock-cap-end" cx={mountPoint.x} cy={mountPoint.y} r="4.2" />
    </g>
  );
}

function HarborMarker({ slot, marker, artRotation, isOwned }: HarborLayout) {
  const resourceClass = slot.harborType.toLowerCase();

  return (
    <g
      className={`harbor-marker harbor-marker-${resourceClass}`}
      data-owned={isOwned}
      transform={`translate(${marker.x} ${marker.y}) scale(0.66)`}
    >
      <title>
        {`${slot.harborSlotId} - ${slot.harborType} ${slot.tradeRate}:1 harbor on ${slot.adjacentEdgeId}; nodes ${slot.adjacentVertexIds.join(', ')}`}
      </title>
      <g className="harbor-plaque-mount" transform={`rotate(${artRotation})`}>
        <path className="harbor-plaque-tie" d="M-13 23 H13 L8 30 H-8 Z" />
        <circle className="harbor-plaque-rivet" cx="-7" cy="26" r="1.8" />
        <circle className="harbor-plaque-rivet" cx="7" cy="26" r="1.8" />
      </g>
      <path className="harbor-plaque-shadow" d={harborPlaquePath(3)} />
      <path className="harbor-plaque-back" d={harborPlaquePath(0)} />
      <path className="harbor-plaque-face" d="M-30 -18 H30 L36 -11 V14 L30 20 H-30 L-36 14 V-11 Z" />
      <path className="harbor-plaque-accent" d="M-29 -16 H29 L33 -11 V-8 H-33 V-11 Z" />
      <path className="harbor-plaque-notch" d="M-37 -3 H-33 M33 -3 H37 M-37 8 H-33 M33 8 H37" />
      <circle className="harbor-icon-disc" cx="-20" cy="3" r="12.2" />
      <image aria-hidden="true" className="harbor-resource-asset" href={gameAssets.harbors[slot.harborType].src} height="22" width="22" x="-31" y="-8" />
      <HarborLabel slot={slot} />
      {isOwned ? <circle className="harbor-owned-dot" cx="29" cy="-17" r="5" /> : null}
    </g>
  );
}

interface HarborLabelProps {
  slot: HarborSlot;
}

function HarborLabel({ slot }: HarborLabelProps) {
  const isGeneric = slot.harborType === 'Generic';

  return (
    <g className="harbor-label">
      <text className="harbor-label-rate" x="9" y={isGeneric ? -1 : 6}>{slot.tradeRate}:1</text>
      {isGeneric ? <text className="harbor-label-type" x="9" y="12">Any</text> : null}
    </g>
  );
}

function harborPlaquePath(yOffset: number) {
  return `M-35 ${-21 + yOffset} H35 Q42 ${-21 + yOffset} 42 ${-14 + yOffset} V15 Q42 ${22 + yOffset} 35 ${22 + yOffset} H-35 Q-42 ${22 + yOffset} -42 15 V-14 Q-42 ${-21 + yOffset} -35 ${-21 + yOffset} Z`;
}

interface BoardDebugOverlayProps {
  game: GameState;
  layout: BoardLayout;
  options: BoardDebugOptions;
}

type EdgeLayout = {
  edge: BoardEdge;
  start: { x: number; y: number };
  end: { x: number; y: number };
  midpoint: { x: number; y: number };
};

type BoardLayout = {
  positionedTiles: Array<{
    tile: GameState['board']['tiles'][number];
    x: number;
    y: number;
    points: string;
  }>;
  vertexLayouts: Array<{
    vertex: BoardVertex;
    x: number;
    y: number;
  }>;
  edgeLayouts: EdgeLayout[];
  slotLayouts: HarborLayout[];
  landBounds: Bounds;
  waterBounds: Bounds;
  viewBox: string;
};

function BoardDebugOverlay({ game, layout, options }: BoardDebugOverlayProps) {
  const hasTileLabels = options.showTileIds || options.showCoordinates || options.showRobberTileId;
  const hasNodeLabels = options.showNodeIds || options.showNodeDetails || options.showValidBuildPlacements;
  const hasEdgeLabels = options.showEdgeIds;
  const hasHarborLabels = options.showHarborSlotIds || options.showHarborDetails;

  if (!hasTileLabels && !hasNodeLabels && !hasEdgeLabels && !hasHarborLabels) {
    return null;
  }

  return (
    <g className="debug-board-overlay">
      {hasTileLabels ? (
        <g className="debug-tile-layer">
          {layout.positionedTiles.map(({ tile, x, y }) => (
            <g key={`debug-${tile.tileId}`}>
              {options.showTileIds ? (
                <text className="debug-map-label" x={x} y={y - 48}>
                  {tile.tileId}
                </text>
              ) : null}
              {options.showCoordinates ? (
                <text className="debug-map-label debug-map-label-muted" x={x} y={y + 54}>
                  q{tile.q}, r{tile.r}
                </text>
              ) : null}
              {options.showRobberTileId && tile.tileId === (game.wardenTileId ?? game.robberTileId) ? (
                <text className="debug-map-label debug-map-label-alert" x={x} y={y + 74}>
                  Warden: {tile.tileId}
                </text>
              ) : null}
            </g>
          ))}
        </g>
      ) : null}

      {hasNodeLabels ? (
        <g className="debug-node-layer">
          {layout.vertexLayouts.map(({ vertex, x, y }) => {
            const isOpenNode = !vertex.ownerPlayerId && !vertex.structureType;
            return (
              <g key={`debug-${vertex.vertexId}`}>
                {options.showValidBuildPlacements && isOpenNode ? (
                  <circle className="debug-valid-node" cx={x} cy={y} r="9" />
                ) : null}
                {options.showNodeIds ? (
                  <text className="debug-node-label" x={x} y={y - 11}>
                    {vertex.vertexId}
                  </text>
                ) : null}
                {options.showNodeDetails ? (
                  <text className="debug-node-label debug-node-detail" x={x} y={y + 14}>
                    {getVertexAdjacentTileSummary(game, vertex)}
                  </text>
                ) : null}
              </g>
            );
          })}
        </g>
      ) : null}

      {hasEdgeLabels ? (
        <g className="debug-edge-layer">
          {layout.edgeLayouts.map(({ edge, midpoint }) => (
            <text className="debug-edge-label" x={midpoint.x} y={midpoint.y} key={`debug-${edge.edgeId}`}>
              {edge.edgeId}
            </text>
          ))}
        </g>
      ) : null}

      {hasHarborLabels ? (
        <g className="debug-harbor-layer">
          {layout.slotLayouts.map(({ slot, marker, dock }) => (
            <g key={`debug-${slot.harborSlotId}`}>
              {options.showHarborSlotIds ? (
                <text className="debug-harbor-label" x={marker.x} y={marker.y - 44}>
                  {slot.harborSlotId}
                </text>
              ) : null}
              {options.showHarborDetails ? (
                <text className="debug-harbor-label debug-map-label-muted" x={marker.x} y={marker.y + 48}>
                  {slot.harborType} {slot.tradeRate}:1 | {slot.adjacentVertexIds.join(', ')}
                </text>
              ) : null}
            </g>
          ))}
        </g>
      ) : null}
    </g>
  );
}

function getVertexAdjacentTileSummary(game: GameState, vertex: BoardVertex) {
  return vertex.adjacentTileIds
    .map((tileId) => {
      const tile = game.board.tiles.find((candidate) => candidate.tileId === tileId);
      return tile ? `${tile.tileId}:${tile.resourceType}` : `${tileId}:?`;
    })
    .join(' | ');
}

function TerrainDefs() {
  return (
    <defs>
      <filter id="tile-depth" x="-20%" y="-20%" width="140%" height="140%">
        <feDropShadow dx="0" dy="4" stdDeviation="4" floodColor="#4c3522" floodOpacity="0.26" />
      </filter>

      <filter id="harbor-depth" x="-25%" y="-25%" width="150%" height="150%">
        <feDropShadow dx="0" dy="5" stdDeviation="4" floodColor="#1f342f" floodOpacity="0.3" />
      </filter>

      <radialGradient id="water-gradient" cx="48%" cy="45%" r="76%">
        <stop offset="0%" stopColor="#b9ddd9" />
        <stop offset="58%" stopColor="#7fb7b5" />
        <stop offset="100%" stopColor="#4f8f95" />
      </radialGradient>

      <pattern id="water-ripple-pattern" width="124" height="72" patternUnits="userSpaceOnUse">
        <path d="M-14 22 C5 11 24 31 43 19 C59 9 75 26 96 15 C107 10 118 13 134 18" fill="none" stroke="#eefbf7" strokeWidth="2.2" opacity="0.18" />
        <path d="M6 50 C22 41 41 57 60 45 C75 36 91 44 110 39" fill="none" stroke="#356f78" strokeWidth="1.8" opacity="0.1" />
        <path d="M34 5 C50 -3 66 12 82 5 C94 0 104 2 118 8" fill="none" stroke="#f8fff9" strokeWidth="1.7" opacity="0.12" />
      </pattern>

      <filter id="terrain-grain-noise" x="0%" y="0%" width="100%" height="100%">
        <feTurbulence type="fractalNoise" baseFrequency="0.8" numOctaves="2" seed="8" result="noise" />
        <feColorMatrix
          in="noise"
          type="matrix"
          values="0 0 0 0 0.55 0 0 0 0 0.47 0 0 0 0 0.32 0 0 0 0.12 0"
        />
      </filter>

      <linearGradient id="tile-sheen" x1="0%" y1="0%" x2="100%" y2="100%">
        <stop offset="0%" stopColor="#ffffff" stopOpacity="0.8" />
        <stop offset="44%" stopColor="#ffffff" stopOpacity="0.08" />
        <stop offset="100%" stopColor="#3a2919" stopOpacity="0.28" />
      </linearGradient>

      <radialGradient id="terrain-base-wood" cx="36%" cy="28%" r="78%">
        <stop offset="0%" stopColor="#4f8a56" />
        <stop offset="46%" stopColor="#245b37" />
        <stop offset="100%" stopColor="#123523" />
      </radialGradient>

      <radialGradient id="terrain-base-clay" cx="38%" cy="26%" r="76%">
        <stop offset="0%" stopColor="#db8f63" />
        <stop offset="48%" stopColor="#bd6844" />
        <stop offset="100%" stopColor="#7d3f33" />
      </radialGradient>

      <radialGradient id="terrain-base-wool" cx="38%" cy="24%" r="80%">
        <stop offset="0%" stopColor="#f1f3cf" />
        <stop offset="52%" stopColor="#dce9b1" />
        <stop offset="100%" stopColor="#adc77a" />
      </radialGradient>

      <radialGradient id="terrain-base-grain" cx="40%" cy="26%" r="78%">
        <stop offset="0%" stopColor="#efd06b" />
        <stop offset="52%" stopColor="#d0a540" />
        <stop offset="100%" stopColor="#997329" />
      </radialGradient>

      <radialGradient id="terrain-base-stone" cx="38%" cy="24%" r="80%">
        <stop offset="0%" stopColor="#c4cbc9" />
        <stop offset="50%" stopColor="#8d999b" />
        <stop offset="100%" stopColor="#58666c" />
      </radialGradient>

      <radialGradient id="terrain-base-none" cx="40%" cy="22%" r="82%">
        <stop offset="0%" stopColor="#ead7ab" />
        <stop offset="52%" stopColor="#d1b27a" />
        <stop offset="100%" stopColor="#a18152" />
      </radialGradient>

      <pattern id="terrain-texture-wood" width="180" height="156" patternUnits="userSpaceOnUse">
        <image href={gameAssets.terrain.Wood.src} width="180" height="156" preserveAspectRatio="xMidYMid slice" opacity="0.82" />
        <rect width="72" height="58" filter="url(#terrain-grain-noise)" opacity="0.38" />
        <path d="M-8 49 C7 31 18 42 28 17 C39 38 50 28 63 9 C68 19 73 21 80 11" stroke="#0d3020" strokeWidth="5" fill="none" opacity="0.4" />
        <path d="M9 48 L21 16 L34 48 Z M-4 42 L7 13 L21 42 Z M41 50 L53 18 L68 50 Z" fill="#113d27" opacity="0.5" />
        <path d="M13 47 V58 M21 45 V58 M55 48 V58" stroke="#092716" strokeWidth="2.2" opacity="0.34" />
        <circle cx="38" cy="25" r="5.2" fill="#0b2f1e" opacity="0.24" />
        <circle cx="62" cy="36" r="3.8" fill="#477d4e" opacity="0.14" />
      </pattern>

      <pattern id="terrain-texture-clay" width="180" height="156" patternUnits="userSpaceOnUse">
        <image href={gameAssets.terrain.Clay.src} width="180" height="156" preserveAspectRatio="xMidYMid slice" opacity="0.78" />
        <rect width="66" height="54" filter="url(#terrain-grain-noise)" opacity="0.24" />
        <path d="M0 13 H66 M0 33 H66 M16 0 V13 M43 13 V33 M23 33 V54 M58 33 V54" stroke="#74382f" strokeWidth="2.5" opacity="0.2" />
        <path d="M6 25 C17 13 29 36 43 22 C52 13 60 19 68 25" stroke="#edb184" strokeWidth="2" opacity="0.24" fill="none" />
        <path d="M11 44 L21 34 L33 44 M42 10 L51 4 L63 13" stroke="#5d3028" strokeWidth="1.7" opacity="0.15" fill="none" />
        <circle cx="51" cy="43" r="2.2" fill="#7f3628" opacity="0.16" />
      </pattern>

      <pattern id="terrain-texture-wool" width="180" height="156" patternUnits="userSpaceOnUse">
        <image href={gameAssets.terrain.Wool.src} width="180" height="156" preserveAspectRatio="xMidYMid slice" opacity="0.78" />
        <rect width="72" height="56" filter="url(#terrain-grain-noise)" opacity="0.1" />
        <path d="M-6 41 C10 25 26 43 42 26 C53 15 63 23 78 12" stroke="#86a75a" strokeWidth="2.4" fill="none" opacity="0.2" />
        <path d="M-8 22 C8 10 24 25 42 12 C54 3 65 8 78 0" stroke="#f7f8d6" strokeWidth="2.7" fill="none" opacity="0.34" />
        <path d="M8 49 L11 42 M26 47 L30 39 M48 46 L52 39 M62 35 L66 29" stroke="#7d9c54" strokeWidth="1.5" opacity="0.14" />
        <circle cx="35" cy="35" r="2.5" fill="#faf7da" opacity="0.22" />
      </pattern>

      <pattern id="terrain-texture-grain" width="180" height="156" patternUnits="userSpaceOnUse">
        <image href={gameAssets.terrain.Grain.src} width="180" height="156" preserveAspectRatio="xMidYMid slice" opacity="0.78" />
        <rect width="58" height="58" filter="url(#terrain-grain-noise)" opacity="0.2" />
        <path d="M6 60 V3 M20 62 V8 M36 60 V0 M53 60 V11" stroke="#8f6e25" strokeWidth="1.8" opacity="0.24" />
        <path d="M6 13 L14 6 M6 22 L15 15 M20 18 L30 9 M20 30 L30 21 M36 13 L46 5 M36 26 L49 16 M53 24 L61 17" stroke="#f9dd86" strokeWidth="2" opacity="0.34" />
        <path d="M-3 42 C12 32 27 45 41 34 C50 27 58 31 66 24" stroke="#b58932" strokeWidth="1.7" opacity="0.16" fill="none" />
      </pattern>

      <pattern id="terrain-texture-stone" width="180" height="156" patternUnits="userSpaceOnUse">
        <image href={gameAssets.terrain.Stone.src} width="180" height="156" preserveAspectRatio="xMidYMid slice" opacity="0.8" />
        <rect width="70" height="58" filter="url(#terrain-grain-noise)" opacity="0.23" />
        <path d="M3 47 L17 14 L29 39 L44 7 L68 46 Z" fill="#536269" opacity="0.23" />
        <path d="M-4 25 L10 10 L23 25 L33 16 L50 31 L73 12" stroke="#dce0dc" strokeWidth="2.4" opacity="0.24" fill="none" />
        <path d="M13 48 L22 29 M43 40 L52 18 M28 11 L35 27" stroke="#34434a" strokeWidth="1.8" opacity="0.2" />
        <circle cx="54" cy="44" r="3" fill="#d6dad6" opacity="0.15" />
      </pattern>

      <pattern id="terrain-texture-none" width="180" height="156" patternUnits="userSpaceOnUse">
        <image href={gameAssets.terrain.None.src} width="180" height="156" preserveAspectRatio="xMidYMid slice" opacity="0.78" />
        <rect width="68" height="56" filter="url(#terrain-grain-noise)" opacity="0.18" />
        <path d="M-3 36 C12 20 27 40 44 25 C55 16 62 22 72 28" stroke="#876d47" strokeWidth="2.4" opacity="0.18" fill="none" />
        <path d="M-5 17 C14 4 27 21 43 11 C54 4 62 8 72 14" stroke="#f0dbb0" strokeWidth="1.9" opacity="0.24" fill="none" />
        <circle cx="14" cy="15" r="2" fill="#86663b" opacity="0.18" />
        <circle cx="50" cy="43" r="2.5" fill="#f0d49b" opacity="0.28" />
      </pattern>
    </defs>
  );
}
