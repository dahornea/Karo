using Karo.Api.DTOs;
using Karo.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Karo.Api.Services;

public sealed class RoomCleanupHostedService : BackgroundService
{
    private readonly RoomLifecycleService _lifecycleService;
    private readonly GameService _gameService;
    private readonly IHubContext<GameLobbyHub> _hubContext;
    private readonly RoomLifecycleOptions _options;
    private readonly ILogger<RoomCleanupHostedService> _logger;

    public RoomCleanupHostedService(
        RoomLifecycleService lifecycleService,
        GameService gameService,
        IHubContext<GameLobbyHub> hubContext,
        RoomLifecycleOptions options,
        ILogger<RoomCleanupHostedService> logger)
    {
        _lifecycleService = lifecycleService;
        _gameService = gameService;
        _hubContext = hubContext;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var result in _lifecycleService.ProcessLifecycle())
                {
                    if (result.RoomRemoved)
                    {
                        await _hubContext.Clients.Group(result.RoomCode).SendAsync("RoomClosed", result.RoomCode, stoppingToken);
                        continue;
                    }

                    if (result.Room is null)
                    {
                        continue;
                    }

                    await _hubContext.Clients.Group(result.Room.RoomCode).SendAsync("RoomUpdated", result.Room.ToDto(), stoppingToken);
                    var game = _gameService.GetGame(result.Room.RoomCode);
                    if (game is null)
                    {
                        continue;
                    }

                    foreach (var player in result.Room.Players.Where(player => !string.IsNullOrWhiteSpace(player.ConnectionId)))
                    {
                        await _hubContext.Clients.Client(player.ConnectionId!).SendAsync("GameUpdated", game.ToDto(player.PlayerId), stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Karo room lifecycle cleanup iteration failed.");
            }
        }
    }
}
