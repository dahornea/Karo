import { CircleDot, Crown, Hash, Layers, Map, Signal, Users } from 'lucide-react';
import type { ConnectionPhase } from '../hooks/useLobbyConnection';
import type { GameState } from '../types/game';

interface GameHeaderProps {
  connectionPhase: ConnectionPhase;
  game: GameState;
}

export function GameHeader({ connectionPhase, game }: GameHeaderProps) {
  const currentPlayer = game.phase === 'Setup'
    ? game.players.find((player) => player.playerId === game.currentSetupPlayerId)
    : game.players.find((player) => player.playerId === game.currentPlayerId);
  const subtitle = game.phase === 'Setup'
    ? `Setup - ${currentPlayer?.playerName ?? 'Player'} ${game.setupStep === 'PlaceTrail' ? 'places a Trail' : 'places a Camp'}`
    : `Turn ${game.turnNumber} - ${currentPlayer?.playerName ?? 'Player'}'s turn`;
  const phaseLabel = game.phase === 'Setup'
    ? 'Setup'
    : game.pendingWardenAction !== 'None'
      ? 'Warden'
      : game.hasRolledThisTurn
        ? `Rolled ${game.lastDiceRoll}`
        : 'Roll needed';

  return (
    <header className="game-header">
      <div className="game-brand">
        <div className="game-logo">K</div>
        <div>
          <p className="eyebrow">Karo Match</p>
          <h1>Karo</h1>
          <p className="game-subtitle">{subtitle}</p>
        </div>
      </div>

      <div className="game-stats">
        <span className="stat-pill">
          <Hash size={16} />
          {game.roomCode}
        </span>
        <span className="stat-pill stat-pill-primary">
          <CircleDot size={16} />
          {phaseLabel}
        </span>
        <span className="stat-pill">
          <Crown size={16} />
          {game.winningVictoryPoints} VP
        </span>
        <span className="stat-pill">
          <Layers size={16} />
          {game.phase === 'Setup' ? 'setup' : `${game.developmentDeckCount} cards`}
        </span>
        <span className="stat-pill">
          <Users size={16} />
          {game.players.length} players
        </span>
        <span className="stat-pill">
          <Map size={16} />
          {game.board.tiles.length} regions
        </span>
        <span className="stat-pill" data-state={connectionPhase}>
          <Signal size={16} />
          <span className="status-dot" />
          {connectionPhase}
        </span>
      </div>
    </header>
  );
}
