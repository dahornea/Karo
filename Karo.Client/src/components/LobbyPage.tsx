import { Check, Clipboard, Crown, LogOut, Play, RefreshCw } from 'lucide-react';
import type { Room } from '../types/lobby';

interface LobbyPageProps {
  playerId: string | null;
  room: Room;
  pendingAction: string | null;
  onStartGame: (roomCode: string) => Promise<void>;
  onSetReady: (roomCode: string, isReady: boolean) => Promise<void>;
  onRecoverSession: () => Promise<void>;
  onLeaveRoom: (roomCode: string) => Promise<void>;
}

const playerColors = ['#d95f43', '#2f7f75', '#4269b2', '#d6a230'];

export function LobbyPage({ playerId, room, pendingAction, onStartGame, onSetReady, onRecoverSession, onLeaveRoom }: LobbyPageProps) {
  const me = room.players.find((player) => player.playerId === playerId);
  const isHost = !!me?.isHost;
  const eligiblePlayers = room.players.filter((player) =>
    !player.hasForfeited
    && player.connectionStatus !== 'TimedOut'
    && player.connectionStatus !== 'Left'
    && player.connectionStatus !== 'Forfeited'
  );
  const allReady = eligiblePlayers.length > 0
    && eligiblePlayers.every((player) => player.connectionStatus === 'Connected' && player.isReady);

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
                <span className="player-seat-avatar" style={{ backgroundColor: player.playerColor || playerColors[index % playerColors.length] }}>
                  {player.playerName.slice(0, 1).toUpperCase()}
                </span>
                <div>
                  <strong>{player.playerName}</strong>
                  <span>{player.playerId === playerId ? `You · ${player.connectionStatus}` : player.connectionStatus}</span>
                </div>
                {player.isHost ? (
                  <span className="host-badge">
                    <Crown size={14} />
                    Host
                  </span>
                ) : null}
                {player.isReady ? <span className="ready-badge"><Check size={13} /> Ready</span> : null}
              </article>
            ))}
          </div>
        </section>

        <aside className="ready-panel">
          <div>
            <p className="eyebrow">Status</p>
            <h2>{allReady ? (isHost ? 'Table ready' : 'Waiting for host') : 'Waiting for players'}</h2>
            <p className="panel-note">
              {room.status === 'Waiting' ? 'Every connected player must be ready before the host starts.' : 'The match has started.'}
            </p>
          </div>

          <div className="lobby-room-code">
            <span>Room code</span>
            <strong>{room.roomCode}</strong>
          </div>

          {!me ? (
            <div className="lobby-seat-recovery" role="status">
              <p className="panel-note">This browser lost its local seat identity. Recover it to ready up and start the match.</p>
              <button className="primary-button" disabled={!!pendingAction} type="button" onClick={() => void onRecoverSession()}>
                <RefreshCw size={18} />
                <span>{pendingAction === 'Recovering your seat' ? 'Recovering...' : 'Recover My Seat'}</span>
              </button>
            </div>
          ) : (
            <button className="secondary-button" type="button" disabled={!!pendingAction} onClick={() => void onSetReady(room.roomCode, !me.isReady)}>
              <Check size={18} />
              <span>{me.isReady ? 'Not Ready' : 'Ready Up'}</span>
            </button>
          )}

          {me && isHost ? (
            <button className="primary-button" type="button" disabled={!!pendingAction || !allReady} onClick={() => void onStartGame(room.roomCode)}>
              <Play size={18} />
              <span>{pendingAction === 'Starting game' ? 'Starting...' : 'Start Game'}</span>
            </button>
          ) : (
            <div className="waiting-bar" aria-hidden="true">
              <span />
            </div>
          )}

          <button className="text-button lobby-leave-button" type="button" disabled={!!pendingAction} onClick={() => {
            if (window.confirm('Leave this Karo room?')) {
              void onLeaveRoom(room.roomCode);
            }
          }}>
            <LogOut size={15} /> Leave Room
          </button>
        </aside>
      </div>
    </section>
  );
}
