namespace Karo.Api.Models;

public enum RoomStatus
{
    Waiting,
    InGame,
    PostGame
}

public enum PlayerConnectionStatus
{
    Connected,
    Reconnecting,
    Disconnected,
    TimedOut,
    Left,
    Forfeited
}

public sealed class Room
{
    public string RoomCode { get; init; } = "";
    public string HostPlayerId { get; set; } = "";
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PostGameAt { get; set; }
    public long RoomStateVersion { get; set; } = 1;
    public List<Player> Players { get; } = new();
}

public sealed class Player
{
    public string PlayerId { get; init; } = "";
    public string PlayerName { get; init; } = "";
    public string? ConnectionId { get; set; }
    public DateTimeOffset JoinedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DisconnectedAt { get; set; }
    public PlayerConnectionStatus ConnectionStatus { get; set; } = PlayerConnectionStatus.Connected;
    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
    public bool HasForfeited { get; set; }
    public string PlayerColor { get; init; } = "";
}

public sealed class PlayerSession
{
    public string PlayerId { get; init; } = "";
    public string RoomCode { get; init; } = "";
    public string ReconnectTokenHash { get; init; } = "";
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class LobbyException : Exception
{
    public string ErrorCode { get; }

    public string UserMessage => Message;

    public LobbyException(string userMessage, string? errorCode = null)
        : base(userMessage)
    {
        ErrorCode = errorCode ?? UserFacingErrorCodes.FromMessage(userMessage);
    }
}

public sealed record PlayerSessionResult(
    Room Room,
    Player Player,
    string ReconnectToken,
    string? ReplacedConnectionId = null);

public sealed record LifecycleDisconnectResult(
    string? RoomCode,
    string? PlayerId,
    Room? Room,
    bool RoomRemoved,
    bool WasCurrentConnection);

public sealed record RoomCleanupResult(
    Room? Room,
    string RoomCode,
    IReadOnlyList<string> TimedOutPlayerIds,
    bool RoomRemoved);
