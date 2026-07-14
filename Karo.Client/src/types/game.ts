export type ResourceType = 'Wood' | 'Clay' | 'Wool' | 'Grain' | 'Stone';
export type TileResourceType = ResourceType | 'None';
export type GameStatus = 'InProgress' | 'Finished';
export type GamePhase = 'Setup' | 'NormalTurn' | 'Finished';
export type SetupRound = 'FirstPlacement' | 'SecondPlacement';
export type SetupStep = 'PlaceCamp' | 'PlaceTrail';
export type SetupDirection = 'Forward' | 'Reverse';
export type PortType = 'Generic3To1' | 'Specific2To1';
export type BoardStructureType = 'Camp' | 'Stronghold';
export type BankTradeRateSource = 'DefaultBank' | 'GenericPort' | 'SpecificPort';
export type HarborType = 'Generic' | ResourceType;
export type PlayerTradeOfferStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Cancelled' | 'Expired';

export type DevelopmentCardType =
  | 'Knight'
  | 'RoadBuilding'
  | 'YearOfPlenty'
  | 'Monopoly'
  | 'VictoryPoint';

export type DevelopmentCardStatus =
  | 'Playable'
  | 'BoughtThisTurn'
  | 'HiddenVictoryPoint'
  | 'AlreadyPlayed';

export interface HexTile {
  tileId: string;
  q: number;
  r: number;
  resourceType: TileResourceType;
  numberToken: number | null;
  isBlocked: boolean;
  adjacentTileIds: string[];
}

export interface BoardVertex {
  vertexId: string;
  x: number;
  y: number;
  isCoastal: boolean;
  adjacentTileIds: string[];
  adjacentVertexIds: string[];
  adjacentEdgeIds: string[];
  ownerPlayerId: string | null;
  structureType: BoardStructureType | null;
}

export interface BoardEdge {
  edgeId: string;
  startVertexId: string;
  endVertexId: string;
  adjacentTileIds: string[];
  ownerPlayerId: string | null;
}

export interface Port {
  id: string;
  type: PortType;
  resourceType: ResourceType | null;
  tileQ: number;
  tileR: number;
  edgeIndex: number;
  adjacentVertexIds: string[];
  displayLabel: string;
}

export interface HarborSlot {
  harborSlotId: string;
  adjacentVertexIds: string[];
  adjacentEdgeId: string;
  tileQ: number;
  tileR: number;
  edgeIndex: number;
  renderX: number;
  renderY: number;
  orientationDegrees: number;
  harborType: HarborType;
  tradeRate: number;
}

export interface BoardState {
  boardSeed: number;
  tiles: HexTile[];
  vertices: BoardVertex[];
  edges: BoardEdge[];
  harborSlots: HarborSlot[];
  ports: Port[];
}

export interface BoardIntegrityResult {
  boardSeed: number;
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface PlayerDevelopmentCard {
  cardId: string;
  type: DevelopmentCardType | null;
  purchasedTurn: number;
  isPlayed: boolean;
  status: DevelopmentCardStatus;
}

export interface ActiveDevelopmentCardEffect {
  type: 'RoadBuilding';
  cardId: string;
  freeTrailsPlaced: number;
  maxFreeTrails: number;
}

export type WardenAction = 'None' | 'Discarding' | 'MoveWarden' | 'ChooseVictim';

export interface WardenDiscardRequirement {
  playerId: string;
  requiredAmount: number;
}

export interface PlayerGameState {
  playerId: string;
  playerName: string;
  isHost: boolean;
  connectionStatus: import('./lobby').PlayerConnectionStatus;
  hasForfeited: boolean;
  playerColor: string;
  supplyCount: number;
  supplies: Record<ResourceType, number>;
  campsBuilt: number;
  strongholdsBuilt: number;
  trailsBuilt: number;
  totalTrails: number;
  remainingTrails: number;
  totalCamps: number;
  remainingCamps: number;
  totalStrongholds: number;
  remainingStrongholds: number;
  visibleVictoryPoints: number;
  totalVictoryPoints: number;
  hasLargestArmy: boolean;
  hasLongestTrail: boolean;
  longestTrailLength: number;
  playedKnightCount: number;
  hasPlayedDevelopmentCardThisTurn: boolean;
  developmentCardCount: number;
  developmentCards: PlayerDevelopmentCard[];
  accessiblePortIds: string[];
  accessibleHarborSlotIds: string[];
  tradeRates: BankTradeRate[];
}

export interface BankTradeRate {
  resource: ResourceType;
  rate: number;
  source: BankTradeRateSource;
  portId: string | null;
}

export interface PlayerTradeOffer {
  tradeOfferId: string;
  roomCode: string;
  turnNumber: number;
  proposerPlayerId: string;
  proposerName: string;
  targetPlayerId: string;
  targetName: string;
  offeredResources: Partial<Record<ResourceType, number>>;
  requestedResources: Partial<Record<ResourceType, number>>;
  status: PlayerTradeOfferStatus;
  createdAt: string;
  resolvedAt: string | null;
  canAccept: boolean;
  canReject: boolean;
  canCancel: boolean;
}

export interface GameLogEntry {
  sequence: number;
  createdAt: string;
  message: string;
  playerId: string | null;
}

export interface GameState {
  roomCode: string;
  status: GameStatus;
  matchId: string;
  gameStateVersion: number;
  pause: GamePauseState | null;
  phase: GamePhase;
  board: BoardState;
  players: PlayerGameState[];
  currentPlayerId: string;
  currentSetupPlayerId: string | null;
  playerOrder: string[];
  setupRound: SetupRound | null;
  setupStep: SetupStep | null;
  setupDirection: SetupDirection | null;
  lastSetupCampVertexId: string | null;
  turnNumber: number;
  lastDiceRoll: number | null;
  hasRolledThisTurn: boolean;
  winningVictoryPoints: number;
  developmentDeckCount: number;
  robberTileId: string;
  wardenTileId: string;
  pendingWardenAction: WardenAction;
  currentWardenPlayerId: string | null;
  pendingWardenDiscards: WardenDiscardRequirement[];
  wardenVictimOptions: string[];
  largestArmyPlayerId: string | null;
  largestArmyKnightCount: number;
  largestArmyAwardedAtTurn: number | null;
  longestTrailPlayerId: string | null;
  longestTrailLength: number;
  winnerPlayerId: string | null;
  activeDevelopmentCardEffect: ActiveDevelopmentCardEffect | null;
  tradeOffers: PlayerTradeOffer[];
  log: GameLogEntry[];
  startedAt: string;
}

export interface GamePauseState {
  isPaused: boolean;
  reason: string;
  disconnectedPlayerId: string;
  pausedAt: string;
  reconnectDeadline: string;
}

export interface DevelopmentCardActionPayload {
  selectedResources?: ResourceType[];
  selectedResource?: ResourceType;
  targetTileId?: string;
  victimPlayerId?: string | null;
}

export type DevelopmentDeckComposition = Partial<Record<DevelopmentCardType, number>>;

export interface PlayerDevelopmentCardSummary {
  developmentCards: PlayerDevelopmentCard[];
  developmentCardCount: number;
  playedKnightCount: number;
}

export interface OpponentDevelopmentCardSummary {
  playerId: string;
  playerName: string;
  developmentCardCount: number;
  playedKnightCount: number;
}

export const resources: ResourceType[] = ['Wood', 'Clay', 'Wool', 'Grain', 'Stone'];

export const developmentCardCost: Partial<Record<ResourceType, number>> = {
  Wool: 1,
  Grain: 1,
  Stone: 1
};
