import type { GameState } from './game';

export type RoomStatus = 'Waiting' | 'InGame';

export interface Player {
  playerId: string;
  playerName: string;
  connectionId: string;
  joinedAt: string;
  isHost: boolean;
}

export interface Room {
  roomCode: string;
  hostConnectionId: string;
  status: RoomStatus;
  players: Player[];
}

export interface JoinRoomResult {
  playerId: string;
  room: Room;
}

export type GameStartedEvent = GameState;

export type LobbyEventName = 'RoomUpdated' | 'GameStarted';
