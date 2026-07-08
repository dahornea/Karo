import { Canvas, useThree } from '@react-three/fiber';
import type { ThreeEvent } from '@react-three/fiber';
import { ContactShadows, Html, MapControls, OrthographicCamera } from '@react-three/drei';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { PointerEvent as ReactPointerEvent, ReactNode } from 'react';
import type { BoardEdge, BoardVertex, GameState, HarborSlot, HexTile as HexTileModel, TileResourceType } from '../types/game';
import type { BoardDebugOptions } from '../types/debug';
import { defaultBoardDebugOptions } from '../types/debug';

interface Board3DRendererProps {
  game: GameState;
  playerId: string | null;
  debugOptions?: BoardDebugOptions;
  pendingAction?: string | null;
  toolbarAction?: ReactNode;
  onPlaceSetupCamp?: (roomCode: string, vertexId: string) => Promise<void>;
  onPlaceSetupTrail?: (roomCode: string, edgeId: string) => Promise<void>;
  onMoveWarden?: (roomCode: string, targetTileId: string) => Promise<void>;
}

type Vec2 = { x: number; z: number };
type CameraPresetId = 'default' | 'top' | 'table';
type CameraCommand = { preset: CameraPresetId; revision: number };
type CameraPreset = {
  label: string;
  position: [number, number, number];
  target: [number, number, number];
  zoom: number;
};
type BoardVector3Ref = {
  x: number;
  y: number;
  z: number;
  set: (x: number, y: number, z: number) => BoardVector3Ref;
};
type BoardCameraRef = {
  position: BoardVector3Ref;
  zoom: number;
  updateProjectionMatrix: () => void;
};
type BoardControlsRef = {
  target: BoardVector3Ref;
  update: () => void;
};

const tileRadius = 1.28;
const backendHexSize = 100;
const coordinateScale = tileRadius / backendHexSize;
const sqrtThree = Math.sqrt(3);
const tileHeight = 0.4;
const hexCornerStartDegrees = 30;
const landTileMeshRadius = tileRadius;
const landTileSurfaceRadius = tileRadius;
const cameraDragThreshold = 6;
const panClamp = {
  x: 3.6,
  z: 3.25
};
const cameraPresets: Record<CameraPresetId, CameraPreset> = {
  default: {
    label: 'Reset',
    position: [4.8, 13.6, 5.0],
    target: [0, 0, 0],
    zoom: 46
  },
  top: {
    label: 'Top',
    position: [0.4, 15, 0.4],
    target: [0, 0, 0],
    zoom: 50
  },
  table: {
    label: 'Table',
    position: [6.4, 8.8, 6.2],
    target: [0, 0, 0],
    zoom: 40
  }
};

const resourceLegend: Array<{ resource: TileResourceType; label: string }> = [
  { resource: 'Wood', label: 'Wood' },
  { resource: 'Clay', label: 'Clay' },
  { resource: 'Wool', label: 'Wool' },
  { resource: 'Grain', label: 'Grain' },
  { resource: 'Stone', label: 'Stone' },
  { resource: 'None', label: 'Desert' }
];

const resourceColors: Record<TileResourceType, { base: string; side: string; top: string; detail: string; light: string; label: string }> = {
  Wood: {
    base: '#1b5737',
    side: '#0f3022',
    top: '#2f7046',
    detail: '#143f2b',
    light: '#4b8a59',
    label: 'Wood'
  },
  Clay: {
    base: '#b96542',
    side: '#71392a',
    top: '#d58655',
    detail: '#88442f',
    light: '#e5a06b',
    label: 'Clay'
  },
  Wool: {
    base: '#d9eaa8',
    side: '#95b568',
    top: '#edf2c5',
    detail: '#a8c67a',
    light: '#f6f6d7',
    label: 'Wool'
  },
  Grain: {
    base: '#d0a63b',
    side: '#8f6724',
    top: '#edcc66',
    detail: '#a57b24',
    light: '#f7df87',
    label: 'Grain'
  },
  Stone: {
    base: '#737f84',
    side: '#49545a',
    top: '#a8b0af',
    detail: '#59666c',
    light: '#c1c8c4',
    label: 'Stone'
  },
  None: {
    base: '#d2ad77',
    side: '#927049',
    top: '#e4ca98',
    detail: '#ad8756',
    light: '#f1dcae',
    label: 'Desert'
  }
};

const playerColors = ['#d95f43', '#2f7f75', '#4269b2', '#d6a230'];

export function Board3DRenderer({
  game,
  playerId,
  debugOptions = defaultBoardDebugOptions,
  pendingAction = null,
  toolbarAction,
  onPlaceSetupCamp,
  onPlaceSetupTrail,
  onMoveWarden
}: Board3DRendererProps) {
  const [cameraCommand, setCameraCommand] = useState<CameraCommand>({ preset: 'default', revision: 0 });
  const pointerStartRef = useRef<{ x: number; y: number } | null>(null);
  const isCameraDragRef = useRef(false);
  const dragResetTimerRef = useRef<number | null>(null);
  const issueCameraCommand = useCallback((preset: CameraPresetId) => {
    setCameraCommand((current) => ({
      preset,
      revision: current.revision + 1
    }));
  }, []);
  const clearDragGuardSoon = useCallback(() => {
    if (dragResetTimerRef.current !== null) {
      window.clearTimeout(dragResetTimerRef.current);
    }

    dragResetTimerRef.current = window.setTimeout(() => {
      isCameraDragRef.current = false;
      dragResetTimerRef.current = null;
    }, 0);
  }, []);
  const handleStagePointerDown = useCallback((event: ReactPointerEvent<HTMLDivElement>) => {
    pointerStartRef.current = { x: event.clientX, y: event.clientY };
    isCameraDragRef.current = false;

    if (dragResetTimerRef.current !== null) {
      window.clearTimeout(dragResetTimerRef.current);
      dragResetTimerRef.current = null;
    }
  }, []);
  const handleStagePointerMove = useCallback((event: ReactPointerEvent<HTMLDivElement>) => {
    const pointerStart = pointerStartRef.current;

    if (!pointerStart) {
      return;
    }

    const movement = Math.hypot(event.clientX - pointerStart.x, event.clientY - pointerStart.y);

    if (movement > cameraDragThreshold) {
      isCameraDragRef.current = true;
    }
  }, []);
  const handleStagePointerEnd = useCallback(() => {
    pointerStartRef.current = null;
    clearDragGuardSoon();
  }, [clearDragGuardSoon]);
  const canUseBoardClick = useCallback(() => !isCameraDragRef.current, []);

  useEffect(() => {
    return () => {
      if (dragResetTimerRef.current !== null) {
        window.clearTimeout(dragResetTimerRef.current);
      }
    };
  }, []);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() !== 'r' || event.altKey || event.ctrlKey || event.metaKey) {
        return;
      }

      const target = event.target as HTMLElement | null;

      if (target?.isContentEditable || ['INPUT', 'SELECT', 'TEXTAREA'].includes(target?.tagName ?? '')) {
        return;
      }

      event.preventDefault();
      issueCameraCommand('default');
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [issueCameraCommand]);

  return (
    <section className="board-table board-table-3d">
      <div className="board-toolbar">
        <div>
          <p className="eyebrow">Experimental Renderer</p>
          <h2>Karo Board 3D</h2>
        </div>
        <div className="board-toolbar-tools">
          <div className="resource-legend" aria-label="Resource legend">
            {resourceLegend.map((item) => (
              <span className={`legend-chip legend-${item.resource.toLowerCase()}`} key={item.resource}>
                {item.label}
              </span>
            ))}
          </div>
          {toolbarAction}
          <ThreeCameraControlsPanel onCameraCommand={issueCameraCommand} />
        </div>
      </div>

      <div
        className="three-board-stage"
        onContextMenu={(event) => event.preventDefault()}
        onPointerCancel={handleStagePointerEnd}
        onPointerDownCapture={handleStagePointerDown}
        onPointerMoveCapture={handleStagePointerMove}
        onPointerUpCapture={handleStagePointerEnd}
      >
        <Canvas
          className="three-board-canvas"
          dpr={[1, 1.7]}
          gl={{ antialias: true, alpha: true }}
          shadows
        >
          <ThreeBoardScene
            cameraCommand={cameraCommand}
            canUseBoardClick={canUseBoardClick}
            debugOptions={debugOptions}
            game={game}
            pendingAction={pendingAction}
            playerId={playerId}
            onMoveWarden={onMoveWarden}
            onPlaceSetupCamp={onPlaceSetupCamp}
            onPlaceSetupTrail={onPlaceSetupTrail}
          />
        </Canvas>
      </div>
    </section>
  );
}

function ThreeCameraControlsPanel({
  onCameraCommand
}: {
  onCameraCommand: (preset: CameraPresetId) => void;
}) {
  return (
    <div className="three-camera-controls" aria-label="3D camera controls">
      <span>View</span>
      {(Object.keys(cameraPresets) as CameraPresetId[]).map((preset) => (
        <button key={preset} type="button" onClick={() => onCameraCommand(preset)}>
          {cameraPresets[preset].label}
        </button>
      ))}
    </div>
  );
}

function ThreeBoardScene({
  game,
  playerId,
  debugOptions,
  cameraCommand,
  canUseBoardClick,
  pendingAction,
  onPlaceSetupCamp,
  onPlaceSetupTrail,
  onMoveWarden
}: Required<Pick<Board3DRendererProps, 'game' | 'playerId' | 'debugOptions'>> & Pick<Board3DRendererProps, 'pendingAction' | 'onPlaceSetupCamp' | 'onPlaceSetupTrail' | 'onMoveWarden'> & {
  cameraCommand: CameraCommand;
  canUseBoardClick: () => boolean;
}) {
  const layout = useMemo(() => buildThreeBoardLayout(game), [game]);
  const isSetupPlacementAvailable = game.phase === 'Setup'
    && game.currentSetupPlayerId === playerId
    && !pendingAction;
  const setupCampTargets = useMemo(() => {
    if (!isSetupPlacementAvailable || game.setupStep !== 'PlaceCamp') {
      return new Set<string>();
    }

    return new Set(
      game.board.vertices
        .filter((vertex) => isValidSetupCampTarget(vertex, game.board.vertices, game.board.edges))
        .map((vertex) => vertex.vertexId)
    );
  }, [game.board.edges, game.board.vertices, game.setupStep, isSetupPlacementAvailable]);
  const setupTrailTargets = useMemo(() => {
    if (!isSetupPlacementAvailable || game.setupStep !== 'PlaceTrail' || !game.lastSetupCampVertexId) {
      return new Set<string>();
    }

    return new Set(
      game.board.edges
        .filter((edge) => !edge.ownerPlayerId && edgeTouchesVertex(edge, game.lastSetupCampVertexId!))
        .map((edge) => edge.edgeId)
    );
  }, [game.board.edges, game.lastSetupCampVertexId, game.setupStep, isSetupPlacementAvailable]);
  const canMoveWarden = game.phase === 'NormalTurn'
    && game.pendingWardenAction === 'MoveWarden'
    && game.currentWardenPlayerId === playerId
    && !pendingAction
    && !!onMoveWarden;
  const wardenTileId = game.wardenTileId ?? game.robberTileId;

  return (
    <>
      <ControlledBoardCamera command={cameraCommand} />
      <ambientLight intensity={0.58} />
      <hemisphereLight args={['#fff2d5', '#6ba3a0', 0.96]} />
      <directionalLight
        castShadow
        intensity={1.62}
        position={[4.8, 10.4, 4.4]}
        shadow-bias={-0.0003}
        shadow-camera-bottom={-8}
        shadow-camera-left={-8}
        shadow-camera-right={8}
        shadow-camera-top={8}
        shadow-mapSize-height={1536}
        shadow-mapSize-width={1536}
      />
      <directionalLight intensity={0.34} position={[-5, 4.8, -4]} color="#ffe8b5" />

      <group rotation={[0, -0.04, 0]}>
        <group position={[-layout.center.x, 0, -layout.center.z]}>
          {layout.tiles.map(({ tile, position }) => (
            <HexPrismTile
              canMoveWarden={canMoveWarden && tile.tileId !== wardenTileId}
              game={game}
              key={tile.tileId}
              position={position}
              tile={tile}
              showCoordinates={debugOptions.showCoordinates}
              showTileId={debugOptions.showTileIds}
              showWardenId={debugOptions.showRobberTileId}
              canUseBoardClick={canUseBoardClick}
              onMoveWarden={onMoveWarden}
            />
          ))}

          {layout.edges.map((edgeLayout) => (
            <ThreeTrailPiece
              game={game}
              isSetupTarget={setupTrailTargets.has(edgeLayout.edge.edgeId)}
              key={edgeLayout.edge.edgeId}
              canUseBoardClick={canUseBoardClick}
              onPlaceSetupTrail={onPlaceSetupTrail}
              {...edgeLayout}
            />
          ))}

          {layout.vertices.map((vertexLayout) => (
            <ThreeBuildNode
              game={game}
              isSetupTarget={setupCampTargets.has(vertexLayout.vertex.vertexId)}
              key={vertexLayout.vertex.vertexId}
              showNodeDetails={debugOptions.showNodeDetails}
              showNodeId={debugOptions.showNodeIds}
              showValidBuildPlacements={debugOptions.showValidBuildPlacements}
              canUseBoardClick={canUseBoardClick}
              onPlaceSetupCamp={onPlaceSetupCamp}
              {...vertexLayout}
            />
          ))}

          {layout.harbors.map((harborLayout) => (
            <ThreeHarborMarker
              key={harborLayout.slot.harborSlotId}
              showDetails={debugOptions.showHarborDetails}
              showId={debugOptions.showHarborSlotIds}
              {...harborLayout}
            />
          ))}

          {debugOptions.showEdgeIds ? (
            layout.edges.map((edgeLayout) => (
              <Html center className="three-debug-label three-edge-debug-label" key={`edge-${edgeLayout.edge.edgeId}`} position={[edgeLayout.midpoint.x, 0.62, edgeLayout.midpoint.z]}>
                {edgeLayout.edge.edgeId}
              </Html>
            ))
          ) : null}
        </group>
      </group>

      <ContactShadows color="#2f2117" far={11} blur={3.9} opacity={0.48} position={[0, -0.08, 0]} scale={16} />
    </>
  );
}

function ControlledBoardCamera({ command }: { command: CameraCommand }) {
  const { size } = useThree();
  const cameraRef = useRef<BoardCameraRef | null>(null);
  const controlsRef = useRef<BoardControlsRef | null>(null);
  const getResponsiveZoom = useCallback((zoom: number) => {
    if (size.width <= 0 || size.height <= 0) {
      return zoom;
    }

    const narrowFit = size.width < 640 ? size.width / 560 : 1;
    const shortFit = size.height < 520 ? size.height / 620 : 1;

    return Math.max(22, Math.round(zoom * Math.min(1, narrowFit, shortFit)));
  }, [size.height, size.width]);
  const setCameraRef = useCallback((camera: unknown) => {
    cameraRef.current = camera as BoardCameraRef | null;
  }, []);
  const setControlsRef = useCallback((controls: unknown) => {
    controlsRef.current = controls as BoardControlsRef | null;
  }, []);
  const clampCameraTarget = useCallback(() => {
    const camera = cameraRef.current;
    const controls = controlsRef.current;

    if (!camera || !controls) {
      return;
    }

    const previousTarget = {
      x: controls.target.x,
      y: controls.target.y,
      z: controls.target.z
    };
    const nextTarget = {
      x: clamp(previousTarget.x, -panClamp.x, panClamp.x),
      y: 0,
      z: clamp(previousTarget.z, -panClamp.z, panClamp.z)
    };

    if (
      previousTarget.x === nextTarget.x
      && previousTarget.y === nextTarget.y
      && previousTarget.z === nextTarget.z
    ) {
      return;
    }

    camera.position.set(
      camera.position.x + nextTarget.x - previousTarget.x,
      camera.position.y + nextTarget.y - previousTarget.y,
      camera.position.z + nextTarget.z - previousTarget.z
    );
    controls.target.set(nextTarget.x, nextTarget.y, nextTarget.z);
    controls.update();
  }, []);

  useEffect(() => {
    const camera = cameraRef.current;
    const controls = controlsRef.current;
    const preset = cameraPresets[command.preset];

    if (!camera || !controls) {
      return;
    }

    camera.position.set(...preset.position);
    camera.zoom = getResponsiveZoom(preset.zoom);
    camera.updateProjectionMatrix();
    controls.target.set(...preset.target);
    controls.update();
  }, [command, getResponsiveZoom]);

  return (
    <>
      <OrthographicCamera
        makeDefault
        ref={setCameraRef}
        near={0.1}
        far={100}
        position={cameraPresets.default.position}
        zoom={getResponsiveZoom(cameraPresets.default.zoom)}
      />
      <MapControls
        ref={setControlsRef}
        enableDamping
        dampingFactor={0.12}
        enablePan
        enableRotate
        enableZoom
        makeDefault
        maxAzimuthAngle={Math.PI * 0.3}
        maxPolarAngle={0.88}
        maxZoom={56}
        minAzimuthAngle={-Math.PI * 0.3}
        minPolarAngle={0.22}
        minZoom={20}
        panSpeed={0.76}
        rotateSpeed={0.56}
        screenSpacePanning
        target={cameraPresets.default.target}
        zoomSpeed={0.76}
        onChange={clampCameraTarget}
      />
    </>
  );
}

function HexPrismTile({
  tile,
  position,
  game,
  canMoveWarden,
  showCoordinates,
  showTileId,
  showWardenId,
  canUseBoardClick,
  onMoveWarden
}: {
  tile: HexTileModel;
  position: Vec2;
  game: GameState;
  canMoveWarden: boolean;
  showCoordinates: boolean;
  showTileId: boolean;
  showWardenId: boolean;
  canUseBoardClick: () => boolean;
  onMoveWarden?: (roomCode: string, targetTileId: string) => Promise<void>;
}) {
  const palette = resourceColors[tile.resourceType];
  const handleTileClick = canMoveWarden
    ? (event: ThreeEvent<MouseEvent>) => {
        event.stopPropagation();
        if (!canUseBoardClick()) {
          return;
        }

        void onMoveWarden?.(game.roomCode, tile.tileId);
      }
    : undefined;
  const hasNumber = tile.resourceType !== 'None' && tile.numberToken !== null;

  return (
    <group position={[position.x, tileHeight / 2, position.z]}>
      <mesh castShadow receiveShadow onClick={handleTileClick}>
        <cylinderGeometry args={[landTileMeshRadius, landTileMeshRadius, tileHeight, 6, 1, false, Math.PI / 6]} />
        <meshStandardMaterial
          color={canMoveWarden ? '#f6d46a' : palette.side}
          emissive={canMoveWarden ? '#5e3f00' : '#000000'}
          emissiveIntensity={canMoveWarden ? 0.12 : 0}
          metalness={0.01}
          roughness={0.88}
        />
      </mesh>
      <mesh castShadow receiveShadow position={[0, tileHeight / 2 + 0.018, 0]}>
        <cylinderGeometry args={[landTileSurfaceRadius, landTileSurfaceRadius, 0.085, 6, 1, false, Math.PI / 6]} />
        <meshStandardMaterial color={palette.base} roughness={0.9} metalness={0.01} />
      </mesh>
      <mesh position={[0, tileHeight / 2 + 0.065, 0]} rotation={[-Math.PI / 2, 0, Math.PI / 6]} receiveShadow>
        <circleGeometry args={[landTileSurfaceRadius, 6]} />
        <meshStandardMaterial color={palette.top} roughness={0.94} metalness={0.01} />
      </mesh>
      {tile.isBlocked ? (
        <mesh position={[0, tileHeight / 2 + 0.088, 0]} rotation={[-Math.PI / 2, 0, Math.PI / 6]}>
          <circleGeometry args={[tileRadius * 0.72, 6]} />
          <meshBasicMaterial color="#35283f" transparent opacity={0.18} />
        </mesh>
      ) : null}
      <Html center className="three-tile-label" position={[0, tileHeight / 2 + 0.115, hasNumber ? 0.42 : 0]}>
        <span className="three-resource-label">{palette.label}</span>
      </Html>
      {hasNumber ? <ThreeNumberToken value={tile.numberToken!} /> : null}
      {tile.isBlocked ? <ThreeWardenPiece /> : null}
      {showTileId || showCoordinates || (showWardenId && tile.isBlocked) ? (
        <Html center className="three-debug-label" position={[0, tileHeight / 2 + 0.5, 0]}>
          {showTileId ? <span>{tile.tileId}</span> : null}
          {showCoordinates ? <span>q{tile.q}, r{tile.r}</span> : null}
          {showWardenId && tile.isBlocked ? <span>Warden</span> : null}
        </Html>
      ) : null}
    </group>
  );
}

function ThreeNumberToken({ value }: { value: number }) {
  const pips = numberTokenPips(value);

  return (
    <group position={[0, tileHeight / 2 + 0.105, 0]}>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.004, 0]} scale={[1.12, 1, 1]}>
        <circleGeometry args={[0.34, 32]} />
        <meshBasicMaterial color="#1d140c" transparent opacity={0.12} />
      </mesh>
      <mesh castShadow receiveShadow>
        <cylinderGeometry args={[0.3, 0.32, 0.075, 32]} />
        <meshStandardMaterial color="#ead8a9" roughness={0.68} metalness={0.02} />
      </mesh>
      <mesh position={[0, 0.043, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <circleGeometry args={[0.272, 32]} />
        <meshStandardMaterial color="#f8ecd0" roughness={0.74} metalness={0.01} />
      </mesh>
      <mesh position={[0, 0.048, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <ringGeometry args={[0.22, 0.27, 28]} />
        <meshBasicMaterial color="#8c6b37" transparent opacity={0.18} />
      </mesh>
      {pips > 0 ? (
        <group position={[0, 0.066, 0.19]}>
          {Array.from({ length: pips }).map((_, index) => (
            <mesh key={index} position={[(index - (pips - 1) / 2) * 0.055, 0, 0]} castShadow>
              <sphereGeometry args={[0.017, 8, 6]} />
              <meshStandardMaterial color="#6f4c25" roughness={0.78} />
            </mesh>
          ))}
        </group>
      ) : null}
      <Html center className="three-number-token" position={[0, 0.085, -0.018]}>
        {value}
      </Html>
    </group>
  );
}

function numberTokenPips(value: number) {
  return Math.max(0, 6 - Math.abs(7 - value));
}

function ThreeTrailPiece({
  edge,
  start,
  end,
  midpoint,
  length,
  angle,
  game,
  isSetupTarget,
  canUseBoardClick,
  onPlaceSetupTrail
}: EdgeLayout3D & {
  game: GameState;
  isSetupTarget: boolean;
  canUseBoardClick: () => boolean;
  onPlaceSetupTrail?: (roomCode: string, edgeId: string) => Promise<void>;
}) {
  if (!edge.ownerPlayerId && !isSetupTarget) {
    return null;
  }

  const canPlace = isSetupTarget && !!onPlaceSetupTrail;
  const color = edge.ownerPlayerId ? playerColor(game, edge.ownerPlayerId) : '#f2d57c';
  const handleClick = canPlace
    ? (event: ThreeEvent<MouseEvent>) => {
        event.stopPropagation();
        if (!canUseBoardClick()) {
          return;
        }

        void onPlaceSetupTrail?.(game.roomCode, edge.edgeId);
      }
    : undefined;

  return (
    <group>
      <TrailBar
        color={color}
        length={length}
        midpoint={midpoint}
        opacity={edge.ownerPlayerId ? 1 : 0.5}
        y={0.5}
        angle={angle}
        radius={edge.ownerPlayerId ? 0.088 : 0.064}
        onClick={handleClick}
      />
      {isSetupTarget ? (
        <TrailBar
          color="#fff3b8"
          length={distance(start, end) * 0.72}
          midpoint={midpoint}
          opacity={0.2}
          y={0.585}
          angle={angle}
          radius={0.105}
          onClick={handleClick}
        />
      ) : null}
    </group>
  );
}

function TrailBar({
  midpoint,
  length,
  angle,
  color,
  y,
  radius,
  opacity,
  onClick
}: {
  midpoint: Vec2;
  length: number;
  angle: number;
  color: string;
  y: number;
  radius: number;
  opacity: number;
  onClick?: (event: ThreeEvent<MouseEvent>) => void;
}) {
  return (
    <group position={[midpoint.x, y, midpoint.z]} rotation={[0, -angle, 0]} onClick={onClick}>
      <mesh rotation={[0, 0, Math.PI / 2]} position={[0, -radius * 0.48, 0]}>
        <cylinderGeometry args={[radius * 1.24, radius * 1.24, length * 0.98, 12]} />
        <meshBasicMaterial color="#21160d" transparent opacity={opacity * 0.18} />
      </mesh>
      <mesh castShadow rotation={[0, 0, Math.PI / 2]}>
        <cylinderGeometry args={[radius, radius, length, 12]} />
        <meshStandardMaterial color={color} roughness={0.68} metalness={0.02} transparent opacity={opacity} />
      </mesh>
      <mesh castShadow position={[-length / 2, 0, 0]}>
        <sphereGeometry args={[radius, 12, 8]} />
        <meshStandardMaterial color={color} roughness={0.68} metalness={0.02} transparent opacity={opacity} />
      </mesh>
      <mesh castShadow position={[length / 2, 0, 0]}>
        <sphereGeometry args={[radius, 12, 8]} />
        <meshStandardMaterial color={color} roughness={0.68} metalness={0.02} transparent opacity={opacity} />
      </mesh>
      <mesh position={[0, radius * 0.42, 0]} rotation={[0, 0, Math.PI / 2]}>
        <cylinderGeometry args={[radius * 0.42, radius * 0.42, length * 0.86, 10]} />
        <meshBasicMaterial color="#fff8d8" transparent opacity={opacity * 0.24} />
      </mesh>
    </group>
  );
}

function ThreeBuildNode({
  vertex,
  position,
  game,
  isSetupTarget,
  showNodeDetails,
  showNodeId,
  showValidBuildPlacements,
  canUseBoardClick,
  onPlaceSetupCamp
}: VertexLayout3D & {
  game: GameState;
  isSetupTarget: boolean;
  showNodeDetails: boolean;
  showNodeId: boolean;
  showValidBuildPlacements: boolean;
  canUseBoardClick: () => boolean;
  onPlaceSetupCamp?: (roomCode: string, vertexId: string) => Promise<void>;
}) {
  const [isHovered, setIsHovered] = useState(false);
  const isOccupied = !!vertex.ownerPlayerId && !!vertex.structureType;
  const canPlace = isSetupTarget && !!onPlaceSetupCamp;
  const color = playerColor(game, vertex.ownerPlayerId);
  const handleClick = canPlace
    ? (event: ThreeEvent<MouseEvent>) => {
        event.stopPropagation();
        if (!canUseBoardClick()) {
          return;
        }

        void onPlaceSetupCamp?.(game.roomCode, vertex.vertexId);
      }
    : undefined;

  return (
    <group position={[position.x, 0.42, position.z]}>
      {isOccupied && vertex.structureType === 'Stronghold' ? <ThreeStrongholdPiece color={color} /> : null}
      {isOccupied && vertex.structureType === 'Camp' ? <ThreeCampPiece color={color} /> : null}
      {isSetupTarget || showValidBuildPlacements ? (
        <group
          onClick={handleClick}
          onPointerOut={() => setIsHovered(false)}
          onPointerOver={() => setIsHovered(true)}
        >
          <mesh rotation={[-Math.PI / 2, 0, 0]}>
            <torusGeometry args={[isSetupTarget ? (isHovered ? 0.2 : 0.112) : 0.085, 0.01, 8, 22]} />
            <meshStandardMaterial color={isSetupTarget ? '#ffe796' : '#fff4cc'} transparent opacity={isSetupTarget ? (isHovered ? 0.72 : 0.12) : 0.08} />
          </mesh>
          {isSetupTarget ? (
            <mesh rotation={[-Math.PI / 2, 0, 0]}>
              <circleGeometry args={[isHovered ? 0.14 : 0.06, 20]} />
              <meshBasicMaterial color="#fff2b2" transparent opacity={isHovered ? 0.2 : 0.035} />
            </mesh>
          ) : null}
        </group>
      ) : null}
      {showNodeId || showNodeDetails ? (
        <Html center className="three-debug-label three-node-debug-label" position={[0, 0.34, 0]}>
          {showNodeId ? <span>{vertex.vertexId}</span> : null}
          {showNodeDetails ? <span>{getVertexAdjacentTileSummary(game, vertex)}</span> : null}
        </Html>
      ) : null}
    </group>
  );
}

function ThreeCampPiece({ color }: { color: string }) {
  return (
    <group>
      <mesh position={[0, -0.115, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <circleGeometry args={[0.25, 28]} />
        <meshBasicMaterial color="#21160d" transparent opacity={0.16} />
      </mesh>
      <mesh castShadow receiveShadow position={[0, -0.065, 0]}>
        <cylinderGeometry args={[0.19, 0.24, 0.085, 10]} />
        <meshStandardMaterial color="#5a3c25" roughness={0.86} />
      </mesh>
      <mesh castShadow position={[0, 0.05, 0]} rotation={[0, Math.PI / 4, 0]}>
        <coneGeometry args={[0.23, 0.32, 4]} />
        <meshStandardMaterial color={color} roughness={0.76} metalness={0.02} />
      </mesh>
      <mesh castShadow position={[0, 0.14, 0]} rotation={[0, Math.PI / 4, 0]}>
        <coneGeometry args={[0.11, 0.15, 4]} />
        <meshStandardMaterial color="#fff0b8" roughness={0.66} />
      </mesh>
      <mesh position={[0, -0.015, 0.155]}>
        <boxGeometry args={[0.08, 0.1, 0.02]} />
        <meshStandardMaterial color="#3c2818" roughness={0.82} />
      </mesh>
    </group>
  );
}

function ThreeStrongholdPiece({ color }: { color: string }) {
  return (
    <group>
      <mesh position={[0, -0.13, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <circleGeometry args={[0.29, 30]} />
        <meshBasicMaterial color="#21160d" transparent opacity={0.17} />
      </mesh>
      <mesh castShadow receiveShadow position={[0, -0.08, 0]}>
        <cylinderGeometry args={[0.21, 0.25, 0.09, 8]} />
        <meshStandardMaterial color="#5a3c25" roughness={0.84} />
      </mesh>
      <mesh castShadow position={[0, 0.12, 0]}>
        <cylinderGeometry args={[0.17, 0.21, 0.43, 6]} />
        <meshStandardMaterial color={color} roughness={0.72} metalness={0.02} />
      </mesh>
      <mesh castShadow position={[0, 0.405, 0]}>
        <cylinderGeometry args={[0.25, 0.2, 0.105, 6]} />
        <meshStandardMaterial color="#633f28" roughness={0.78} />
      </mesh>
      <mesh castShadow position={[0, 0.52, 0]}>
        <coneGeometry args={[0.22, 0.2, 6]} />
        <meshStandardMaterial color="#f4d98a" roughness={0.7} />
      </mesh>
      <mesh position={[0, 0.16, 0.19]}>
        <boxGeometry args={[0.08, 0.14, 0.025]} />
        <meshStandardMaterial color="#2f2117" roughness={0.82} />
      </mesh>
    </group>
  );
}

function ThreeWardenPiece() {
  return (
    <group position={[0.48, tileHeight / 2 + 0.2, -0.44]}>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.01, 0]}>
        <circleGeometry args={[0.26, 28]} />
        <meshBasicMaterial color="#1c1220" transparent opacity={0.18} />
      </mesh>
      <mesh castShadow receiveShadow position={[0, 0.05, 0]}>
        <cylinderGeometry args={[0.18, 0.22, 0.1, 12]} />
        <meshStandardMaterial color="#201729" roughness={0.68} />
      </mesh>
      <mesh castShadow position={[0, 0.2, 0]}>
        <cylinderGeometry args={[0.12, 0.17, 0.28, 10]} />
        <meshStandardMaterial color="#31253b" roughness={0.62} />
      </mesh>
      <mesh castShadow position={[0, 0.4, 0]}>
        <sphereGeometry args={[0.14, 14, 10]} />
        <meshStandardMaterial color="#584264" roughness={0.58} />
      </mesh>
      <Html center className="three-warden-label" position={[0, 0.66, 0]}>
        W
      </Html>
    </group>
  );
}

function ThreeHarborMarker({
  slot,
  position,
  coast,
  angle,
  showDetails,
  showId
}: HarborLayout3D & {
  showDetails: boolean;
  showId: boolean;
}) {
  const label = slot.harborType === 'Generic' ? 'Any' : slot.harborType;
  const harborColor = slot.harborType === 'Generic' ? '#f0dfbd' : resourceColors[slot.harborType].top;
  const connectorAngle = Math.atan2(position.z - coast.z, position.x - coast.x);
  const connectorLength = Math.min(distance(position, coast), 0.58);
  const connectorMidpoint = {
    x: coast.x + Math.cos(connectorAngle) * connectorLength * 0.5,
    z: coast.z + Math.sin(connectorAngle) * connectorLength * 0.5
  };

  return (
    <group>
      <ConnectorBar color="#6f5135" length={connectorLength} midpoint={connectorMidpoint} opacity={0.42} y={0.08} angle={connectorAngle} thickness={0.04} />
      <group position={[coast.x + Math.cos(connectorAngle) * 0.23, 0.13, coast.z + Math.sin(connectorAngle) * 0.23]} rotation={[0, -connectorAngle, 0]}>
        <mesh castShadow position={[-0.11, 0, 0]}>
          <boxGeometry args={[0.09, 0.11, 0.22]} />
          <meshStandardMaterial color="#5d3d25" roughness={0.82} />
        </mesh>
        <mesh castShadow position={[0.11, 0, 0]}>
          <boxGeometry args={[0.09, 0.11, 0.22]} />
          <meshStandardMaterial color="#5d3d25" roughness={0.82} />
        </mesh>
        <mesh castShadow position={[0, 0.045, 0]}>
          <boxGeometry args={[0.3, 0.06, 0.16]} />
          <meshStandardMaterial color="#8a623b" roughness={0.84} />
        </mesh>
      </group>
      <group position={[position.x, 0.18, position.z]} rotation={[0, -angle, 0]}>
        <mesh castShadow receiveShadow>
          <cylinderGeometry args={[0.25, 0.3, 0.1, 14]} />
          <meshStandardMaterial color={harborColor} roughness={0.78} metalness={0.02} />
        </mesh>
        <mesh castShadow position={[0, 0.095, -0.02]}>
          <boxGeometry args={[0.44, 0.06, 0.15]} />
          <meshStandardMaterial color="#7b5635" roughness={0.84} />
        </mesh>
        <mesh castShadow position={[-0.17, 0.145, 0.04]}>
          <boxGeometry args={[0.045, 0.12, 0.06]} />
          <meshStandardMaterial color="#5d3d25" roughness={0.82} />
        </mesh>
        <mesh castShadow position={[0.17, 0.145, 0.04]}>
          <boxGeometry args={[0.045, 0.12, 0.06]} />
          <meshStandardMaterial color="#5d3d25" roughness={0.82} />
        </mesh>
        <mesh castShadow position={[0, 0.14, 0.13]} rotation={[0, 0, Math.PI / 2]}>
          <cylinderGeometry args={[0.026, 0.026, 0.18, 10]} />
          <meshStandardMaterial color="#efe0b8" roughness={0.68} />
        </mesh>
        <Html center className="three-harbor-label" position={[0, 0.255, 0]}>
          <strong>{slot.tradeRate}:1</strong>
          <span>{label}</span>
          {showId ? <em>{slot.harborSlotId}</em> : null}
          {showDetails ? <small>{slot.adjacentVertexIds.join(', ')}</small> : null}
        </Html>
      </group>
    </group>
  );
}

function ConnectorBar({
  midpoint,
  length,
  angle,
  color,
  y,
  thickness,
  opacity,
  onClick
}: {
  midpoint: Vec2;
  length: number;
  angle: number;
  color: string;
  y: number;
  thickness: number;
  opacity: number;
  onClick?: (event: ThreeEvent<MouseEvent>) => void;
}) {
  return (
    <mesh castShadow position={[midpoint.x, y, midpoint.z]} rotation={[0, -angle, 0]} onClick={onClick}>
      <boxGeometry args={[length, thickness, thickness]} />
      <meshStandardMaterial color={color} roughness={0.68} transparent opacity={opacity} />
    </mesh>
  );
}

function buildThreeBoardLayout(game: GameState): ThreeBoardLayout {
  const tiles = game.board.tiles.map((tile) => ({
    tile,
    position: axialToThree(tile.q, tile.r)
  }));
  const tileLayoutById = new Map(tiles.map((tileLayout) => [tileLayout.tile.tileId, tileLayout]));
  const center = getCenter(tiles.map(({ position }) => position));
  const vertices = game.board.vertices.map((vertex) => ({
    vertex,
    position: toThreeVertex(vertex, tileLayoutById)
  }));
  const vertexById = new Map(vertices.map(({ vertex, position }) => [vertex.vertexId, position]));
  const edges = game.board.edges
    .map((edge) => {
      const start = vertexById.get(edge.startVertexId);
      const end = vertexById.get(edge.endVertexId);

      if (!start || !end) {
        return null;
      }

      const edgeMidpoint = midpoint(start, end);
      return {
        edge,
        start,
        end,
        midpoint: edgeMidpoint,
        length: distance(start, end),
        angle: Math.atan2(end.z - start.z, end.x - start.x)
      };
    })
    .filter((edge): edge is EdgeLayout3D => edge !== null);
  const harbors = game.board.harborSlots.map((slot) => {
    const rotation = (Math.PI / 180) * slot.orientationDegrees;
    const outward = {
      x: Math.cos(rotation),
      z: Math.sin(rotation)
    };
    const start = vertexById.get(slot.adjacentVertexIds[0]);
    const end = vertexById.get(slot.adjacentVertexIds[1]);
    const coast = start && end
      ? midpoint(start, end)
      : {
          x: slot.renderX * coordinateScale,
          z: slot.renderY * coordinateScale
        };
    const harborDistance = 0.62;

    return {
      slot,
      coast,
      position: {
        x: coast.x + outward.x * harborDistance,
        z: coast.z + outward.z * harborDistance
      },
      angle: rotation
    };
  });
  return {
    center,
    tiles,
    vertices,
    edges,
    harbors
  };
}

function axialToThree(q: number, r: number): Vec2 {
  return {
    x: tileRadius * sqrtThree * (q + r / 2),
    z: tileRadius * 1.5 * r
  };
}

function toThreeVertex(vertex: BoardVertex, tileLayoutById: Map<string, TileLayout3D>): Vec2 {
  const backendPosition = toScaledBackendVertex(vertex);
  const adjacentCorners = vertex.adjacentTileIds
    .map((tileId) => tileLayoutById.get(tileId))
    .filter((tileLayout): tileLayout is TileLayout3D => tileLayout !== undefined)
    .map(({ position }) => closestHexCorner(position, backendPosition));

  if (adjacentCorners.length === 0) {
    return backendPosition;
  }

  return {
    x: adjacentCorners.reduce((sum, corner) => sum + corner.x, 0) / adjacentCorners.length,
    z: adjacentCorners.reduce((sum, corner) => sum + corner.z, 0) / adjacentCorners.length
  };
}

function toScaledBackendVertex(vertex: BoardVertex): Vec2 {
  return {
    x: vertex.x * coordinateScale,
    z: vertex.y * coordinateScale
  };
}

function closestHexCorner(center: Vec2, target: Vec2) {
  let bestCorner = hexCornerToThree(center, 0);
  let bestDistance = distance(bestCorner, target);

  for (let corner = 1; corner < 6; corner += 1) {
    const candidate = hexCornerToThree(center, corner);
    const candidateDistance = distance(candidate, target);

    if (candidateDistance < bestDistance) {
      bestCorner = candidate;
      bestDistance = candidateDistance;
    }
  }

  return bestCorner;
}

function hexCornerToThree(center: Vec2, corner: number): Vec2 {
  const angle = (Math.PI / 180) * (hexCornerStartDegrees + 60 * corner);
  return {
    x: center.x + tileRadius * Math.cos(angle),
    z: center.z + tileRadius * Math.sin(angle)
  };
}

function midpoint(start: Vec2, end: Vec2): Vec2 {
  return {
    x: (start.x + end.x) / 2,
    z: (start.z + end.z) / 2
  };
}

function distance(start: Vec2, end: Vec2) {
  return Math.hypot(end.x - start.x, end.z - start.z);
}

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value));
}

function getCenter(points: Vec2[]): Vec2 {
  const bounds = getThreeBounds(points);
  return {
    x: (bounds.minX + bounds.maxX) / 2,
    z: (bounds.minZ + bounds.maxZ) / 2
  };
}

function getThreeBounds(points: Vec2[]) {
  return {
    minX: Math.min(...points.map((point) => point.x)),
    maxX: Math.max(...points.map((point) => point.x)),
    minZ: Math.min(...points.map((point) => point.z)),
    maxZ: Math.max(...points.map((point) => point.z))
  };
}

function edgeTouchesVertex(edge: BoardEdge, vertexId: string) {
  return edge.startVertexId === vertexId || edge.endVertexId === vertexId;
}

function isValidSetupCampTarget(vertex: BoardVertex, vertices: BoardVertex[], edges: BoardEdge[]) {
  if (vertex.ownerPlayerId || vertex.structureType) {
    return false;
  }

  return !edges
    .filter((edge) => edgeTouchesVertex(edge, vertex.vertexId))
    .some((edge) => {
      const otherVertexId = edge.startVertexId === vertex.vertexId ? edge.endVertexId : edge.startVertexId;
      const otherVertex = vertices.find((candidate) => candidate.vertexId === otherVertexId);
      return !!otherVertex?.ownerPlayerId && !!otherVertex.structureType;
    });
}

function playerColor(game: GameState, ownerPlayerId: string | null) {
  if (!ownerPlayerId) {
    return '#8f7a58';
  }

  const playerIndex = game.players.findIndex((player) => player.playerId === ownerPlayerId);
  return playerColors[Math.max(0, playerIndex) % playerColors.length];
}

function getVertexAdjacentTileSummary(game: GameState, vertex: BoardVertex) {
  return vertex.adjacentTileIds
    .map((tileId) => {
      const tile = game.board.tiles.find((candidate) => candidate.tileId === tileId);
      return tile ? `${tile.tileId}:${tile.resourceType}` : `${tileId}:?`;
    })
    .join(' | ');
}

interface ThreeBoardLayout {
  center: Vec2;
  tiles: TileLayout3D[];
  vertices: VertexLayout3D[];
  edges: EdgeLayout3D[];
  harbors: HarborLayout3D[];
}

interface TileLayout3D {
  tile: HexTileModel;
  position: Vec2;
}

interface VertexLayout3D {
  vertex: BoardVertex;
  position: Vec2;
}

interface EdgeLayout3D {
  edge: BoardEdge;
  start: Vec2;
  end: Vec2;
  midpoint: Vec2;
  length: number;
  angle: number;
}

interface HarborLayout3D {
  slot: HarborSlot;
  position: Vec2;
  coast: Vec2;
  angle: number;
}
