namespace Karo.Api.Models;

public enum ResourceType
{
    Wood,
    Clay,
    Wool,
    Grain,
    Stone
}

public enum TileResourceType
{
    Wood,
    Clay,
    Wool,
    Grain,
    Stone,
    None
}

public enum GameStatus
{
    InProgress,
    Finished
}

public enum GamePhase
{
    Setup,
    NormalTurn,
    Finished
}

public enum SetupRound
{
    FirstPlacement,
    SecondPlacement
}

public enum SetupStep
{
    PlaceCamp,
    PlaceTrail
}

public enum SetupDirection
{
    Forward,
    Reverse
}

public enum DevelopmentCardType
{
    Knight,
    RoadBuilding,
    YearOfPlenty,
    Monopoly,
    VictoryPoint
}

public enum ActiveDevelopmentCardType
{
    RoadBuilding
}

public enum WardenAction
{
    None,
    Discarding,
    MoveWarden,
    ChooseVictim
}

public enum BoardStructureType
{
    Camp,
    Stronghold
}

public enum PortType
{
    Generic3To1,
    Specific2To1
}

public enum BankTradeRateSource
{
    DefaultBank,
    GenericPort,
    SpecificPort
}

public enum HarborType
{
    Generic,
    Wood,
    Clay,
    Wool,
    Grain,
    Stone
}

public sealed class GameState
{
    public string RoomCode { get; init; } = "";
    public BoardState Board { get; init; } = new();
    public List<PlayerGameState> Players { get; } = new();
    public List<DevelopmentCard> DevelopmentDeck { get; } = new();
    public List<GameLogEntry> Log { get; } = new();
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public GameStatus Status { get; set; } = GameStatus.InProgress;
    public GamePhase Phase { get; set; } = GamePhase.Setup;
    public List<string> PlayerOrder { get; } = new();
    public SetupRound? SetupRound { get; set; } = Karo.Api.Models.SetupRound.FirstPlacement;
    public SetupStep? SetupStep { get; set; } = Karo.Api.Models.SetupStep.PlaceCamp;
    public SetupDirection? SetupDirection { get; set; } = Karo.Api.Models.SetupDirection.Forward;
    public int SetupPlayerIndex { get; set; }
    public string? LastSetupCampVertexId { get; set; }
    public int WinningVictoryPoints { get; init; } = 10;
    public int CurrentPlayerIndex { get; set; }
    public int TurnNumber { get; set; } = 1;
    public int? LastDiceRoll { get; set; }
    public bool HasRolledThisTurn { get; set; }
    public string RobberTileId { get; set; } = "";
    public string WardenTileId
    {
        get => RobberTileId;
        set => RobberTileId = value;
    }

    public WardenAction PendingWardenAction { get; set; } = WardenAction.None;
    public string? CurrentWardenPlayerId { get; set; }
    public List<WardenDiscardRequirement> PendingWardenDiscards { get; } = new();
    public List<string> WardenVictimOptions { get; } = new();
    public string? LargestArmyPlayerId { get; set; }
    public int LargestArmyKnightCount { get; set; }
    public int? LargestArmyAwardedAtTurn { get; set; }
    public string? WinnerPlayerId { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public PlayerGameState CurrentPlayer => Players[CurrentPlayerIndex];
    public string? CurrentSetupPlayerId => Phase == GamePhase.Setup
        ? PlayerOrder.ElementAtOrDefault(SetupPlayerIndex)
        : null;
}

public sealed class PlayerGameState
{
    public string PlayerId { get; init; } = "";
    public string PlayerName { get; init; } = "";
    public bool IsHost { get; init; }
    public Dictionary<ResourceType, int> Supplies { get; } = ResourceTypes.All.ToDictionary(resource => resource, _ => 0);
    public List<PlayerDevelopmentCard> DevelopmentCards { get; } = new();
    public int TrailsBuilt { get; set; }
    public int CampsBuilt { get; set; }
    public int StrongholdsBuilt { get; set; }
    public int PlayedKnightCount { get; set; }
    public bool HasPlayedDevelopmentCardThisTurn { get; set; }
    public ActiveDevelopmentCardEffect? ActiveDevelopmentCardEffect { get; set; }
    public int? DebugVictoryPointOverride { get; set; }
}

public sealed class BoardState
{
    public List<HexTile> Tiles { get; } = new();
    public List<BoardVertex> Vertices { get; } = new();
    public List<BoardEdge> Edges { get; } = new();
    public List<HarborSlot> HarborSlots { get; } = new();
    public List<Port> Ports { get; } = new();
}

public sealed class HexTile
{
    public string TileId { get; init; } = "";
    public int Q { get; init; }
    public int R { get; init; }
    public TileResourceType ResourceType { get; init; }
    public int? NumberToken { get; init; }
    public bool IsBlocked { get; set; }
}

public sealed class BoardVertex
{
    public string VertexId { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public bool IsCoastal { get; set; }
    public List<string> AdjacentTileIds { get; } = new();
    public string? OwnerPlayerId { get; set; }
    public BoardStructureType? StructureType { get; set; }
}

public sealed class BoardEdge
{
    public string EdgeId { get; init; } = "";
    public string StartVertexId { get; init; } = "";
    public string EndVertexId { get; init; } = "";
    public string? OwnerPlayerId { get; set; }
}

public sealed class HarborSlot
{
    public string HarborSlotId { get; init; } = "";
    public List<string> AdjacentVertexIds { get; } = new();
    public string AdjacentEdgeId { get; init; } = "";
    public int TileQ { get; init; }
    public int TileR { get; init; }
    public int EdgeIndex { get; init; }
    public double RenderX { get; init; }
    public double RenderY { get; init; }
    public double OrientationDegrees { get; init; }
    public HarborType? HarborType { get; init; }
    public int? TradeRate { get; init; }
}

public sealed class Port
{
    public string Id { get; init; } = "";
    public PortType Type { get; init; }
    public ResourceType? ResourceType { get; init; }
    public int TileQ { get; init; }
    public int TileR { get; init; }
    public int EdgeIndex { get; init; }
    public List<string> AdjacentVertexIds { get; } = new();
    public string DisplayLabel => Type == PortType.Generic3To1
        ? "3:1"
        : $"2:1 {ResourceType}";
}

public sealed record BankTradeRate(
    ResourceType Resource,
    int Rate,
    BankTradeRateSource Source,
    string? PortId);

public sealed class DevelopmentCard
{
    public string CardId { get; init; } = "";
    public DevelopmentCardType Type { get; init; }
}

public sealed class PlayerDevelopmentCard
{
    public string CardId { get; init; } = "";
    public DevelopmentCardType Type { get; init; }
    public int PurchasedTurn { get; init; }
    public bool IsPlayed { get; set; }
}

public sealed class ActiveDevelopmentCardEffect
{
    public ActiveDevelopmentCardType Type { get; init; }
    public string CardId { get; init; } = "";
    public int FreeTrailsPlaced { get; set; }
    public int MaxFreeTrails { get; init; } = 2;
}

public sealed class WardenDiscardRequirement
{
    public string PlayerId { get; init; } = "";
    public int RequiredAmount { get; init; }
}

public sealed record GameLogEntry(
    int Sequence,
    DateTimeOffset CreatedAt,
    string Message,
    string? PlayerId = null);

public sealed class GameRuleException : Exception
{
    public string ErrorCode { get; }

    public string UserMessage => Message;

    public GameRuleException(string userMessage, string? errorCode = null)
        : base(userMessage)
    {
        ErrorCode = errorCode ?? UserFacingErrorCodes.FromMessage(userMessage);
    }
}

public static class ResourceTypes
{
    public static readonly IReadOnlyList<ResourceType> All =
    [
        ResourceType.Wood,
        ResourceType.Clay,
        ResourceType.Wool,
        ResourceType.Grain,
        ResourceType.Stone
    ];
}
