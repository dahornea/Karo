import { Hash, Signal } from 'lucide-react';
import type { ConnectionPhase } from '../hooks/useLobbyConnection';
import type { GameState } from '../types/game';
import type { ActionAssetType } from '../assets/game/gameAssets';
import { ActionIcon } from './GameAsset';

interface GameHeaderProps {
  connectionPhase: ConnectionPhase;
  game: GameState;
  onOpenMatchDetails: () => void;
}

export function GameHeader({ connectionPhase, game, onOpenMatchDetails }: GameHeaderProps) {
  const phaseLabel = game.phase === 'Setup'
    ? `Setup ${game.setupRound === 'SecondPlacement' ? '2' : '1'}`
    : game.pendingWardenAction !== 'None'
      ? 'Warden'
      : game.activeDevelopmentCardEffect
        ? 'Card action'
      : game.status === 'Finished'
        ? 'Finished'
      : game.hasRolledThisTurn
        ? `Turn ${game.turnNumber}`
        : `Turn ${game.turnNumber}`;
  const phaseAsset: ActionAssetType = game.phase === 'Setup'
    ? 'Build'
    : game.pendingWardenAction !== 'None'
      ? 'MoveWarden'
      : game.activeDevelopmentCardEffect
        ? 'Cards'
        : game.status === 'Finished'
          ? 'VictoryPoint'
          : game.hasRolledThisTurn
            ? 'EndTurn'
            : 'RollDice';

  return (
    <header className="game-header">
      <div className="game-brand">
        <div className="game-logo">K</div>
        <div>
          <p className="eyebrow">Karo Match</p>
          <h1>Karo</h1>
        </div>
      </div>

      <div className="game-stats">
        <span className="stat-pill">
          <Hash size={16} />
          {game.roomCode}
        </span>
        <span className="stat-pill stat-pill-primary">
          <ActionIcon decorative size="xs" type={phaseAsset} />
          {phaseLabel}
        </span>
        <span className="stat-pill" data-state={connectionPhase}>
          <Signal size={16} />
          <span className="status-dot" />
          {connectionPhase}
        </span>
        <button className="stat-pill match-details-button" type="button" onClick={onOpenMatchDetails}>
          <ActionIcon decorative size="xs" type="MatchDetails" />
          Details
        </button>
      </div>
    </header>
  );
}
