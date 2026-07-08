import { Crown, Shield, Users } from 'lucide-react';
import type { GameState } from '../types/game';

interface PlayerPanelProps {
  game: GameState;
  playerId: string | null;
}

const playerColors = ['#d95f43', '#2f7f75', '#4269b2', '#d6a230'];

export function PlayerPanel({ game, playerId }: PlayerPanelProps) {
  const activePlayerId = game.phase === 'Setup' ? game.currentSetupPlayerId : game.currentPlayerId;

  return (
    <aside className="game-side-card player-panel">
      <div className="panel-heading">
        <h2>Players</h2>
        <Users size={16} />
      </div>

      <div className="game-player-list">
        {game.players.map((player, index) => (
          <article
            className="game-player-card"
            data-active={player.playerId === activePlayerId}
            data-self={player.playerId === playerId}
            key={player.playerId}
          >
            <span className="game-player-avatar" style={{ backgroundColor: playerColors[index % playerColors.length] }}>
              {player.playerName.slice(0, 1).toUpperCase()}
            </span>
            <div className="game-player-main">
              <div className="game-player-name-row">
                <strong>{player.playerName}</strong>
                <div className="player-badges">
                  {player.playerId === playerId ? <span className="mini-badge">You</span> : null}
                  {player.playerId === activePlayerId ? <span className="mini-badge">{game.phase === 'Setup' ? 'Setup' : 'Turn'}</span> : null}
                  {player.isHost ? (
                    <span className="mini-badge">
                      <Crown size={13} />
                      Host
                    </span>
                  ) : null}
                  {player.hasLargestArmy ? (
                    <span className="mini-badge">
                      <Shield size={13} />
                      Largest Army
                    </span>
                  ) : null}
                </div>
              </div>
              <div className="player-stat-row" aria-label={`${player.playerName} summary`}>
                <span>
                  <b>{player.totalVictoryPoints}</b>
                  VP
                </span>
                <span>
                  <b>{player.supplyCount}</b>
                  supplies
                </span>
                <span>
                  <b>{player.developmentCardCount}</b>
                  cards
                </span>
                <span>
                  <b>{player.playedKnightCount}</b>
                  Knights
                </span>
              </div>
            </div>
          </article>
        ))}
      </div>
    </aside>
  );
}
