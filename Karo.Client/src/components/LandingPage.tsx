import { ArrowRight, ArrowRightLeft, LockKeyhole, Plus, RadioTower, RotateCcw, Shapes } from 'lucide-react';
import { FormEvent, useState } from 'react';

interface LandingPageProps {
  connectionPhase: string;
  pendingAction: string | null;
  onCreateRoom: (playerName: string) => Promise<void>;
  onJoinRoom: (roomCode: string, playerName: string) => Promise<void>;
}

export function LandingPage({
  connectionPhase,
  pendingAction,
  onCreateRoom,
  onJoinRoom
}: LandingPageProps) {
  const [playerName, setPlayerName] = useState('');
  const [roomCode, setRoomCode] = useState('');
  const canSubmit = connectionPhase === 'connected' && !pendingAction;
  const trimmedName = playerName.trim();
  const normalizedRoomCode = roomCode.trim().toUpperCase();

  const submitCreate = (event: FormEvent) => {
    event.preventDefault();
    void onCreateRoom(trimmedName).catch(() => undefined);
  };

  const submitJoin = (event: FormEvent) => {
    event.preventDefault();
    void onJoinRoom(normalizedRoomCode, trimmedName).catch(() => undefined);
  };

  return (
    <section className="landing-screen">
      <div className="landing-shell">
        <div className="landing-copy">
          <div className="landing-brand-row">
            <div className="brand-mark">
              <Shapes size={30} />
            </div>
            <div>
              <p className="eyebrow">Karo</p>
              <span>Online tabletop strategy</span>
            </div>
          </div>

          <div className="landing-hero-text">
            <h1>Build, trade, and outsmart your friends.</h1>
            <p>
              Host a private Karo table, invite your friends, and compete across a shifting hex island.
            </p>
          </div>

          <FeatureChips />
          <FeatureCards />
        </div>

        <div className="room-action-panel" aria-label="Karo room actions">
          <div className="room-panel-heading">
            <p className="eyebrow">Take a seat</p>
            <h2>Start or join a private table</h2>
            <p>Use one player name for both hosting and joining.</p>
          </div>

          <label className="landing-field" htmlFor="player-name">
            Player name
            <input
              autoComplete="nickname"
              id="player-name"
              maxLength={18}
              placeholder="Mira"
              value={playerName}
              onChange={(event) => setPlayerName(event.target.value)}
            />
          </label>

          <form className="room-action-card create-room-card" onSubmit={submitCreate}>
            <div>
              <p className="eyebrow">Host Table</p>
              <h3>Create a room</h3>
              <p>Open a fresh Karo lobby and share the room code with friends.</p>
            </div>

            <button className="primary-button" disabled={!canSubmit || !trimmedName} type="submit">
              <Plus size={18} />
              <span>{pendingAction === 'Creating room' ? 'Creating...' : 'Create Room'}</span>
            </button>
          </form>

          <form className="room-action-card join-room-card" onSubmit={submitJoin}>
            <div>
              <p className="eyebrow">Join Match</p>
              <h3>Enter a room</h3>
              <p>Paste a six-character table code to take your seat.</p>
            </div>

            <label className="landing-field" htmlFor="room-code">
              Room code
              <input
                autoComplete="off"
                id="room-code"
                inputMode="text"
                maxLength={6}
                placeholder="KARO7Q"
                value={roomCode}
                onChange={(event) => setRoomCode(event.target.value.toUpperCase())}
              />
            </label>

            <button className="secondary-button" disabled={!canSubmit || !trimmedName || normalizedRoomCode.length < 6} type="submit">
              <ArrowRight size={18} />
              <span>{pendingAction === 'Joining room' ? 'Joining...' : 'Join Room'}</span>
            </button>
          </form>

          <div className="connection-note" data-state={connectionPhase}>
            <span />
            {connectionPhase === 'connected' ? 'Karo server connected' : `Server ${connectionPhase}`}
          </div>
        </div>
      </div>
    </section>
  );
}

function FeatureChips() {
  return (
    <div className="feature-strip landing-feature-chips" aria-label="Karo match pillars">
      <span>
        <LockKeyhole size={15} />
        Private rooms
      </span>
      <span>
        <RadioTower size={15} />
        Live lobby
      </span>
      <span>
        <RotateCcw size={15} />
        Real-time turns
      </span>
      <span>
        <ArrowRightLeft size={15} />
        Strategic trading
      </span>
    </div>
  );
}

function FeatureCards() {
  const cards = [
    {
      icon: LockKeyhole,
      title: 'Private Rooms',
      description: 'Create a table code and keep the match invite-only.'
    },
    {
      icon: RadioTower,
      title: 'Real-time Lobby',
      description: 'Players join, leave, and ready up together live.'
    },
    {
      icon: ArrowRightLeft,
      title: 'Strategic Turns',
      description: 'Build routes, manage supplies, and time your trades.'
    }
  ];

  return (
    <div className="landing-feature-card-grid" aria-label="Karo highlights">
      {cards.map((card) => {
        const Icon = card.icon;

        return (
          <article className="landing-feature-card" key={card.title}>
            <span className="landing-feature-icon">
              <Icon size={18} />
            </span>
            <div>
              <h2>{card.title}</h2>
              <p>{card.description}</p>
            </div>
          </article>
        );
      })}
    </div>
  );
}
