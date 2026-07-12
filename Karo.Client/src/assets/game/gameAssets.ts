import type { DevelopmentCardType, HarborType, ResourceType, TileResourceType } from '../../types/game';
import buildIcon from './actions/build.svg';
import cardsIcon from './actions/cards.svg';
import closeIcon from './actions/close.svg';
import debugIcon from './actions/debug.svg';
import endTurnIcon from './actions/end-turn.svg';
import gameLogIcon from './actions/game-log.svg';
import matchDetailsIcon from './actions/match-details.svg';
import moveWardenIcon from './actions/move-warden.svg';
import rollDiceIcon from './actions/roll-dice.svg';
import suppliesIcon from './actions/supplies.svg';
import tradeIcon from './actions/trade.svg';
import victoryPointIcon from './actions/victory-point.svg';
import cardBackArt from './cards/card-back.webp';
import knightArt from './cards/knight.webp';
import monopolyArt from './cards/monopoly.webp';
import roadBuildingArt from './cards/road-building.webp';
import victoryPointArt from './cards/victory-point.webp';
import yearOfPlentyArt from './cards/year-of-plenty.webp';
import genericHarborIcon from './harbors/generic.svg';
import campArt from './pieces/camp.webp';
import strongholdArt from './pieces/stronghold.webp';
import trailIcon from './pieces/trail.svg';
import wardenArt from './pieces/warden.webp';
import clayIcon from './resources/clay.svg';
import desertIcon from './resources/desert.svg';
import grainIcon from './resources/grain.svg';
import stoneIcon from './resources/stone.svg';
import woodIcon from './resources/wood.svg';
import woolIcon from './resources/wool.svg';
import clayTerrain from './terrain/clay.webp';
import desertTerrain from './terrain/desert.webp';
import grainTerrain from './terrain/grain.webp';
import stoneTerrain from './terrain/stone.webp';
import woodTerrain from './terrain/wood.webp';
import woolTerrain from './terrain/wool.webp';

export type PieceType = 'Trail' | 'Camp' | 'Stronghold' | 'Warden';
export type ActionAssetType =
  | 'Build'
  | 'Trade'
  | 'Cards'
  | 'GameLog'
  | 'RollDice'
  | 'EndTurn'
  | 'MoveWarden'
  | 'Close'
  | 'MatchDetails'
  | 'Debug'
  | 'Supplies'
  | 'VictoryPoint';

export interface GameAssetDefinition {
  src: string;
  label: string;
  format: 'svg' | 'webp' | 'glb';
}

const resourceAssets = {
  Wood: { src: woodIcon, label: 'Wood', format: 'svg' },
  Clay: { src: clayIcon, label: 'Clay', format: 'svg' },
  Wool: { src: woolIcon, label: 'Wool', format: 'svg' },
  Grain: { src: grainIcon, label: 'Grain', format: 'svg' },
  Stone: { src: stoneIcon, label: 'Stone', format: 'svg' }
} satisfies Record<ResourceType, GameAssetDefinition>;

const terrainAssets = {
  Wood: { src: woodTerrain, label: 'Wood terrain', format: 'webp' },
  Clay: { src: clayTerrain, label: 'Clay terrain', format: 'webp' },
  Wool: { src: woolTerrain, label: 'Wool terrain', format: 'webp' },
  Grain: { src: grainTerrain, label: 'Grain terrain', format: 'webp' },
  Stone: { src: stoneTerrain, label: 'Stone terrain', format: 'webp' },
  None: { src: desertTerrain, label: 'Desert terrain', format: 'webp' }
} satisfies Record<TileResourceType, GameAssetDefinition>;

const tileSymbolAssets = {
  ...resourceAssets,
  None: { src: desertIcon, label: 'Desert', format: 'svg' }
} satisfies Record<TileResourceType, GameAssetDefinition>;

const pieceAssets = {
  Trail: { src: trailIcon, label: 'Trail', format: 'svg' },
  Camp: { src: campArt, label: 'Camp', format: 'webp' },
  Stronghold: { src: strongholdArt, label: 'Stronghold', format: 'webp' },
  Warden: { src: wardenArt, label: 'Warden', format: 'webp' }
} satisfies Record<PieceType, GameAssetDefinition>;

const cardAssets = {
  Knight: { src: knightArt, label: 'Knight', format: 'webp' },
  RoadBuilding: { src: roadBuildingArt, label: 'Road Building', format: 'webp' },
  YearOfPlenty: { src: yearOfPlentyArt, label: 'Year of Plenty', format: 'webp' },
  Monopoly: { src: monopolyArt, label: 'Monopoly', format: 'webp' },
  VictoryPoint: { src: victoryPointArt, label: 'Victory Point', format: 'webp' }
} satisfies Record<DevelopmentCardType, GameAssetDefinition>;

const actionAssets = {
  Build: { src: buildIcon, label: 'Build', format: 'svg' },
  Trade: { src: tradeIcon, label: 'Trade', format: 'svg' },
  Cards: { src: cardsIcon, label: 'Development Cards', format: 'svg' },
  GameLog: { src: gameLogIcon, label: 'Game Log', format: 'svg' },
  RollDice: { src: rollDiceIcon, label: 'Roll Dice', format: 'svg' },
  EndTurn: { src: endTurnIcon, label: 'End Turn', format: 'svg' },
  MoveWarden: { src: moveWardenIcon, label: 'Move Warden', format: 'svg' },
  Close: { src: closeIcon, label: 'Close', format: 'svg' },
  MatchDetails: { src: matchDetailsIcon, label: 'Match Details', format: 'svg' },
  Debug: { src: debugIcon, label: 'Debug', format: 'svg' },
  Supplies: { src: suppliesIcon, label: 'Supplies', format: 'svg' },
  VictoryPoint: { src: victoryPointIcon, label: 'Victory Points', format: 'svg' }
} satisfies Record<ActionAssetType, GameAssetDefinition>;

const harborAssets = {
  Generic: { src: genericHarborIcon, label: 'Generic harbor', format: 'svg' },
  Wood: resourceAssets.Wood,
  Clay: resourceAssets.Clay,
  Wool: resourceAssets.Wool,
  Grain: resourceAssets.Grain,
  Stone: resourceAssets.Stone
} satisfies Record<HarborType, GameAssetDefinition>;

const modelAssets3d: Record<PieceType, GameAssetDefinition | null> = {
  Camp: null,
  Stronghold: null,
  Trail: null,
  Warden: null
};

export const gameAssets = {
  resources: resourceAssets,
  actions: actionAssets,
  pieces: pieceAssets,
  cards: cardAssets,
  cardBack: { src: cardBackArt, label: 'Karo Development Card back', format: 'webp' } satisfies GameAssetDefinition,
  terrain: terrainAssets,
  symbols: tileSymbolAssets,
  harbors: harborAssets,
  models3d: modelAssets3d
} as const;

export function getGameAsset<T extends Record<string, GameAssetDefinition>>(mapping: T, key: keyof T) {
  const asset = mapping[key];
  if (!asset && import.meta.env.DEV) {
    console.warn(`[Karo assets] Missing asset mapping for ${String(key)}.`);
  }
  return asset ?? gameAssets.harbors.Generic;
}
