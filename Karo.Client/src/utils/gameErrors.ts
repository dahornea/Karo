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
    message: 'You already played a Development Card this turn.',
    title: 'Card already played'
  },
  {
    code: 'DevelopmentCardBoughtThisTurn',
    message: 'You cannot play a Development Card bought this turn.',
    title: 'Card bought this turn'
  },
  {
    code: 'DevelopmentCardBuyRequiresRoll',
    message: 'You must roll before buying a Development Card.',
    title: 'Roll required'
  },
  {
    code: 'DevelopmentCardPlayRequiresRoll',
    message: 'You must roll before playing a Development Card.',
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
    code: 'InvalidPlacement',
    message: 'Invalid placement.',
    title: 'Invalid placement'
  },
  {
    code: 'DevelopmentCardBuyBlockedDuringSetup',
    message: 'You cannot buy Development Cards during setup.',
    title: 'Setup phase'
  },
  {
    code: 'DevelopmentCardPlayBlockedDuringSetup',
    message: 'You cannot play Development Cards during setup.',
    title: 'Setup phase'
  }
];

const codeTitles: Record<string, string> = {
  CampSpacingRequired: 'Invalid camp spot',
  BuildNodeOccupied: 'Node occupied',
  TrailEdgeOccupied: 'Trail occupied',
  SetupTrailMustConnect: 'Invalid trail',
  DevelopmentDeckEmpty: 'Deck empty',
  TradeSameResource: 'Invalid trade',
  TradeBlockedDuringSetup: 'Setup phase',
  WardenDiscardAmountMismatch: 'Discard amount',
  WardenMustMove: 'Move the Warden',
  InvalidWardenTile: 'Invalid Warden move',
  InvalidWardenVictim: 'Invalid victim',
  PlayerNameRequired: 'Name required',
  InvalidRoomCode: 'Invalid room',
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
