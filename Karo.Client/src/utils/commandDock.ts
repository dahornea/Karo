import type { GameState, PlayerGameState } from '../types/game';
import { getDevelopmentCardAvailability, getTradeAvailability } from './actionAvailability';

export type CommandDockActionId = 'trade' | 'cards' | 'log';

export interface CommandDockActionState {
  id: CommandDockActionId;
  disabledReason: string | null;
}

export function getVisibleCommandDockActions(
  game: GameState,
  me: PlayerGameState,
  playerId: string | null,
  _actionsUnlocked: boolean
): CommandDockActionState[] {
  const mandatoryActionActive = game.pendingWardenAction !== 'None' || !!game.activeDevelopmentCardEffect;

  if (game.status === 'Finished' || mandatoryActionActive || game.phase === 'Setup') {
    return [{ id: 'log', disabledReason: null }];
  }

  if (game.phase !== 'NormalTurn') {
    return [{ id: 'log', disabledReason: null }];
  }

  const tradeAvailability = getTradeAvailability(game, me, playerId);
  const cardAvailability = getDevelopmentCardAvailability(game, me, playerId);

  if (!game.hasRolledThisTurn) {
    const actions: CommandDockActionState[] = [];

    if (cardAvailability.canOpenCards) {
      const hasPlayableCard = cardAvailability.playableCards.some((card) => card.canPlay);
      actions.push({
        id: 'cards',
        disabledReason: hasPlayableCard || me.developmentCardCount > 0 ? null : 'No Development Cards to view.'
      });
    }

    actions.push({ id: 'log', disabledReason: null });
    return actions;
  }

  return [
    { id: 'trade', disabledReason: tradeAvailability.disabledReason },
    { id: 'cards', disabledReason: cardAvailability.canOpenCards ? null : 'No Development Card actions are available.' },
    { id: 'log', disabledReason: null }
  ];
}

export function hasPlayablePreRollDevelopmentCard(me: PlayerGameState) {
  return me.developmentCards.some((card) => (
    card.type !== 'VictoryPoint'
    && card.status === 'Playable'
    && !card.isPlayed
  ));
}
