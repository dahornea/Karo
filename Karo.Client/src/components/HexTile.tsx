import type { HexTile as HexTileModel, TileResourceType } from '../types/game';
import { gameAssets } from '../assets/game/gameAssets';
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

      <image
        aria-hidden="true"
        className={`terrain-resource-asset terrain-resource-${resourceKey}`}
        href={gameAssets.symbols[tile.resourceType].src}
        height="36"
        width="36"
        x={centerX - 18}
        y={tile.numberToken ? centerY + 27 : centerY - 18}
      />

      {tile.numberToken ? <NumberToken x={centerX} y={centerY} value={tile.numberToken} /> : null}

      {tile.isBlocked ? (
        <image
          aria-label="Warden"
          className="warden-asset"
          href={gameAssets.pieces.Warden.src}
          height="48"
          width="48"
          x={centerX + 6}
          y={centerY - 54}
        />
      ) : null}
    </g>
  );
}
