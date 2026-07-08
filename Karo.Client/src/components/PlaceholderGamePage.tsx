import { Construction, Users } from 'lucide-react';
import type { Room } from '../types/lobby';

interface PlaceholderGamePageProps {
  room: Room;
}

const playerColors = ['#d95f43', '#2f7f75', '#4269b2', '#d6a230'];

export function PlaceholderGamePage({ room }: PlaceholderGamePageProps) {
  return (
    <section className="match-screen">
      <header className="match-header">
        <div>
          <p className="eyebrow">Room {room.roomCode}</p>
          <h1>Karo Match</h1>
        </div>
        <div className="match-status">
          <Construction size={18} />
          <span>Milestone 1</span>
        </div>
      </header>

      <div className="match-layout">
        <section className="table-panel placeholder-panel">
          <Construction size={44} />
          <h2>Game board coming in Milestone 2</h2>
          <p>
            The real-time lobby foundation is working. The shared board, turns, supplies, and trading will arrive in later milestones.
          </p>
        </section>

        <aside className="table-panel">
          <div className="panel-heading">
            <h2>Players</h2>
            <Users size={16} />
          </div>
          <div className="player-list">
            {room.players.map((player, index) => (
              <article className="player-row" key={player.playerId}>
                <span className="player-color" style={{ backgroundColor: playerColors[index % playerColors.length] }} />
                <div>
                  <strong>{player.playerName}</strong>
                  <span>{player.isHost ? 'Host' : 'Player'}</span>
                </div>
              </article>
            ))}
          </div>
        </aside>
      </div>
    </section>
  );
}
