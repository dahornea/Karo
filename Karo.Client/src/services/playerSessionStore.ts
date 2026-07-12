import type { PlayerSession } from '../types/lobby';

export interface StoredPlayerSession extends PlayerSession {
  playerName: string;
}

const lastRoomKey = 'karo:last-session-room';

function roomKey(roomCode: string) {
  return `karo:session:${roomCode.toUpperCase()}`;
}

export function savePlayerSession(session: StoredPlayerSession) {
  sessionStorage.setItem(roomKey(session.roomCode), JSON.stringify(session));
  sessionStorage.setItem(lastRoomKey, session.roomCode.toUpperCase());
}

export function getActivePlayerSession(): StoredPlayerSession | null {
  const roomCode = sessionStorage.getItem(lastRoomKey);
  return roomCode ? getPlayerSession(roomCode) : null;
}

export function getPlayerSession(roomCode: string): StoredPlayerSession | null {
  const raw = sessionStorage.getItem(roomKey(roomCode));
  if (!raw) {
    return null;
  }

  try {
    const session = JSON.parse(raw) as Partial<StoredPlayerSession>;
    return typeof session.roomCode === 'string'
      && typeof session.playerId === 'string'
      && typeof session.reconnectToken === 'string'
      && typeof session.playerName === 'string'
      ? session as StoredPlayerSession
      : null;
  } catch {
    return null;
  }
}

export function clearPlayerSession(roomCode?: string) {
  const targetRoomCode = roomCode ?? sessionStorage.getItem(lastRoomKey);
  if (targetRoomCode) {
    sessionStorage.removeItem(roomKey(targetRoomCode));
  }

  if (!roomCode || sessionStorage.getItem(lastRoomKey) === roomCode.toUpperCase()) {
    sessionStorage.removeItem(lastRoomKey);
  }
}
