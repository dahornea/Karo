using Karo.Api.Models;
using Microsoft.Extensions.Hosting;

namespace Karo.Api.Services;

public sealed class DebugGameService
{
    private const int TestingResourceAmount = 8;

    private readonly GameService _gameService;
    private readonly IHostEnvironment _environment;

    public DebugGameService(GameService gameService, IHostEnvironment environment)
    {
        _gameService = gameService;
        _environment = environment;
    }

    public GameState AddResource(Room room, Player actor, string playerId, ResourceType resource, int amount)
    {
        EnsureDebugAllowed(room, actor);

        if (amount <= 0)
        {
            throw new GameRuleException("Debug resource amount must be positive.");
        }

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            player.Supplies[resource] += amount;
            GameService.AddLog(game, $"[DEBUG] Added {amount} {resource} to {player.PlayerName}.", actor.PlayerId);
            return game;
        });
    }

    public GameState SetResources(Room room, Player actor, string playerId, IReadOnlyDictionary<ResourceType, int> resources)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            foreach (var resource in ResourceTypes.All)
            {
                player.Supplies[resource] = Math.Max(0, resources.TryGetValue(resource, out var amount) ? amount : 0);
            }

            GameService.AddLog(game, $"[DEBUG] Set resources for {player.PlayerName}.", actor.PlayerId);
            return game;
        });
    }

    public GameState SetTestingResources(Room room, Player actor, string playerId)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            foreach (var resource in ResourceTypes.All)
            {
                player.Supplies[resource] = TestingResourceAmount;
            }

            GameService.AddLog(game, $"[DEBUG] Set testing resources for {player.PlayerName}.", actor.PlayerId);
            return game;
        });
    }

    public GameState ClearResources(Room room, Player actor, string playerId)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            foreach (var resource in ResourceTypes.All)
            {
                player.Supplies[resource] = 0;
            }

            GameService.AddLog(game, $"[DEBUG] Cleared resources for {player.PlayerName}.", actor.PlayerId);
            return game;
        });
    }

    public GameState SetCurrentPlayer(Room room, Player actor, string playerId)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var playerIndex = game.Players.FindIndex(player => string.Equals(player.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
            if (playerIndex < 0)
            {
                throw new GameRuleException("Choose a valid player.");
            }

            game.CurrentPlayerIndex = playerIndex;
            game.LastDiceRoll = null;
            game.HasRolledThisTurn = false;
            GameService.AddLog(game, $"[DEBUG] Forced turn to {game.CurrentPlayer.PlayerName}.", actor.PlayerId);
            return game;
        });
    }

    public GameState ForceDiceRoll(Room room, Player actor, int diceValue)
    {
        EnsureDebugAllowed(room, actor);

        if (diceValue < 2 || diceValue > 12)
        {
            throw new GameRuleException("Debug dice value must be between 2 and 12.");
        }

        return _gameService.ForceDiceRollForDebug(room.RoomCode, actor.PlayerId, diceValue);
    }

    public GameState ResetRollState(Room room, Player actor)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            game.LastDiceRoll = null;
            game.HasRolledThisTurn = false;
            GameService.AddLog(game, "[DEBUG] Reset roll state.", actor.PlayerId);
            return game;
        });
    }

    public GameState MoveWarden(Room room, Player actor, string targetTileId)
    {
        EnsureDebugAllowed(room, actor);
        return _gameService.MoveWardenForDebug(room.RoomCode, actor.PlayerId, targetTileId);
    }

    public GameState ClearWardenState(Room room, Player actor)
    {
        EnsureDebugAllowed(room, actor);
        return _gameService.ClearWardenStateForDebug(room.RoomCode, actor.PlayerId);
    }

    public GameState SkipSetup(Room room, Player actor)
    {
        EnsureDebugAllowed(room, actor);
        return _gameService.SkipSetupForDebug(room.RoomCode, actor.PlayerId);
    }

    public GameState ForceSetupStep(Room room, Player actor, string playerId, SetupStep setupStep)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            var playerOrderIndex = game.PlayerOrder.FindIndex(candidate =>
                string.Equals(candidate, player.PlayerId, StringComparison.OrdinalIgnoreCase));

            if (playerOrderIndex < 0)
            {
                game.PlayerOrder.Add(player.PlayerId);
                playerOrderIndex = game.PlayerOrder.Count - 1;
            }

            game.Phase = GamePhase.Setup;
            game.SetupRound ??= SetupRound.FirstPlacement;
            game.SetupDirection ??= game.SetupRound == SetupRound.SecondPlacement
                ? SetupDirection.Reverse
                : SetupDirection.Forward;
            game.SetupStep = setupStep;
            game.SetupPlayerIndex = playerOrderIndex;
            game.CurrentPlayerIndex = game.Players.FindIndex(candidate =>
                string.Equals(candidate.PlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase));
            game.LastDiceRoll = null;
            game.HasRolledThisTurn = false;

            if (setupStep == SetupStep.PlaceTrail)
            {
                game.LastSetupCampVertexId = game.Board.Vertices
                    .FirstOrDefault(vertex => string.Equals(vertex.OwnerPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase)
                        && vertex.StructureType == BoardStructureType.Camp)
                    ?.VertexId;

                if (game.LastSetupCampVertexId is null)
                {
                    throw new GameRuleException("Give the target player a setup Camp before forcing the Trail step.");
                }
            }
            else
            {
                game.LastSetupCampVertexId = null;
            }

            GameService.AddLog(game, $"[DEBUG] Forced setup to {setupStep} for {player.PlayerName}.", actor.PlayerId);
            return game;
        });
    }

    public GameState SetVictoryPoints(Room room, Player actor, string playerId, int points)
    {
        EnsureDebugAllowed(room, actor);

        if (points < 0)
        {
            throw new GameRuleException("Debug victory points cannot be negative.");
        }

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            player.DebugVictoryPointOverride = points;
            GameService.AddLog(game, $"[DEBUG] Set {player.PlayerName} to {points} victory points.", actor.PlayerId);
            GameService.CheckWinner(game, player);
            return game;
        });
    }

    public GameState TriggerWinCheck(Room room, Player actor, string playerId)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            GameService.AddLog(game, $"[DEBUG] Triggered win check for {player.PlayerName}.", actor.PlayerId);
            GameService.CheckWinner(game, player);
            return game;
        });
    }

    public GameState GiveDevelopmentCard(Room room, Player actor, string playerId, DevelopmentCardType? cardType)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            var type = cardType ?? GetRandomDevelopmentCardType();
            player.DevelopmentCards.Add(new PlayerDevelopmentCard
            {
                CardId = $"debug-{Guid.NewGuid():N}",
                Type = type,
                PurchasedTurn = Math.Max(0, game.TurnNumber - 1)
            });

            GameService.AddLog(game, $"[DEBUG] Gave {player.PlayerName} a {type} card.", actor.PlayerId);
            GameService.CheckWinner(game, player);
            return game;
        });
    }

    public GameState ClearDevelopmentCards(Room room, Player actor, string playerId)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            player.DevelopmentCards.Clear();
            player.HasPlayedDevelopmentCardThisTurn = false;
            player.ActiveDevelopmentCardEffect = null;
            GameService.AddLog(game, $"[DEBUG] Cleared development cards for {player.PlayerName}.", actor.PlayerId);
            return game;
        });
    }

    public GameState ResetDevelopmentCardPlayLimit(Room room, Player actor, string playerId)
    {
        EnsureDebugAllowed(room, actor);

        return _gameService.UpdateGame(room.RoomCode, game =>
        {
            var player = GetPlayer(game, playerId);
            player.HasPlayedDevelopmentCardThisTurn = false;
            player.ActiveDevelopmentCardEffect = null;
            GameService.AddLog(game, $"[DEBUG] Reset development card play limit for {player.PlayerName}.", actor.PlayerId);
            return game;
        });
    }

    public IReadOnlyDictionary<string, int> GetDevelopmentDeckComposition(Room room, Player actor)
    {
        EnsureDebugAllowed(room, actor);

        IReadOnlyDictionary<string, int> composition = new Dictionary<string, int>();

        _gameService.UpdateGame(room.RoomCode, game =>
        {
            composition = Enum.GetValues<DevelopmentCardType>()
                .ToDictionary(
                    type => type.ToString(),
                    type => game.DevelopmentDeck.Count(card => card.Type == type));
            return game;
        });

        return composition;
    }

    public GameState RestartMatch(Room room, Player actor)
    {
        EnsureDebugAllowed(room, actor);
        var game = _gameService.RestartGame(room);
        GameService.AddLog(game, "[DEBUG] Restarted match with the same players.", actor.PlayerId);
        return game;
    }

    private void EnsureDebugAllowed(Room room, Player actor)
    {
        if (!_environment.IsDevelopment())
        {
            throw new GameRuleException("Debug actions are only available in Development.");
        }

        if (!room.Players.Any(player => string.Equals(player.PlayerId, actor.PlayerId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new GameRuleException("You are not in this room.");
        }

        if (!actor.IsHost)
        {
            throw new GameRuleException("Only the host can use debug actions.");
        }
    }

    private static PlayerGameState GetPlayer(GameState game, string playerId)
    {
        return game.Players.FirstOrDefault(player => string.Equals(player.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new GameRuleException("Choose a valid player.");
    }

    private static DevelopmentCardType GetRandomDevelopmentCardType()
    {
        var types = Enum.GetValues<DevelopmentCardType>();
        return types[Random.Shared.Next(types.Length)];
    }
}
