import { RotateCcw, X } from 'lucide-react';
import type { ReactNode } from 'react';
import { useEffect, useMemo, useState } from 'react';
import type { BoardRendererMode } from '../types/boardRenderer';
import type { BoardDebugOptions } from '../types/debug';
import type { BoardIntegrityResult, DevelopmentCardType, DevelopmentDeckComposition, GameState, ResourceType, SetupStep } from '../types/game';
import { resources } from '../types/game';
import { ActionIcon, ResourceIcon } from './GameAsset';

interface DebugModePanelProps {
  game: GameState;
  playerId: string | null;
  pendingAction: string | null;
  boardOptions: BoardDebugOptions;
  boardRendererMode: BoardRendererMode;
  onBoardOptionsChange: (options: BoardDebugOptions) => void;
  onBoardRendererModeChange: (mode: BoardRendererMode) => void;
  onDisableDebugMode: () => void;
  onEndTurn: (roomCode: string) => Promise<void>;
  onDebugAddResource: (roomCode: string, targetPlayerId: string, resource: ResourceType, amount: number) => Promise<void>;
  onDebugSetTestingResources: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugClearResources: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugSetCurrentPlayer: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugForceDiceRoll: (roomCode: string, diceValue: number) => Promise<void>;
  onDebugResetRollState: (roomCode: string) => Promise<void>;
  onDebugMoveWarden: (roomCode: string, targetTileId: string) => Promise<void>;
  onDebugClearWardenState: (roomCode: string) => Promise<void>;
  onDebugSkipSetup: (roomCode: string) => Promise<void>;
  onDebugForceSetupStep: (roomCode: string, targetPlayerId: string, setupStep: SetupStep) => Promise<void>;
  onDebugSetVictoryPoints: (roomCode: string, targetPlayerId: string, points: number) => Promise<void>;
  onDebugTriggerWinCheck: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugRecalculateLongestTrail: (roomCode: string) => Promise<void>;
  onDebugGiveDevelopmentCard: (roomCode: string, targetPlayerId: string, cardType: DevelopmentCardType | 'Random') => Promise<void>;
  onDebugClearDevelopmentCards: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugResetDevelopmentCardPlayLimit: (roomCode: string, targetPlayerId: string) => Promise<void>;
  onDebugGetDevelopmentDeckComposition: (roomCode: string) => Promise<DevelopmentDeckComposition>;
  onDebugRestartMatch: (roomCode: string) => Promise<void>;
  onDebugRegenerateBoard: (roomCode: string, boardSeed: number) => Promise<void>;
  onDebugValidateBoard: (roomCode: string) => Promise<BoardIntegrityResult>;
}

type DebugSectionId = 'state' | 'resources' | 'turn' | 'board' | 'harbors' | 'cards' | 'actions';

const debugSections: Array<{ id: DebugSectionId; label: string }> = [
  { id: 'state', label: 'State' },
  { id: 'resources', label: 'Resources' },
  { id: 'turn', label: 'Turn + Dice' },
  { id: 'board', label: 'Board' },
  { id: 'harbors', label: 'Harbors' },
  { id: 'cards', label: 'Cards' },
  { id: 'actions', label: 'Game Actions' }
];

const diceValues = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
const developmentCardTypes: Array<DevelopmentCardType | 'Random'> = [
  'Random',
  'Knight',
  'RoadBuilding',
  'YearOfPlenty',
  'Monopoly',
  'VictoryPoint'
];

export function DebugModePanel({
  game,
  playerId,
  pendingAction,
  boardOptions,
  boardRendererMode,
  onBoardOptionsChange,
  onBoardRendererModeChange,
  onDisableDebugMode,
  onEndTurn,
  onDebugAddResource,
  onDebugSetTestingResources,
  onDebugClearResources,
  onDebugSetCurrentPlayer,
  onDebugForceDiceRoll,
  onDebugResetRollState,
  onDebugMoveWarden,
  onDebugClearWardenState,
  onDebugSkipSetup,
  onDebugForceSetupStep,
  onDebugSetVictoryPoints,
  onDebugTriggerWinCheck,
  onDebugRecalculateLongestTrail,
  onDebugGiveDevelopmentCard,
  onDebugClearDevelopmentCards,
  onDebugResetDevelopmentCardPlayLimit,
  onDebugGetDevelopmentDeckComposition,
  onDebugRestartMatch,
  onDebugRegenerateBoard,
  onDebugValidateBoard
}: DebugModePanelProps) {
  const me = game.players.find((player) => player.playerId === playerId) ?? null;
  const currentTurnPlayer = game.players.find((player) => player.playerId === game.currentPlayerId) ?? null;
  const currentSetupPlayer = game.players.find((player) => player.playerId === game.currentSetupPlayerId) ?? null;
  const [isOpen, setIsOpen] = useState(false);
  const [activeSection, setActiveSection] = useState<DebugSectionId>('state');
  const [targetPlayerId, setTargetPlayerId] = useState(playerId ?? game.players[0]?.playerId ?? '');
  const [victoryPoints, setVictoryPoints] = useState(10);
  const [deckComposition, setDeckComposition] = useState<DevelopmentDeckComposition | null>(null);
  const [boardSeed, setBoardSeed] = useState(String(game.board.boardSeed));
  const [boardIntegrity, setBoardIntegrity] = useState<BoardIntegrityResult | null>(null);
  const [debugWardenTileId, setDebugWardenTileId] = useState(game.wardenTileId ?? game.robberTileId);
  const targetPlayer = game.players.find((player) => player.playerId === targetPlayerId) ?? me ?? game.players[0] ?? null;
  const targetId = targetPlayer?.playerId ?? '';
  const isBusy = !!pendingAction || !targetId;
  const selectedDebugWardenTileId = game.board.tiles.some((tile) => tile.tileId === debugWardenTileId)
    ? debugWardenTileId
    : game.wardenTileId ?? game.robberTileId;

  const harborSummary = useMemo(() => {
    const counts = game.board.harborSlots.reduce<Record<string, number>>((summary, slot) => {
      const key = `${slot.harborType} ${slot.tradeRate}:1`;
      summary[key] = (summary[key] ?? 0) + 1;
      return summary;
    }, {});

    return Object.entries(counts)
      .map(([label, count]) => `${label} x${count}`)
      .join(' | ');
  }, [game.board.harborSlots]);

  const updateBoardOption = (key: keyof BoardDebugOptions, checked: boolean) => {
    onBoardOptionsChange({
      ...boardOptions,
      [key]: checked
    });
  };

  useEffect(() => {
    setBoardSeed(String(game.board.boardSeed));
    setBoardIntegrity(null);
  }, [game.board.boardSeed]);

  const loadDeckComposition = async () => {
    const composition = await onDebugGetDevelopmentDeckComposition(game.roomCode);
    setDeckComposition(composition);
  };

  const regenerateBoard = async () => {
    const seed = Number(boardSeed);
    if (!Number.isSafeInteger(seed)) {
      return;
    }

    await onDebugRegenerateBoard(game.roomCode, seed);
  };

  const validateBoard = async () => {
    setBoardIntegrity(await onDebugValidateBoard(game.roomCode));
  };

  const copyBoardSeed = () => {
    void navigator.clipboard?.writeText(String(game.board.boardSeed));
  };

  return (
    <>
      <button className="debug-panel-toggle" type="button" onClick={() => setIsOpen(true)}>
        <ActionIcon type="Debug" />
        Debug
      </button>

      {isOpen ? (
        <aside className="debug-drawer" aria-label="Debug Mode">
          <header className="debug-drawer-header">
            <div>
              <span className="debug-badge">
                <ActionIcon type="Debug" />
                Debug Mode
              </span>
              <p>{pendingAction ?? 'Ready'} - local development tools</p>
            </div>
            <div className="debug-header-actions">
              <button type="button" title="Collapse debug drawer" onClick={() => setIsOpen(false)}>
                <X size={16} />
              </button>
            </div>
          </header>

          <div className="debug-target-bar">
            <label className="debug-select-label">
              Target
              <select value={targetId} onChange={(event) => setTargetPlayerId(event.target.value)}>
                {game.players.map((player) => (
                  <option value={player.playerId} key={player.playerId}>
                    {player.playerName} {player.isHost ? '(host)' : ''}
                  </option>
                ))}
              </select>
            </label>
            <button type="button" className="debug-disable-button" onClick={onDisableDebugMode}>
              Disable
            </button>
          </div>

          <nav className="debug-tabs" aria-label="Debug sections">
            {debugSections.map((section) => (
              <button
                type="button"
                data-active={section.id === activeSection}
                key={section.id}
                onClick={() => setActiveSection(section.id)}
              >
                {section.label}
              </button>
            ))}
          </nav>

          <div className="debug-drawer-body">
            {activeSection === 'state' ? (
              <DebugSection title="State" meta={game.phase}>
                <div className="debug-info-grid">
                  <span>Room</span>
                  <b>{game.roomCode}</b>
                  <span>You</span>
                  <b>{me ? `${me.playerName} (${shortId(me.playerId)})` : 'Unknown'}</b>
                  <span>Turn</span>
                  <b>{game.turnNumber} | {currentTurnPlayer?.playerName ?? 'Unknown'}</b>
                  <span>Setup</span>
                  <b>{currentSetupPlayer?.playerName ?? 'None'} | {game.setupStep ?? 'None'}</b>
                  <span>Dice</span>
                  <b>{game.hasRolledThisTurn ? game.lastDiceRoll ?? 'Rolled' : 'Not rolled'}</b>
                  <span>Warden</span>
                  <b>{game.wardenTileId ?? game.robberTileId}</b>
                  <span>Warden Action</span>
                  <b>{game.pendingWardenAction}</b>
                  <span>Discarding</span>
                  <b>{game.pendingWardenDiscards.map((discard) => `${shortId(discard.playerId)}:${discard.requiredAmount}`).join(', ') || 'None'}</b>
                  <span>Victims</span>
                  <b>{game.wardenVictimOptions.map(shortId).join(', ') || 'None'}</b>
                  <span>Largest Army</span>
                  <b>{game.largestArmyPlayerId ? `${shortId(game.largestArmyPlayerId)}:${game.largestArmyKnightCount}` : 'None'}</b>
                  <span>Longest Trail</span>
                  <b>{game.longestTrailPlayerId ? `${shortId(game.longestTrailPlayerId)}:${game.longestTrailLength}` : 'None'}</b>
                  <span>Trail Lengths</span>
                  <b>{game.players.map((player) => `${shortId(player.playerId)}:${player.longestTrailLength}`).join(', ')}</b>
                  <span>Pieces</span>
                  <b>
                    {targetPlayer
                      ? `T ${targetPlayer.remainingTrails}/${targetPlayer.totalTrails} | C ${targetPlayer.remainingCamps}/${targetPlayer.totalCamps} | S ${targetPlayer.remainingStrongholds}/${targetPlayer.totalStrongholds}`
                      : 'None'}
                  </b>
                  <span>Pending</span>
                  <b>{pendingAction ?? 'None'}</b>
                </div>
              </DebugSection>
            ) : null}

            {activeSection === 'resources' ? (
              <DebugSection title="Resources" meta={`${targetPlayer?.supplyCount ?? 0} total`}>
                <div className="debug-resource-grid">
                  {resources.map((resource) => (
                    <div aria-label={`${resource}: ${targetPlayer?.supplies[resource] ?? 0}`} className="debug-resource-row" key={resource} title={resource}>
                      <span aria-hidden="true"><ResourceIcon decorative size="md" type={resource} /><span className="sr-only">{resource}</span></span>
                      <b>{targetPlayer?.supplies[resource] ?? 0}</b>
                      <button aria-label={`Add 1 ${resource}`} type="button" disabled={isBusy} onClick={() => void onDebugAddResource(game.roomCode, targetId, resource, 1)}>
                        +1
                      </button>
                      <button aria-label={`Add 5 ${resource}`} type="button" disabled={isBusy} onClick={() => void onDebugAddResource(game.roomCode, targetId, resource, 5)}>
                        +5
                      </button>
                    </div>
                  ))}
                </div>
                <div className="debug-button-row">
                  <button type="button" disabled={isBusy} onClick={() => void onDebugSetTestingResources(game.roomCode, targetId)}>
                    Set Testing
                  </button>
                  <button type="button" disabled={isBusy} onClick={() => void onDebugClearResources(game.roomCode, targetId)}>
                    Clear
                  </button>
                </div>
              </DebugSection>
            ) : null}

            {activeSection === 'turn' ? (
              <DebugSection title="Turn + Dice" meta={game.hasRolledThisTurn ? `Rolled ${game.lastDiceRoll}` : 'Roll open'}>
                <div className="debug-button-row">
                  <button type="button" disabled={isBusy} onClick={() => void onDebugSetCurrentPlayer(game.roomCode, targetId)}>
                    Force Turn
                  </button>
                  <button type="button" disabled={!!pendingAction || game.currentPlayerId !== playerId} onClick={() => void onEndTurn(game.roomCode)}>
                    End Turn
                  </button>
                  <button type="button" disabled={!!pendingAction} onClick={() => void onDebugResetRollState(game.roomCode)}>
                    <RotateCcw size={13} />
                    Allow Roll
                  </button>
                </div>
                <div className="debug-dice-grid">
                  {diceValues.map((value) => (
                    <button type="button" disabled={!!pendingAction} key={value} onClick={() => void onDebugForceDiceRoll(game.roomCode, value)}>
                      {value}
                    </button>
                  ))}
                </div>
                <label className="debug-select-label">
                  Warden Tile
                  <select value={selectedDebugWardenTileId} onChange={(event) => setDebugWardenTileId(event.target.value)}>
                    {game.board.tiles.map((tile) => (
                      <option value={tile.tileId} key={tile.tileId}>
                        {tile.resourceType} {tile.numberToken ?? ''} - {tile.tileId}
                      </option>
                    ))}
                  </select>
                </label>
                <div className="debug-button-row">
                  <button type="button" disabled={!!pendingAction} onClick={() => void onDebugMoveWarden(game.roomCode, selectedDebugWardenTileId)}>
                    Move Warden
                  </button>
                  <button type="button" disabled={!!pendingAction || game.pendingWardenAction === 'None'} onClick={() => void onDebugClearWardenState(game.roomCode)}>
                    Clear Warden State
                  </button>
                </div>
              </DebugSection>
            ) : null}

            {activeSection === 'board' ? (
              <DebugSection title="Board Inspector" meta={`Seed ${game.board.boardSeed}`}>
                <div className="debug-info-grid">
                  <span>Land</span>
                  <b>{game.board.tiles.length} tiles | {game.board.vertices.length} nodes | {game.board.edges.length} edges</b>
                  <span>Warden</span>
                  <b>{game.wardenTileId ?? game.robberTileId}</b>
                  <span>Harbors</span>
                  <b>{game.board.harborSlots.length} slots</b>
                  <span>Integrity</span>
                  <b>{boardIntegrity ? (boardIntegrity.isValid ? 'Valid' : `${boardIntegrity.errors.length} errors`) : 'Not checked'}</b>
                </div>
                <div className="debug-inline-controls">
                  <input
                    aria-label="Board seed"
                    inputMode="numeric"
                    value={boardSeed}
                    onChange={(event) => setBoardSeed(event.target.value)}
                  />
                  <button type="button" disabled={!!pendingAction} onClick={() => void copyBoardSeed()}>
                    Copy Seed
                  </button>
                  <button type="button" disabled={!!pendingAction || !Number.isSafeInteger(Number(boardSeed))} onClick={() => void regenerateBoard()}>
                    Regenerate
                  </button>
                  <button type="button" disabled={!!pendingAction} onClick={() => void validateBoard()}>
                    Validate
                  </button>
                </div>
                {boardIntegrity ? (
                  <div className="debug-small-text" role="status">
                    {boardIntegrity.isValid
                      ? `Seed ${boardIntegrity.boardSeed} passed the board integrity validator.`
                      : boardIntegrity.errors.join(' | ')}
                    {boardIntegrity.warnings.length > 0 ? ` Warnings: ${boardIntegrity.warnings.join(' | ')}` : ''}
                  </div>
                ) : null}
                <details className="debug-inspector-details">
                  <summary>Topology data</summary>
                  <div className="debug-inspector-list">
                    {game.board.tiles.map((tile) => (
                      <span key={tile.tileId}>
                        {tile.tileId} ({tile.q},{tile.r}) {tile.resourceType} {tile.numberToken ?? '-'} | tiles: {tile.adjacentTileIds.join(',')}
                      </span>
                    ))}
                  </div>
                  <div className="debug-inspector-list">
                    {game.board.vertices.map((vertex) => (
                      <span key={vertex.vertexId}>
                        {vertex.vertexId} {vertex.isCoastal ? 'coast' : 'inland'} | tiles: {vertex.adjacentTileIds.join(',')} | edges: {vertex.adjacentEdgeIds.join(',')}
                      </span>
                    ))}
                  </div>
                  <div className="debug-inspector-list">
                    {game.board.harborSlots.map((slot) => (
                      <span key={slot.harborSlotId}>
                        {slot.harborSlotId} {slot.harborType} {slot.tradeRate}:1 | nodes: {slot.adjacentVertexIds.join(',')}
                      </span>
                    ))}
                  </div>
                </details>
                <div className="debug-renderer-row" aria-label="Board renderer mode">
                  <span>Renderer</span>
                  <button type="button" data-active={boardRendererMode === '2d'} onClick={() => onBoardRendererModeChange('2d')}>
                    2D SVG
                  </button>
                  <button type="button" data-active={boardRendererMode === '3d'} onClick={() => onBoardRendererModeChange('3d')}>
                    3D Experimental
                  </button>
                </div>
                <p className="debug-small-text">
                  Current board renderer: {boardRendererMode === '3d' ? '3D experimental canvas' : '2D SVG fallback'}.
                </p>
                <div className="debug-toggle-grid">
                  <DebugToggle label="Tile IDs" checked={boardOptions.showTileIds} onChange={(checked) => updateBoardOption('showTileIds', checked)} />
                  <DebugToggle label="Node IDs" checked={boardOptions.showNodeIds} onChange={(checked) => updateBoardOption('showNodeIds', checked)} />
                  <DebugToggle label="Node Details" checked={boardOptions.showNodeDetails} onChange={(checked) => updateBoardOption('showNodeDetails', checked)} />
                  <DebugToggle label="Edge IDs" checked={boardOptions.showEdgeIds} onChange={(checked) => updateBoardOption('showEdgeIds', checked)} />
                  <DebugToggle label="Harbor IDs" checked={boardOptions.showHarborSlotIds} onChange={(checked) => updateBoardOption('showHarborSlotIds', checked)} />
                  <DebugToggle label="Open Nodes" checked={boardOptions.showValidBuildPlacements} onChange={(checked) => updateBoardOption('showValidBuildPlacements', checked)} />
                  <DebugToggle label="Coordinates" checked={boardOptions.showCoordinates} onChange={(checked) => updateBoardOption('showCoordinates', checked)} />
                  <DebugToggle label="Warden ID" checked={boardOptions.showRobberTileId} onChange={(checked) => updateBoardOption('showRobberTileId', checked)} />
                  <DebugToggle label="Harbor Details" checked={boardOptions.showHarborDetails} onChange={(checked) => updateBoardOption('showHarborDetails', checked)} />
                </div>
              </DebugSection>
            ) : null}

            {activeSection === 'harbors' ? (
              <DebugSection title="Harbors" meta={`${targetPlayer?.accessibleHarborSlotIds?.length ?? targetPlayer?.accessiblePortIds.length ?? 0} accessible`}>
                <p className="debug-small-text">{harborSummary}</p>
                <p className="debug-small-text">
                  Rates: {(targetPlayer?.tradeRates ?? []).map((rate) => `${rate.resource} ${rate.rate}:1`).join(' | ')}
                </p>
                <p className="debug-small-text">
                  Access: {(targetPlayer?.accessibleHarborSlotIds ?? targetPlayer?.accessiblePortIds ?? []).join(', ') || 'None'}
                </p>
              </DebugSection>
            ) : null}

            {activeSection === 'cards' ? (
              <DebugSection title="Development Cards" meta={`Deck ${game.developmentDeckCount}`}>
                <div className="debug-button-row">
                  <button type="button" disabled={!!pendingAction} onClick={() => void loadDeckComposition()}>
                    Load Deck Composition
                  </button>
                </div>
                <p className="debug-small-text">
                  Deck: {deckComposition
                    ? developmentCardTypes
                        .filter((type): type is DevelopmentCardType => type !== 'Random')
                        .map((type) => `${formatCardType(type)} ${deckComposition[type] ?? 0}`)
                        .join(' | ')
                    : `${game.developmentDeckCount} cards remaining`}
                  {' '}| Knights played: {targetPlayer?.playedKnightCount ?? 0}
                </p>
                <div className="debug-card-list">
                  {(targetPlayer?.developmentCards ?? []).map((card) => (
                    <span key={card.cardId}>
                      {card.type ?? 'Hidden'} | {card.status}
                    </span>
                  ))}
                  {(targetPlayer?.developmentCards.length ?? 0) === 0 ? <span>No cards</span> : null}
                </div>
                <div className="debug-dice-grid">
                  {developmentCardTypes.map((type) => (
                    <button type="button" disabled={isBusy} key={type} onClick={() => void onDebugGiveDevelopmentCard(game.roomCode, targetId, type)}>
                      Draw {formatCardType(type)}
                    </button>
                  ))}
                </div>
                <div className="debug-button-row">
                  <button type="button" disabled={isBusy} onClick={() => void onDebugClearDevelopmentCards(game.roomCode, targetId)}>
                    Clear Cards
                  </button>
                  <button type="button" disabled={isBusy} onClick={() => void onDebugResetDevelopmentCardPlayLimit(game.roomCode, targetId)}>
                    Reset Play Limit
                  </button>
                </div>
              </DebugSection>
            ) : null}

            {activeSection === 'actions' ? (
              <DebugSection title="Game Actions" meta={`${targetPlayer?.totalVictoryPoints ?? 0} VP`}>
                <div className="debug-button-row">
                  <button type="button" disabled={!!pendingAction || game.phase !== 'Setup'} onClick={() => void onDebugSkipSetup(game.roomCode)}>
                    Skip Setup
                  </button>
                  <button type="button" disabled={isBusy} onClick={() => void onDebugForceSetupStep(game.roomCode, targetId, 'PlaceCamp')}>
                    Force Setup Camp
                  </button>
                  <button type="button" disabled={isBusy} onClick={() => void onDebugForceSetupStep(game.roomCode, targetId, 'PlaceTrail')}>
                    Force Setup Trail
                  </button>
                </div>
                <div className="debug-inline-controls">
                  <input
                    min="0"
                    type="number"
                    value={victoryPoints}
                    onChange={(event) => setVictoryPoints(Number(event.target.value))}
                  />
                  <button type="button" disabled={isBusy} onClick={() => void onDebugSetVictoryPoints(game.roomCode, targetId, victoryPoints)}>
                    Set VP
                  </button>
                </div>
                <div className="debug-button-row">
                  <button type="button" disabled={isBusy} onClick={() => void onDebugTriggerWinCheck(game.roomCode, targetId)}>
                    Trigger Win Check
                  </button>
                  <button type="button" disabled={!!pendingAction} onClick={() => void onDebugRecalculateLongestTrail(game.roomCode)}>
                    Recalc Longest Trail
                  </button>
                  <button type="button" disabled={!!pendingAction} onClick={() => void onDebugRestartMatch(game.roomCode)}>
                    Restart Match
                  </button>
                </div>
              </DebugSection>
            ) : null}
          </div>
        </aside>
      ) : null}
    </>
  );
}

function DebugSection({
  title,
  meta,
  children
}: {
  title: string;
  meta: string;
  children: ReactNode;
}) {
  return (
    <section className="debug-section">
      <div className="debug-section-heading">
        <strong>{title}</strong>
        <span>{meta}</span>
      </div>
      {children}
    </section>
  );
}

function DebugToggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (checked: boolean) => void }) {
  return (
    <label className="debug-toggle">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

function shortId(value: string) {
  return value.slice(0, 6);
}

function formatCardType(type: DevelopmentCardType | 'Random') {
  return type.replace(/([A-Z])/g, ' $1').trim();
}
