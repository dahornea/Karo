export interface BoardDebugOptions {
  showTileIds: boolean;
  showNodeIds: boolean;
  showNodeDetails: boolean;
  showEdgeIds: boolean;
  showHarborSlotIds: boolean;
  showValidBuildPlacements: boolean;
  showCoordinates: boolean;
  showRobberTileId: boolean;
  showHarborDetails: boolean;
}

export const defaultBoardDebugOptions: BoardDebugOptions = {
  showTileIds: false,
  showNodeIds: false,
  showNodeDetails: false,
  showEdgeIds: false,
  showHarborSlotIds: false,
  showValidBuildPlacements: false,
  showCoordinates: false,
  showRobberTileId: false,
  showHarborDetails: false
};
