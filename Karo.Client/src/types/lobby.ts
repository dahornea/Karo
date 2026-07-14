import type { GameState } from './game';

export type RoomStatus = 'Waiting' | 'InGame' | 'PostGame';
export type PlayerConnectionStatus = 'Connected' | 'Reconnecting' | 'Disconnected' | 'TimedOut' | 'Left' | 'Forfeited';

export interface Player {
  playerId: string;
  playerName: string;
  connectionStatus: PlayerConnectionStatus;
  joinedAt: string;
  lastSeenAt: string;
  disconnectedAt: string | null;
  isHost: boolean;
  isReady: boolean;
  hasForfeited: boolean;
  playerColor: string;
}

export interface Room {
  roomCode: string;
  hostPlayerId: string;
  status: RoomStatus;
  roomStateVersion: number;
  players: Player[];
}

export interface PlayerSession {
  roomCode: string;
  playerId: string;
  reconnectToken: string;
}

export interface JoinRoomResult {
  playerId: string;
  room: Room;
  session: PlayerSession;
}

export interface ResumeRoomSessionResult {
  room: Room;
  game: GameState | null;
  session: PlayerSession;
}

export type GameStartedEvent = GameState;

export type LobbyEventName = 'RoomUpdated' | 'GameStarted' | 'GameUpdated' | 'ReturnedToLobby';
