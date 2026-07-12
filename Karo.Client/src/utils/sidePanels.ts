import type { GamePhase, GameState } from '../types/game';

export interface PlayerStatusInput {
  isActive: boolean;
  isSelf: boolean;
  isWinner: boolean;
  phase: GamePhase;
}

export interface TurnPanelContextInput {
  currentTurnPlayerName: string;
  game: GameState;
  isFinished: boolean;
  isMyTurn: boolean;
  setupPiece: string;
  setupRound: string;
  wardenFlowActive: boolean;
  winnerName: string;
}

export function getPlayerStatusLabel({
  isActive,
  isSelf,
  isWinner,
  phase
}: PlayerStatusInput) {
  if (isWinner) {
    return 'Winner';
  }

  if (isActive && phase === 'Setup') {
    return isSelf ? 'Your setup' : 'Setting up now';
  }

  if (isActive) {
    return isSelf ? 'Your turn' : 'Current turn';
  }

  return isSelf ? 'You' : 'Waiting';
}

export function getTurnPanelContext({
  currentTurnPlayerName,
  game,
  isFinished,
  isMyTurn,
  setupPiece,
  setupRound,
  wardenFlowActive,
  winnerName
}: TurnPanelContextInput) {
  if (isFinished) {
    return {
      visual: 'finished' as const,
      phaseLabel: 'Match finished',
      primaryInstruction: 'Match finished',
      helperText: `${winnerName} reached the score target. Review the final standings in the log.`,
      secondaryText: '',
      details: [
        { label: 'Winner', value: winnerName },
        { label: 'Target', value: `${game.winningVictoryPoints} VP` }
      ]
    };
  }

  if (game.phase === 'Setup') {
    return {
      visual: setupPiece === 'Trail' ? 'setup-trail' as const : 'setup-camp' as const,
      phaseLabel: `Setup - ${setupRound}`,
      primaryInstruction: `Place a ${setupPiece}`,
      helperText: setupPiece === 'Trail'
        ? 'Connect it to the Camp you just placed.'
        : 'Choose a highlighted open intersection on the island.',
      secondaryText: '',
      details: []
    };
  }

  if (game.activeDevelopmentCardEffect) {
    return {
      visual: 'road-building' as const,
      phaseLabel: 'Road Building',
      primaryInstruction: 'Place free Trails',
      helperText: 'Use highlighted board edges or cancel the remaining Road Building effect.',
      secondaryText: '',
      details: [{ label: 'Progress', value: `${game.activeDevelopmentCardEffect.freeTrailsPlaced}/${game.activeDevelopmentCardEffect.maxFreeTrails}` }]
    };
  }

  if (wardenFlowActive) {
    return {
      visual: 'warden' as const,
      phaseLabel: 'Warden',
      primaryInstruction: game.pendingWardenAction === 'MoveWarden'
        ? 'Move the Warden'
        : game.pendingWardenAction === 'ChooseVictim'
          ? 'Choose a player'
          : 'Discard Supplies',
      helperText: 'Finish the required Warden step before normal actions continue.',
      secondaryText: '',
      details: []
    };
  }

  if (!game.hasRolledThisTurn) {
    return {
      visual: 'before-roll' as const,
      phaseLabel: `Turn ${game.turnNumber}`,
      primaryInstruction: 'Roll the dice',
      helperText: isMyTurn
        ? 'Roll to unlock building, trading, card purchases, and ending your turn; eligible Development Cards may still be played first.'
        : `Waiting for ${currentTurnPlayerName} to roll.`,
      secondaryText: '',
      details: []
    };
  }

  return {
    visual: 'after-roll' as const,
    phaseLabel: `Turn ${game.turnNumber}`,
    primaryInstruction: game.lastDiceRoll ? `Rolled ${game.lastDiceRoll}` : 'Actions unlocked',
    helperText: isMyTurn
      ? 'Build, trade, play a card, or end your turn.'
      : `Waiting for ${currentTurnPlayerName} to finish their turn.`,
    secondaryText: '',
    details: []
  };
}
