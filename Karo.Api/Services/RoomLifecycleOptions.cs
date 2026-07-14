namespace Karo.Api.Services;

public sealed class RoomLifecycleOptions
{
    public const string SectionName = "Karo:Lifecycle";

    public TimeSpan ReconnectGracePeriod { get; init; } = TimeSpan.FromSeconds(120);
    public TimeSpan FullyDisconnectedRoomTtl { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan PostGameRoomTtl { get; init; } = TimeSpan.FromMinutes(45);
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromSeconds(15);
    public int MinimumPlayers { get; init; } = 3;
    public int MaximumPlayers { get; init; } = 4;
}
