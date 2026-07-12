import { Anchor, ChevronDown, Shield, Users } from 'lucide-react';
import { useState } from 'react';
import type { GameState } from '../types/game';
import { getPlayerStatusLabel } from '../utils/sidePanels';
import { ActionIcon, DevelopmentCardArtwork } from './GameAsset';
import { IconStat, PieceCount, PlayerStatusIcons } from './MatchIconUI';

interface PlayerPanelProps {
  game: GameState;
  playerId: string | null;
}

export function PlayerPanel({ game, playerId }: PlayerPanelProps) {
  const [expandedPlayerId, setExpandedPlayerId] = useState<string | null>(null);
  const activePlayerId = game.phase === 'Setup' ? game.currentSetupPlayerId : game.currentPlayerId;

  return (
    <aside className="game-side-card player-panel compact-player-rail">
      <div className="panel-heading">
        <h2>Players</h2>
        <Users size={16} />
      </div>

      <div className="game-player-list">
        {game.players.map((player, index) => {
          const isActive = player.playerId === activePlayerId && game.status !== 'Finished';
          const isSelf = player.playerId === playerId;
          const isWinner = player.playerId === game.winnerPlayerId;
          const isExpanded = expandedPlayerId === player.playerId;
          const statusLabel = player.hasForfeited
            ? 'Forfeited'
            : player.connectionStatus !== 'Connected'
              ? player.connectionStatus
              : getPlayerStatusLabel({ isActive, isSelf, isWinner, phase: game.phase });
          const detailsId = `player-details-${player.playerId}`;

          return (
            <article
              aria-current={isActive ? 'step' : undefined}
              aria-label={`${player.playerName}: ${statusLabel}, ${player.totalVictoryPoints} Victory Points, ${player.supplyCount} Supplies, ${player.developmentCardCount} Development Cards`}
              className="game-player-card player-summary-card"
              data-active={isActive}
              data-expanded={isExpanded}
              data-self={isSelf}
              data-winner={isWinner}
              key={player.playerId}
            >
              <button
                aria-controls={detailsId}
                aria-expanded={isExpanded}
                className="player-card-toggle"
                title={`${isExpanded ? 'Hide' : 'Show'} ${player.playerName} details`}
                type="button"
                onClick={() => setExpandedPlayerId(isExpanded ? null : player.playerId)}
              >
                <span className="player-identity-row">
                  <span className="game-player-avatar" style={{ backgroundColor: player.playerColor || ['#d95f43', '#2f7f75', '#4269b2', '#d6a230'][index % 4] }}>
                    {player.playerName.slice(0, 1).toUpperCase()}
                  </span>
                  <span className="player-name-stack">
                    <strong>{player.playerName}</strong>
                    <small className={`player-connection-state connection-${player.connectionStatus.toLowerCase()}`}>{statusLabel}</small>
                    <PlayerStatusIcons
                      hasLargestArmy={player.hasLargestArmy}
                      hasLongestTrail={player.hasLongestTrail}
                      isActive={isActive}
                      isHost={player.isHost}
                      isSelf={isSelf}
                      isWinner={isWinner}
                      phase={game.phase}
                    />
                  </span>
                  <ChevronDown aria-hidden="true" className="player-expand-icon" size={15} />
                </span>

                <span className="player-stat-row" aria-label={`${player.playerName} public summary`}>
                  <IconStat icon={<ActionIcon decorative size="sm" type="VictoryPoint" />} label="Victory Points" value={player.totalVictoryPoints} />
                  <IconStat icon={<ActionIcon decorative size="sm" type="Supplies" />} label="Supplies" value={player.supplyCount} />
                  <IconStat
                    icon={<span className="player-hidden-card"><DevelopmentCardArtwork decorative hidden /></span>}
                    label="Development Cards"
                    value={player.developmentCardCount}
                  />
                </span>
              </button>

              {isExpanded ? (
                <div className="player-detail-popover" id={detailsId}>
                  <div className="player-piece-details" aria-label={`${player.playerName} remaining pieces`}>
                    <PieceCount remaining={player.remainingTrails} total={player.totalTrails} type="Trail" />
                    <PieceCount remaining={player.remainingCamps} total={player.totalCamps} type="Camp" />
                    <PieceCount remaining={player.remainingStrongholds} total={player.totalStrongholds} type="Stronghold" />
                  </div>
                  <div className="player-detail-stats">
                    <IconStat icon={<Shield size={16} />} label="Knights played" value={player.playedKnightCount} />
                    <IconStat icon={<ActionIcon decorative size="sm" type="Build" />} label="Longest Trail length" value={player.longestTrailLength} />
                    <IconStat icon={<Anchor size={16} />} label="Accessible harbors" value={player.accessibleHarborSlotIds.length} />
                  </div>
                  <p>
                    Score <strong>{player.visibleVictoryPoints}</strong> visible
                    <span aria-hidden="true"> | </span>
                    <strong>{player.totalVictoryPoints}</strong> total
                  </p>
                </div>
              ) : null}
            </article>
          );
        })}
      </div>
    </aside>
  );
}
