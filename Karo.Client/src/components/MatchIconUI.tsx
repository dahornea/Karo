import { Check, CircleDot, Clock3, Crown, Hammer, LockKeyhole, Route, Shield, Star, Trophy, UserRound } from 'lucide-react';
import type { ButtonHTMLAttributes, ReactNode } from 'react';
import type { ActionAssetType, PieceType } from '../assets/game/gameAssets';
import { ActionIcon, DevelopmentCardArtwork, PieceAsset } from './GameAsset';

export function AccessibleIconTooltip({
  children,
  className = '',
  label
}: {
  children: ReactNode;
  className?: string;
  label: string;
}) {
  return (
    <span aria-label={label} className={`accessible-icon-tooltip ${className}`.trim()} role="img" tabIndex={0} title={label}>
      <span aria-hidden="true">{children}</span>
      <span className="sr-only">{label}</span>
    </span>
  );
}

export function IconStat({ ariaLabel, icon, label, value }: { ariaLabel?: string; icon: ReactNode; label: string; value: ReactNode }) {
  const accessibleValue = ariaLabel ?? (typeof value === 'string' || typeof value === 'number' ? `${value} ${label}` : label);

  return (
    <span aria-label={accessibleValue} className="icon-stat" role="img" tabIndex={0} title={accessibleValue}>
      <span aria-hidden="true" className="icon-stat-symbol">{icon}</span>
      <b>{value}</b>
      <span className="sr-only">{label}</span>
    </span>
  );
}

export function IconActionButton({ asset, label, className = '', ...buttonProps }: ButtonHTMLAttributes<HTMLButtonElement> & {
  asset: ActionAssetType;
  label: string;
}) {
  return (
    <button {...buttonProps} aria-label={buttonProps['aria-label'] ?? label} className={`icon-action-button ${className}`.trim()}>
      <ActionIcon decorative type={asset} />
      <span>{label}</span>
    </button>
  );
}

export function PieceCount({ remaining, total, type }: { remaining: number; total: number; type: Exclude<PieceType, 'Warden'> }) {
  return (
    <span aria-label={`${remaining} of ${total} ${type}${total === 1 ? '' : 's'} remaining`} className="piece-count" title={`${type}: ${remaining}/${total} remaining`}>
      <PieceAsset decorative type={type} />
      <b>{remaining}</b>
      <span aria-hidden="true">/</span>
      <small>{total}</small>
      <span className="sr-only">{type}s remaining</span>
    </span>
  );
}

export function AwardBadge({ award }: { award: 'LargestArmy' | 'LongestTrail' }) {
  const isArmy = award === 'LargestArmy';
  const label = isArmy ? 'Largest Army' : 'Longest Trail';
  return (
    <AccessibleIconTooltip className="award-badge" label={label}>
      {isArmy ? <Shield size={15} /> : <Route size={15} />}
    </AccessibleIconTooltip>
  );
}

export function PlayerStatusIcons({
  hasLargestArmy,
  hasLongestTrail,
  isActive,
  isHost,
  isSelf,
  isWinner,
  phase
}: {
  hasLargestArmy: boolean;
  hasLongestTrail: boolean;
  isActive: boolean;
  isHost: boolean;
  isSelf: boolean;
  isWinner: boolean;
  phase: string;
}) {
  const primary = isWinner
    ? { icon: <Trophy size={14} />, label: 'Winner' }
    : isActive && phase === 'Setup'
      ? { icon: <Hammer size={14} />, label: isSelf ? 'Your setup' : 'Setting up' }
      : isActive
        ? { icon: <CircleDot size={14} />, label: isSelf ? 'Your turn' : 'Current turn' }
        : isSelf
          ? { icon: <UserRound size={14} />, label: 'You' }
          : null;

  return (
    <span className="player-status-icons" aria-label="Player statuses">
      {primary ? (
        <span className="player-primary-status" title={primary.label}>
          <span aria-hidden="true">{primary.icon}</span>
          <span>{primary.label}</span>
        </span>
      ) : null}
      {isSelf && primary?.label !== 'You' && !primary?.label.startsWith('Your') ? <AccessibleIconTooltip label="You"><UserRound size={14} /></AccessibleIconTooltip> : null}
      {isHost ? <AccessibleIconTooltip label="Room host"><Crown size={14} /></AccessibleIconTooltip> : null}
      {hasLargestArmy ? <AwardBadge award="LargestArmy" /> : null}
      {hasLongestTrail ? <AwardBadge award="LongestTrail" /> : null}
    </span>
  );
}

export type ContextStateKind = 'setup-camp' | 'setup-trail' | 'before-roll' | 'after-roll' | 'warden' | 'road-building' | 'finished';

export function ContextStateIcon({ kind, value }: { kind: ContextStateKind; value?: number | string | null }) {
  const labels: Record<ContextStateKind, string> = {
    'setup-camp': 'Camp placement',
    'setup-trail': 'Trail placement',
    'before-roll': 'Dice roll required',
    'after-roll': `Dice result ${value ?? ''}`.trim(),
    warden: 'Warden action',
    'road-building': 'Road Building action',
    finished: 'Match winner'
  };

  return (
    <div aria-label={labels[kind]} className={`context-state-icon context-state-${kind}`} role="img">
      {kind === 'setup-camp' ? <PieceAsset decorative type="Camp" /> : null}
      {kind === 'setup-trail' ? <PieceAsset decorative type="Trail" /> : null}
      {kind === 'before-roll' || kind === 'after-roll' ? <ActionIcon decorative size="lg" type="RollDice" /> : null}
      {kind === 'warden' ? <PieceAsset decorative type="Warden" /> : null}
      {kind === 'road-building' ? <DevelopmentCardArtwork decorative type="RoadBuilding" /> : null}
      {kind === 'finished' ? <ActionIcon decorative size="lg" type="VictoryPoint" /> : null}
      {kind === 'after-roll' && value ? <b>{value}</b> : null}
    </div>
  );
}

export function ContextStatusIcon({ label, type, value }: { label: string; type: 'player' | 'dice' | 'piece' | 'progress' | 'score'; value: string }) {
  const icon = type === 'player'
    ? <UserRound size={16} />
    : type === 'dice'
      ? <ActionIcon decorative size="sm" type="RollDice" />
      : type === 'piece'
        ? <Hammer size={16} />
        : type === 'progress'
          ? <Clock3 size={16} />
          : <Star size={16} />;

  return <IconStat ariaLabel={`${label}: ${value}`} icon={icon} label={label} value={value} />;
}

export function AvailabilityStatus({ available, label }: { available: boolean; label: string }) {
  return (
    <span aria-label={label} className="availability-status" data-available={available} title={label}>
      {available ? <Check aria-hidden="true" size={14} /> : <LockKeyhole aria-hidden="true" size={14} />}
      <span>{label}</span>
    </span>
  );
}
