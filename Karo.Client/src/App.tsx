import { useEffect } from 'react';
import { Wifi, WifiOff, X } from 'lucide-react';
import { GamePage } from './components/GamePage';
import { LandingPage } from './components/LandingPage';
import { LobbyPage } from './components/LobbyPage';
import { useDebugMode } from './hooks/useDebugMode';
import { useLobbyConnection } from './hooks/useLobbyConnection';

export function App() {
  const lobby = useLobbyConnection();
  const debug = useDebugMode();
  const isConnected = lobby.connectionPhase === 'connected';
  const showGame = !!lobby.gameStarted;

  useEffect(() => {
    if (!lobby.error) {
      return undefined;
    }

    const timeoutId = window.setTimeout(lobby.clearError, 6500);
    return () => window.clearTimeout(timeoutId);
  }, [lobby.error, lobby.clearError]);

  return (
    <main className="app-shell min-h-screen">
      <div className="connection-pill" data-state={lobby.connectionPhase}>
        {isConnected ? <Wifi size={16} /> : <WifiOff size={16} />}
        <span>{lobby.connectionPhase}</span>
      </div>

      {lobby.error ? (
        <section className="toast" role="status" aria-live="polite">
          <div className="toast-copy">
            <strong>{lobby.error.title}</strong>
            <span>{lobby.error.message}</span>
            {debug.isDebugEnabled && lobby.error.debugDetails ? (
              <details className="toast-debug">
                <summary>Debug details</summary>
                <code>{lobby.error.debugDetails}</code>
              </details>
            ) : null}
          </div>
          <button aria-label="Dismiss message" className="toast-dismiss" type="button" onClick={lobby.clearError}>
            <X size={15} />
          </button>
        </section>
      ) : null}

      {!lobby.room ? (
        <LandingPage
          connectionPhase={lobby.connectionPhase}
          pendingAction={lobby.pendingAction}
          onCreateRoom={lobby.createRoom}
          onJoinRoom={lobby.joinRoom}
        />
      ) : showGame ? (
        <GamePage
          connectionPhase={lobby.connectionPhase}
          game={lobby.gameStarted!}
          playerId={lobby.playerId}
          pendingAction={lobby.pendingAction}
          onBuyDevelopmentCard={lobby.buyDevelopmentCard}
          onTradeWithBank={lobby.tradeWithBank}
          onEndTurn={lobby.endTurn}
          onPlaceSetupCamp={lobby.placeSetupCamp}
          onPlaceSetupTrail={lobby.placeSetupTrail}
          onRollDice={lobby.rollDice}
          onDiscardForWarden={lobby.discardForWarden}
          onMoveWarden={lobby.moveWarden}
          onStealFromWardenVictim={lobby.stealFromWardenVictim}
          onPlayYearOfPlenty={lobby.playYearOfPlenty}
          onPlayMonopoly={lobby.playMonopoly}
          onPlayKnight={lobby.playKnight}
          onStartRoadBuilding={lobby.startRoadBuilding}
          onCancelActiveDevelopmentCard={lobby.cancelActiveDevelopmentCard}
          isDebugEnabled={debug.isDebugEnabled}
          onDebugModeChange={debug.setIsDebugEnabled}
          onDebugAddResource={lobby.debugAddResource}
          onDebugSetTestingResources={lobby.debugSetTestingResources}
          onDebugClearResources={lobby.debugClearResources}
          onDebugSetCurrentPlayer={lobby.debugSetCurrentPlayer}
          onDebugForceDiceRoll={lobby.debugForceDiceRoll}
          onDebugResetRollState={lobby.debugResetRollState}
          onDebugMoveWarden={lobby.debugMoveWarden}
          onDebugClearWardenState={lobby.debugClearWardenState}
          onDebugSkipSetup={lobby.debugSkipSetup}
          onDebugForceSetupStep={lobby.debugForceSetupStep}
          onDebugSetVictoryPoints={lobby.debugSetVictoryPoints}
          onDebugTriggerWinCheck={lobby.debugTriggerWinCheck}
          onDebugGiveDevelopmentCard={lobby.debugGiveDevelopmentCard}
          onDebugClearDevelopmentCards={lobby.debugClearDevelopmentCards}
          onDebugResetDevelopmentCardPlayLimit={lobby.debugResetDevelopmentCardPlayLimit}
          onDebugGetDevelopmentDeckComposition={lobby.debugGetDevelopmentDeckComposition}
          onDebugRestartMatch={lobby.debugRestartMatch}
        />
      ) : (
        <LobbyPage
          playerId={lobby.playerId}
          room={lobby.room}
          pendingAction={lobby.pendingAction}
          onStartGame={lobby.startGame}
        />
      )}
    </main>
  );
}
