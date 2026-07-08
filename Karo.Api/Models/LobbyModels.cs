namespace Karo.Api.Models;

public enum RoomStatus
{
    Waiting,
    InGame
}

public sealed class Room
{
    public string RoomCode { get; init; } = "";
    public string HostConnectionId { get; set; } = "";
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<Player> Players { get; } = new();
}

public sealed class Player
{
    public string PlayerId { get; init; } = "";
    public string PlayerName { get; init; } = "";
    public string ConnectionId { get; init; } = "";
    public DateTimeOffset JoinedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsHost { get; set; }
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
