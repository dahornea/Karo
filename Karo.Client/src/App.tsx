import { Wifi, WifiOff } from 'lucide-react';
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

  return (
    <main className="app-shell min-h-screen">
      <div className="connection-pill" data-state={lobby.connectionPhase}>
        {isConnected ? <Wifi size={16} /> : <WifiOff size={16} />}
        <span>{lobby.connectionPhase}</span>
      </div>

      {lobby.error ? (
        <button className="toast" type="button" onClick={lobby.clearError}>
          {lobby.error}
        </button>
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
