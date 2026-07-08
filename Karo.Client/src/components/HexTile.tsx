import type { HexTile as HexTileModel, TileResourceType } from '../types/game';
import { NumberToken } from './NumberToken';

interface HexTileProps {
  tile: HexTileModel;
  centerX: number;
  centerY: number;
  points: string;
  isWardenTarget?: boolean;
  roomCode?: string;
  onMoveWarden?: (roomCode: string, targetTileId: string) => Promise<void>;
}

const resourceLabels: Record<TileResourceType, string> = {
  Wood: 'Wood',
  Clay: 'Clay',
  Wool: 'Wool',
  Grain: 'Grain',
  Stone: 'Stone',
  None: 'Desert'
};

export function HexTile({
  tile,
  centerX,
  centerY,
  points,
  isWardenTarget = false,
  roomCode,
  onMoveWarden
}: HexTileProps) {
  const resourceKey = tile.resourceType.toLowerCase();
  const baseId = `terrain-base-${resourceKey}`;
  const patternId = `terrain-texture-${resourceKey}`;
  const canMoveWarden = isWardenTarget && !!roomCode && !!onMoveWarden;

  return (
    <g
      className={`hex-tile hex-tile-${resourceKey}`}
      data-warden-target={isWardenTarget}
      onClick={canMoveWarden ? () => void onMoveWarden?.(roomCode!, tile.tileId) : undefined}
      role={canMoveWarden ? 'button' : undefined}
      tabIndex={canMoveWarden ? 0 : undefined}
      onKeyDown={canMoveWarden ? (event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          void onMoveWarden?.(roomCode!, tile.tileId);
        }
      } : undefined}
    >
      <title>{isWardenTarget ? `Move Warden to ${resourceLabels[tile.resourceType]}` : resourceLabels[tile.resourceType]}</title>
      <polygon className="hex-shadow" points={points} transform="translate(0 6)" />
      <polygon className="hex-base" points={points} fill={`url(#${baseId})`} />
      <polygon className="hex-texture" points={points} fill={`url(#${patternId})`} />
      <polygon className="hex-highlight" points={points} />
      <polygon className="hex-border" points={points} />
      {tile.isBlocked ? <polygon className="hex-warden-block" points={points} /> : null}

      <TerrainIcon
        resource={tile.resourceType}
        x={centerX}
        y={tile.numberToken ? centerY + 43 : centerY + 18}
      />

      <text className="tile-resource-label" x={centerX} y={centerY - 20}>
        {resourceLabels[tile.resourceType]}
      </text>

      {tile.numberToken ? <NumberToken x={centerX} y={centerY} value={tile.numberToken} /> : null}

      {tile.isBlocked ? (
        <g className="warden-marker" transform={`translate(${centerX + 28} ${centerY - 28})`}>
          <circle r="12" />
          <text y="1">W</text>
        </g>
      ) : null}
    </g>
  );
}

interface TerrainIconProps {
  resource: TileResourceType;
  x: number;
  y: number;
}

function TerrainIcon({ resource, x, y }: TerrainIconProps) {
  switch (resource) {
    case 'Wood':
      return (
        <g className="terrain-icon terrain-icon-wood" transform={`translate(${x} ${y})`}>
          <path d="M-25 13 L-15 -8 L-4 13 Z" />
          <path d="M-12 15 L1 -15 L15 15 Z" />
          <path d="M8 13 L19 -8 L29 13 Z" />
          <path d="M-15 13 V21 M1 15 V23 M19 13 V21" />
        </g>
      );
    case 'Clay':
      return (
        <g className="terrain-icon terrain-icon-clay" transform={`translate(${x} ${y})`}>
          <path d="M-22 4 C-12 -8 -4 10 6 -2 C13 -10 19 -1 23 5" />
          <path d="M-18 12 H18" />
          <path d="M-8 4 L-2 12 M8 0 L14 12" />
        </g>
      );
    case 'Wool':
      return (
        <g className="terrain-icon terrain-icon-wool" transform={`translate(${x} ${y})`}>
          <path d="M-26 9 C-15 -4 -5 12 7 1 C15 -7 22 -1 28 7" />
          <path d="M-22 17 C-11 8 -1 20 10 11 C17 5 23 8 27 12" />
          <path d="M-13 11 L-9 5 M2 13 L6 7 M17 11 L20 6" />
        </g>
      );
    case 'Grain':
      return (
        <g className="terrain-icon terrain-icon-grain" transform={`translate(${x} ${y})`}>
          <path d="M0 18 V-16" />
          <path d="M0 -9 L-10 -16 M0 -5 L10 -12 M0 0 L-11 -6 M0 4 L11 -3 M0 9 L-9 3 M0 12 L9 6" />
        </g>
      );
    case 'Stone':
      return (
        <g className="terrain-icon terrain-icon-stone" transform={`translate(${x} ${y})`}>
          <path d="M-24 14 L-10 -12 L2 11 L13 -8 L26 14 Z" />
          <path d="M-10 -12 L-5 14 M13 -8 L8 14 M-2 3 H17" />
        </g>
      );
    case 'None':
      return (
        <g className="terrain-icon terrain-icon-none" transform={`translate(${x} ${y})`}>
          <path d="M-25 5 C-14 -6 -5 13 6 1 C14 -7 21 -1 25 5" />
          <path d="M-20 15 C-10 7 -2 18 8 10 C16 4 21 8 24 12" />
        </g>
      );
  }
}
