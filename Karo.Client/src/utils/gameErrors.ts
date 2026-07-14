export interface FriendlyGameError {
  title: string;
  message: string;
  errorCode?: string;
  debugDetails?: string;
}

interface ServerValidationError {
  errorCode?: string;
  userMessage?: string;
}

const fallbackError: FriendlyGameError = {
  title: 'Action failed',
  message: 'Something went wrong. Please try again.'
};

const knownMessages: Array<{ code: string; message: string; title: string }> = [
  {
    code: 'DevelopmentCardAlreadyPlayedThisTurn',
    message: 'You can play only one Development Card per turn.',
    title: 'Card limit used'
  },
  {
    code: 'DevelopmentCardBoughtThisTurn',
    message: 'You cannot play a Development Card bought this turn.',
    title: 'Card bought this turn'
  },
  {
    code: 'DevelopmentCardBuyRequiresRoll',
    message: 'Roll the dice before buying a Development Card.',
    title: 'Roll required'
  },
  {
    code: 'TradeRequiresRoll',
    message: 'You must roll before trading.',
    title: 'Roll required'
  },
  {
    code: 'NotYourTurn',
    message: 'It is not your turn.',
    title: 'Not your turn'
  },
  {
    code: 'MatchPausedForReconnect',
    message: 'The match is paused while a player reconnects.',
    title: 'Match paused'
  },
  {
    code: 'WardenActionRequired',
    message: 'Resolve the Warden action first.',
    title: 'Warden waiting'
  },
  {
    code: 'NotEnoughSupplies',
    message: 'Not enough supplies.',
    title: 'Not enough supplies'
  },
  {
    code: 'NotEnoughSuppliesForTrade',
    message: 'You do not have enough supplies for this trade.',
    title: 'Not enough supplies'
  },
  {
    code: 'PlayerTradeNotEnoughOfferedSupplies',
    message: 'Not enough Supplies for this offer.',
    title: 'Not enough supplies'
  },
  {
    code: 'PlayerTradeTargetSuppliesChanged',
    message: 'The other player no longer has the requested Supplies.',
    title: 'Trade changed'
  },
  {
    code: 'PlayerTradeProposerSuppliesChanged',
    message: 'The proposing player no longer has the offered Supplies.',
    title: 'Trade changed'
  },
  {
    code: 'PlayerTradeUnavailableOffer',
    message: 'This trade offer is no longer available.',
    title: 'Offer unavailable'
  },
  {
    code: 'InvalidPlacement',
    message: 'Invalid placement.',
    title: 'Invalid placement'
  },
  {
    code: 'DevelopmentCardBuyBlockedDuringSetup',
    message: 'Development Cards cannot be bought during setup.',
    title: 'Setup phase'
  },
  {
    code: 'DevelopmentCardPlayBlockedDuringSetup',
    message: 'Development Cards cannot be used during setup.',
    title: 'Setup phase'
  },
  {
    code: 'DevelopmentCardActionPending',
    message: 'Resolve the current Development Card action first.',
    title: 'Card action pending'
  },
  {
    code: 'NoTrailPiecesRemaining',
    message: 'You have no Trail pieces remaining.',
    title: 'No Trails left'
  },
  {
    code: 'NoLegalTrailPlacement',
    message: 'No legal Trail placement is available.',
    title: 'No Trail placement'
  },
  {
    code: 'NoCampPiecesRemaining',
    message: 'You have no Camp pieces remaining.',
    title: 'No Camps left'
  },
  {
    code: 'NoStrongholdPiecesRemaining',
    message: 'You have no Stronghold pieces remaining.',
    title: 'No Strongholds left'
  }
];

const codeTitles: Record<string, string> = {
  CampSpacingRequired: 'Invalid camp spot',
  BuildNodeOccupied: 'Node occupied',
  TrailEdgeOccupied: 'Trail occupied',
  SetupTrailMustConnect: 'Invalid trail',
  DevelopmentDeckEmpty: 'Deck empty',
  DevelopmentCardActionPending: 'Card action pending',
  NoTrailPiecesRemaining: 'No Trails left',
  NoLegalTrailPlacement: 'No Trail placement',
  NoCampPiecesRemaining: 'No Camps left',
  NoStrongholdPiecesRemaining: 'No Strongholds left',
  TradeSameResource: 'Invalid trade',
  TradeBlockedDuringSetup: 'Setup phase',
  PlayerTradeOnlyCurrentCanOffer: 'Current player only',
  PlayerTradeSelfBlocked: 'Invalid trade',
  PlayerTradeRequiresBothSides: 'Invalid trade',
  PlayerTradeUnavailable: 'Trading unavailable',
  PlayerTradeOnlyTargetCanAccept: 'Wrong player',
  PlayerTradeOnlyTargetCanReject: 'Wrong player',
  PlayerTradeOnlyProposerCanCancel: 'Wrong player',
  WardenDiscardAmountMismatch: 'Discard amount',
  WardenMustMove: 'Move the Warden',
  InvalidWardenTile: 'Invalid Warden move',
  InvalidWardenVictim: 'Invalid victim',
  PlayerNameRequired: 'Name required',
  InvalidRoomCode: 'Invalid room',
  RoomNotFound: 'Room unavailable',
  SessionNotConnected: 'Session unavailable',
  SessionExpired: 'Session expired',
  SessionReplaced: 'Session replaced',
  PlayersNotReady: 'Players not ready',
  NotEnoughPlayers: 'More players needed',
  NotRoomHost: 'Host only',
  PlayerNotTimedOut: 'Player still reconnecting',
  ForfeitRequired: 'Forfeit required',
  MatchPausedForReconnect: 'Match paused',
  MatchNotFinished: 'Match in progress',
  RoomAlreadyInGame: 'Match already started',
  RoomFull: 'Room full',
  OnlyHostCanStart: 'Host only'
};

export function makeFriendlyGameError(
  title: string,
  message: string,
  errorCode?: string,
  debugDetails?: string
): FriendlyGameError {
  return {
    title,
    message,
    errorCode,
    debugDetails
  };
}

export function mapGameErrorCodeToMessage(errorCode: string, userMessage?: string): FriendlyGameError {
  const known = knownMessages.find((entry) => entry.code === errorCode);

  if (known) {
    return {
      title: known.title,
      message: userMessage ?? known.message,
      errorCode
    };
  }

  return {
    title: codeTitles[errorCode] ?? titleFromMessage(userMessage ?? 'Action failed'),
    message: userMessage ?? fallbackError.message,
    errorCode
  };
}

export function parseSignalRError(error: unknown): ServerValidationError | null {
  const rawMessage = getRawErrorMessage(error);

  if (!rawMessage) {
    return null;
  }

  const jsonPayload = extractJsonPayload(rawMessage);
  if (jsonPayload?.userMessage) {
    return jsonPayload;
  }

  const hubMessage = rawMessage.split('HubException:').pop()?.trim();
  if (hubMessage && hubMessage !== rawMessage) {
    const nestedPayload = extractJsonPayload(hubMessage);
    if (nestedPayload?.userMessage) {
      return nestedPayload;
    }

    const knownHubMessage = findKnownMessage(hubMessage);
    if (knownHubMessage) {
      return {
        errorCode: knownHubMessage.code,
        userMessage: knownHubMessage.message
      };
    }
  }

  const knownMessage = findKnownMessage(rawMessage);
  if (knownMessage) {
    return {
      errorCode: knownMessage.code,
      userMessage: knownMessage.message
    };
  }

  return null;
}

export function getFriendlyGameError(error: unknown): FriendlyGameError {
  const parsed = parseSignalRError(error);
  const debugDetails = getRawErrorMessage(error);

  if (parsed?.userMessage) {
    return {
      ...mapGameErrorCodeToMessage(parsed.errorCode ?? 'ValidationFailed', parsed.userMessage),
      debugDetails
    };
  }

  if (debugDetails && !containsTechnicalNoise(debugDetails)) {
    return {
      title: titleFromMessage(debugDetails),
      message: debugDetails,
      debugDetails
    };
  }

  return {
    ...fallbackError,
    debugDetails
  };
}

function getRawErrorMessage(error: unknown) {
  if (error instanceof Error) {
    return error.message;
  }

  if (typeof error === 'string') {
    return error;
  }

  return '';
}

function extractJsonPayload(message: string): ServerValidationError | null {
  const match = message.match(/\{[^{}]*"userMessage"[^{}]*\}/);

  if (!match) {
    return null;
  }

  try {
    const parsed = JSON.parse(match[0]) as ServerValidationError;
    return typeof parsed.userMessage === 'string' ? parsed : null;
  } catch {
    return null;
  }
}

function findKnownMessage(message: string) {
  return knownMessages.find((entry) => message.includes(entry.message));
}

function containsTechnicalNoise(message: string) {
  return /unexpected error occurred invoking|HubException|Play[A-Z]|StartRoadBuilding|MoveWarden|DiscardForWarden|StealFromWardenVictim|at\s+\S+\(|stack trace/i
    .test(message);
}

function titleFromMessage(message: string) {
  const firstSentence = message.split('.')[0]?.trim() || 'Action failed';
  return firstSentence.length > 32 ? 'Action failed' : firstSentence;
}
