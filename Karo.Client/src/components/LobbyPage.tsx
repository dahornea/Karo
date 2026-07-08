import { Clipboard, Crown, Play } from 'lucide-react';
import type { Room } from '../types/lobby';

interface LobbyPageProps {
  playerId: string | null;
  room: Room;
  pendingAction: string | null;
  onStartGame: (roomCode: string) => Promise<void>;
}

const playerColors = ['#d95f43', '#2f7f75', '#4269b2', '#d6a230'];

export function LobbyPage({ playerId, room, pendingAction, onStartGame }: LobbyPageProps) {
  const me = room.players.find((player) => player.playerId === playerId);
  const isHost = !!me?.isHost;

  const copyRoomCode = () => {
    void navigator.clipboard?.writeText(room.roomCode);
  };

  return (
    <section className="lobby-screen">
      <header className="lobby-header">
        <div>
          <p className="eyebrow">Karo Lobby</p>
          <h1>Room {room.roomCode}</h1>
          <p className="game-subtitle">{room.players.length} player{room.players.length === 1 ? '' : 's'} seated at the table</p>
        </div>
        <button className="icon-button" type="button" title="Copy room code" onClick={copyRoomCode}>
          <Clipboard size={18} />
          <span>Copy</span>
        </button>
      </header>

      <div className="lobby-layout">
        <section className="table-panel">
          <div className="panel-heading">
            <h2>Players</h2>
            <span>{room.players.length}/4</span>
          </div>

          <div className="player-list">
            {room.players.map((player, index) => (
              <article className="player-row" data-self={player.playerId === playerId} key={player.playerId}>
                <span className="player-seat-avatar" style={{ backgroundColor: playerColors[index % playerColors.length] }}>
                  {player.playerName.slice(0, 1).toUpperCase()}
                </span>
                <div>
                  <strong>{player.playerName}</strong>
                  <span>{player.playerId === playerId ? 'You' : 'Connected'}</span>
                </div>
                {player.isHost ? (
                  <span className="host-badge">
                    <Crown size={14} />
                    Host
                  </span>
                ) : null}
              </article>
            ))}
          </div>
        </section>

        <aside className="ready-panel">
          <div>
            <p className="eyebrow">Status</p>
            <h2>{isHost ? 'Ready to start' : 'Waiting for host'}</h2>
            <p className="panel-note">
              {room.status === 'Waiting' ? 'Players can join while the room is waiting.' : 'The match has started.'}
            </p>
          </div>

          <div className="lobby-room-code">
            <span>Room code</span>
            <strong>{room.roomCode}</strong>
          </div>

          {isHost ? (
            <button className="primary-button" type="button" disabled={!!pendingAction} onClick={() => void onStartGame(room.roomCode)}>
              <Play size={18} />
              <span>{pendingAction === 'Starting game' ? 'Starting...' : 'Start Game'}</span>
            </button>
          ) : (
            <div className="waiting-bar" aria-hidden="true">
              <span />
            </div>
          )}
        </aside>
      </div>
    </section>
  );
}
