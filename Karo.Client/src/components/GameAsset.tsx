import { useEffect, useState } from 'react';
import type { CSSProperties } from 'react';
import type { DevelopmentCardType, HarborType, ResourceType, TileResourceType } from '../types/game';
import { resources } from '../types/game';
import type { ActionAssetType, GameAssetDefinition, PieceType } from '../assets/game/gameAssets';
import { gameAssets, getGameAsset } from '../assets/game/gameAssets';

type AssetSize = 'xs' | 'sm' | 'md' | 'lg';

interface GameAssetProps {
  asset: GameAssetDefinition;
  alt?: string;
  className?: string;
  decorative?: boolean;
  fallback?: GameAssetDefinition;
}

export function GameAsset({ asset, alt, className = '', decorative = false, fallback = gameAssets.harbors.Generic }: GameAssetProps) {
  const [source, setSource] = useState(asset.src);

  useEffect(() => setSource(asset.src), [asset.src]);

  return (
    <img
      alt={decorative ? '' : alt ?? asset.label}
      aria-hidden={decorative || undefined}
      className={`game-asset ${className}`.trim()}
      draggable={false}
      src={source}
      title={decorative ? undefined : alt ?? asset.label}
      onError={() => {
        if (source !== fallback.src) {
          if (import.meta.env.DEV) console.warn(`[Karo assets] Failed to load ${asset.src}; using fallback.`);
          setSource(fallback.src);
        }
      }}
    />
  );
}

export function ResourceIcon({ type, size = 'md', decorative = false }: { type: ResourceType; size?: AssetSize; decorative?: boolean }) {
  const asset = getGameAsset(gameAssets.resources, type);
  return <span aria-hidden={decorative || undefined} aria-label={decorative ? undefined : `${type} Supply`} className={`game-asset-mask resource-icon resource-icon-${size} resource-icon-${type.toLowerCase()}`} role={decorative ? undefined : 'img'} style={{ '--asset-mask': `url("${asset.src}")` } as CSSProperties} title={decorative ? undefined : `${type} Supply`} />;
}

export function TerrainSymbol({ type, size = 'md', decorative = false }: { type: TileResourceType; size?: AssetSize; decorative?: boolean }) {
  const asset = getGameAsset(gameAssets.symbols, type);
  const label = type === 'None' ? 'Desert' : `${type} Supply`;
  return <span aria-hidden={decorative || undefined} aria-label={decorative ? undefined : label} className={`game-asset-mask terrain-symbol terrain-symbol-${size} terrain-symbol-${type.toLowerCase()}`} role={decorative ? undefined : 'img'} style={{ '--asset-mask': `url("${asset.src}")` } as CSSProperties} title={decorative ? undefined : label} />;
}

export function ActionIcon({ type, size = 'md', decorative = true }: { type: ActionAssetType; size?: AssetSize; decorative?: boolean }) {
  const asset = getGameAsset(gameAssets.actions, type);
  return <span aria-hidden={decorative || undefined} aria-label={decorative ? undefined : asset.label} className={`game-asset-mask action-icon action-icon-${size}`} role={decorative ? undefined : 'img'} style={{ '--asset-mask': `url("${asset.src}")` } as CSSProperties} title={decorative ? undefined : asset.label} />;
}

export function ResourceAmount({ type, amount, compact = false }: { type: ResourceType; amount: number; compact?: boolean }) {
  return (
    <span className="resource-amount" aria-label={`${type}: ${amount}`} title={`${type}: ${amount}`}>
      <ResourceIcon decorative type={type} size={compact ? 'sm' : 'md'} />
      <b>{amount}</b>
      <span className="sr-only">{type}</span>
    </span>
  );
}

export function ResourceStripItem({
  type,
  amount,
  className = ''
}: {
  type: ResourceType;
  amount: number;
  className?: string;
}) {
  return (
    <span
      aria-label={`${type}: ${amount}`}
      className={`resource-strip-item resource-strip-${type.toLowerCase()} ${className}`.trim()}
      title={`${type}: ${amount}`}
    >
      <span aria-hidden="true" className="resource-strip-icon">
        <ResourceIcon decorative size="md" type={type} />
      </span>
      <b className="resource-strip-count">{amount}</b>
      <span className="sr-only">{type}</span>
    </span>
  );
}

export function ResourceInlineSummary({
  values,
  includeZero = false,
  compact = true
}: {
  values: Partial<Record<ResourceType, number>>;
  includeZero?: boolean;
  compact?: boolean;
}) {
  const visibleResources = resources.filter((resource) => includeZero || (values[resource] ?? 0) > 0);
  const label = visibleResources.map((resource) => `${resource}: ${values[resource] ?? 0}`).join(', ') || 'No supplies';

  return (
    <span aria-label={label} className="resource-inline-summary">
      {visibleResources.map((resource) => (
        <ResourceAmount amount={values[resource] ?? 0} compact={compact} key={resource} type={resource} />
      ))}
    </span>
  );
}

export function ResourceCost({ cost, compact = false }: { cost: Partial<Record<ResourceType, number>>; compact?: boolean }) {
  const entries = resources.filter((resource) => (cost[resource] ?? 0) > 0);
  const label = entries.map((resource) => `${cost[resource]} ${resource}`).join(' and ');

  return (
    <span className={`resource-cost ${compact ? 'resource-cost-compact' : ''}`} aria-label={`Costs ${label}`}>
      {entries.map((resource, index) => (
        <span className="resource-cost-item" key={resource}>
          {index > 0 ? <i aria-hidden="true">+</i> : null}
          <ResourceIcon decorative type={resource} size="sm" />
          <b>{cost[resource]}</b>
          <span className="sr-only">{resource}</span>
        </span>
      ))}
    </span>
  );
}

export function PieceAsset({ type, playerColor, decorative = false }: { type: PieceType; playerColor?: string; decorative?: boolean }) {
  const style = playerColor ? ({ '--piece-color': playerColor } as CSSProperties) : undefined;
  return (
    <span className={`piece-asset piece-asset-${type.toLowerCase()}`} style={style} aria-label={decorative ? undefined : type}>
      <GameAsset asset={getGameAsset(gameAssets.pieces, type)} decorative={decorative} alt={type} />
      {playerColor && type !== 'Warden' ? <span className="piece-color-accent" aria-hidden="true" /> : null}
    </span>
  );
}

export function PiecePreview({ type, playerColor }: { type: PieceType; playerColor?: string }) {
  return <span className="piece-preview"><PieceAsset type={type} playerColor={playerColor} /></span>;
}

export function DevelopmentCardArtwork({ type, hidden = false, decorative = false }: { type?: DevelopmentCardType | null; hidden?: boolean; decorative?: boolean }) {
  const asset = hidden || !type ? gameAssets.cardBack : getGameAsset(gameAssets.cards, type);
  const label = hidden || !type ? 'Hidden Karo Development Card' : `${type.replace(/([A-Z])/g, ' $1').trim()} artwork`;
  return <GameAsset asset={asset} className="development-card-artwork" decorative={decorative} alt={label} fallback={gameAssets.cardBack} />;
}

export function HarborIcon({ type, decorative = false }: { type: HarborType; decorative?: boolean }) {
  const asset = getGameAsset(gameAssets.harbors, type);
  return <span aria-hidden={decorative || undefined} aria-label={decorative ? undefined : `${type} harbor`} className={`game-asset-mask harbor-asset harbor-asset-${type.toLowerCase()}`} role={decorative ? undefined : 'img'} style={{ '--asset-mask': `url("${asset.src}")` } as CSSProperties} />;
}

export function TerrainTexture({ type, decorative = true }: { type: TileResourceType; decorative?: boolean }) {
  return <GameAsset asset={getGameAsset(gameAssets.terrain, type)} className={`terrain-texture-asset terrain-texture-${type.toLowerCase()}`} decorative={decorative} alt={`${type === 'None' ? 'Desert' : type} terrain`} />;
}

export function Model3DFallback({ type, playerColor }: { type: PieceType; playerColor?: string }) {
  return <PieceAsset type={type} playerColor={playerColor} />;
}
