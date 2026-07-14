using System.Security.Cryptography;
using System.Text;
using Karo.Api.Models;
using Microsoft.Extensions.Hosting;

namespace Karo.Api.Services;

public sealed class LobbyService
{
    private const int RoomCodeLength = 6;
    private const int MaxPlayerNameLength = 18;
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly string[] PlayerColors = ["#d95f43", "#2f7f75", "#4269b2", "#d6a230"];

    private readonly object _gate = new();
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _connectionRooms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _staleConnectionPlayerIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayerSession> _sessionsByPlayerId = new(StringComparer.OrdinalIgnoreCase);
    private readonly RoomLifecycleOptions _options;
    private readonly bool _isDevelopment;

    public LobbyService(RoomLifecycleOptions? options = null, IHostEnvironment? environment = null)
    {
        _options = options ?? new RoomLifecycleOptions();
        _isDevelopment = environment?.IsDevelopment() ?? true;
    }

    public Room CreateRoom(string connectionId, string playerName)
    {
        return CreateRoomSession(connectionId, playerName).Room;
    }

    public PlayerSessionResult CreateRoomSession(string connectionId, string playerName)
    {
        lock (_gate)
        {
            EnsureConnectionIsAvailable(connectionId);

            var roomCode = GenerateRoomCode();
            var now = DateTimeOffset.UtcNow;
            var player = CreatePlayer(connectionId, playerName, isHost: true, colorIndex: 0, now);
            var room = new Room
            {
                RoomCode = roomCode,
                HostPlayerId = player.PlayerId,
                LastActivityAt = now
            };

            room.Players.Add(player);
            _rooms[room.RoomCode] = room;
            _connectionRooms[connectionId] = room.RoomCode;
            var token = IssueSession(room, player, now);
            return new PlayerSessionResult(room, player, token);
        }
    }

    public Room JoinRoom(string connectionId, string roomCode, string playerName)
    {
        return JoinRoomSession(connectionId, roomCode, playerName).Room;
    }

    public PlayerSessionResult JoinRoomSession(string connectionId, string roomCode, string playerName)
    {
        lock (_gate)
        {
            EnsureConnectionIsAvailable(connectionId);
            var room = GetRoomOrThrow(roomCode);

            if (room.Status != RoomStatus.Waiting)
            {
                throw new LobbyException(room.Status == RoomStatus.PostGame
                    ? "This room is between matches. Return to the lobby before joining."
                    : "This room is already in game.");
            }

            if (EligibleRoomPlayers(room).Count >= _options.MaximumPlayers)
            {
                throw new LobbyException("This room is full.");
            }

            var now = DateTimeOffset.UtcNow;
            var player = CreatePlayer(connectionId, playerName, isHost: false, colorIndex: room.Players.Count, now);
            room.Players.Add(player);
            ResetReadiness(room);
            Touch(room, now);
            _connectionRooms[connectionId] = room.RoomCode;
            var token = IssueSession(room, player, now);
            return new PlayerSessionResult(room, player, token);
        }
    }

    public PlayerSessionResult ResumeRoomSession(string connectionId, string roomCode, string playerId, string reconnectToken)
    {
        lock (_gate)
        {
            var room = GetRoomOrThrow(roomCode);
            var player = room.Players.FirstOrDefault(candidate =>
                    string.Equals(candidate.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                ?? throw new LobbyException("Your player session could not be restored.", "PlayerSessionNotFound");

            if (player.ConnectionStatus is PlayerConnectionStatus.Left or PlayerConnectionStatus.Forfeited)
            {
                throw new LobbyException("This player session is no longer active.", "PlayerAlreadyLeft");
            }

            if (!_sessionsByPlayerId.TryGetValue(player.PlayerId, out var session)
                || session.RevokedAt is not null
                || !string.Equals(session.RoomCode, room.RoomCode, StringComparison.OrdinalIgnoreCase)
                || !TokensMatch(session.ReconnectTokenHash, reconnectToken))
            {
                throw new LobbyException("Your player session could not be restored.", "InvalidReconnectToken");
            }

            var now = DateTimeOffset.UtcNow;
            if (player.DisconnectedAt is not null
                && now - player.DisconnectedAt.Value > _options.ReconnectGracePeriod)
            {
                player.ConnectionStatus = PlayerConnectionStatus.TimedOut;
                RevokeSession(player.PlayerId, now);
                Touch(room, now);
                throw new LobbyException("The reconnect window has expired.", "ReconnectGraceExpired");
            }

            var replacedConnectionId = player.ConnectionId;
            if (!string.IsNullOrWhiteSpace(replacedConnectionId)
                && !string.Equals(replacedConnectionId, connectionId, StringComparison.Ordinal))
            {
                _connectionRooms.Remove(replacedConnectionId);
                _staleConnectionPlayerIds[replacedConnectionId] = player.PlayerId;
            }

            if (_connectionRooms.TryGetValue(connectionId, out var connectedRoomCode)
                && !string.Equals(connectedRoomCode, room.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new LobbyException("This connection is already seated at another Karo table.");
            }

            player.ConnectionId = connectionId;
            player.ConnectionStatus = PlayerConnectionStatus.Connected;
            player.DisconnectedAt = null;
            player.LastSeenAt = now;
            _connectionRooms[connectionId] = room.RoomCode;
            _staleConnectionPlayerIds.Remove(connectionId);
            Touch(room, now);
            return new PlayerSessionResult(room, player, reconnectToken, replacedConnectionId);
        }
    }

    public PlayerSessionResult RecoverCurrentSession(string connectionId)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionUnsafe(connectionId)
                ?? throw new LobbyException("You are not connected to a room.", "SessionNotConnected");
            var player = GetPlayerForConnectionOrThrow(room, connectionId);
            if (player.ConnectionStatus != PlayerConnectionStatus.Connected)
            {
                throw new LobbyException("Your player session could not be restored.", "PlayerSessionNotFound");
            }

            var now = DateTimeOffset.UtcNow;
            var reconnectToken = IssueSession(room, player, now);
            Touch(room, now);
            return new PlayerSessionResult(room, player, reconnectToken);
        }
    }

    public Room StartGame(string connectionId, string roomCode)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionOrThrow(connectionId, roomCode);
            var player = GetPlayerForConnectionOrThrow(room, connectionId);
            if (!string.Equals(room.HostPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new LobbyException("Only the host can start the game.");
            }

            var eligiblePlayers = EligibleRoomPlayers(room);
            var minimumPlayers = _isDevelopment ? 1 : _options.MinimumPlayers;
            if (eligiblePlayers.Count < minimumPlayers || eligiblePlayers.Count > _options.MaximumPlayers)
            {
                throw new LobbyException(_isDevelopment
                    ? "Karo requires 1-4 players in Development."
                    : "Karo requires 3-4 players.", "UnsupportedPlayerCount");
            }

            if (eligiblePlayers.Any(candidate => !candidate.IsReady || candidate.ConnectionStatus != PlayerConnectionStatus.Connected))
            {
                throw new LobbyException("Waiting for all players to be ready.", "PlayersNotReady");
            }

            room.Status = RoomStatus.InGame;
            ResetReadiness(room);
            Touch(room, DateTimeOffset.UtcNow);
            return room;
        }
    }

    public Room RollbackGameStart(string connectionId, string roomCode)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionOrThrow(connectionId, roomCode);
            var player = GetPlayerForConnectionOrThrow(room, connectionId);
            if (!string.Equals(room.HostPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new LobbyException("Only the host can restart this room.");
            }

            room.Status = RoomStatus.Waiting;
            Touch(room, DateTimeOffset.UtcNow);
            return room;
        }
    }

    public Room SetReady(string connectionId, string roomCode, bool isReady)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionOrThrow(connectionId, roomCode);
            if (room.Status != RoomStatus.Waiting)
            {
                throw new LobbyException("Ready state is only available in the lobby.");
            }

            var player = GetPlayerForConnectionOrThrow(room, connectionId);
            player.IsReady = isReady && player.ConnectionStatus == PlayerConnectionStatus.Connected;
            Touch(room, DateTimeOffset.UtcNow);
            return room;
        }
    }

    public Room? GetRoomForConnection(string connectionId)
    {
        lock (_gate)
        {
            return _connectionRooms.TryGetValue(connectionId, out var roomCode)
                   && _rooms.TryGetValue(roomCode, out var room)
                   && room.Players.Any(player => string.Equals(player.ConnectionId, connectionId, StringComparison.Ordinal))
                ? room
                : null;
        }
    }

    public Room? GetRoom(string roomCode)
    {
        lock (_gate)
        {
            _rooms.TryGetValue(NormalizeRoomCode(roomCode), out var room);
            return room;
        }
    }

    public Player? GetPlayerForConnection(string connectionId)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionUnsafe(connectionId);
            return room?.Players.FirstOrDefault(player => string.Equals(player.ConnectionId, connectionId, StringComparison.Ordinal));
        }
    }

    public bool IsStaleConnection(string connectionId)
    {
        lock (_gate)
        {
            return _staleConnectionPlayerIds.ContainsKey(connectionId);
        }
    }

    public LifecycleDisconnectResult Disconnect(string connectionId)
    {
        lock (_gate)
        {
            if (!_connectionRooms.Remove(connectionId, out var roomCode)
                || !_rooms.TryGetValue(roomCode, out var room))
            {
                return new LifecycleDisconnectResult(null, null, null, false, false);
            }

            var player = room.Players.FirstOrDefault(item => string.Equals(item.ConnectionId, connectionId, StringComparison.Ordinal));
            if (player is null)
            {
                return new LifecycleDisconnectResult(roomCode, null, room, false, false);
            }

            var now = DateTimeOffset.UtcNow;
            player.ConnectionId = null;
            player.ConnectionStatus = PlayerConnectionStatus.Reconnecting;
            player.DisconnectedAt = now;
            player.LastSeenAt = now;
            player.IsReady = false;
            Touch(room, now);
            return new LifecycleDisconnectResult(roomCode, player.PlayerId, room, false, true);
        }
    }

    public LifecycleDisconnectResult LeaveLobby(string connectionId, string roomCode)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionOrThrow(connectionId, roomCode);
            if (room.Status == RoomStatus.InGame)
            {
                throw new LobbyException("Leaving an active match requires forfeiting the match.");
            }

            var player = GetPlayerForConnectionOrThrow(room, connectionId);
            RemovePlayer(room, player, PlayerConnectionStatus.Left, DateTimeOffset.UtcNow);
            return FinishDeparture(room, player.PlayerId);
        }
    }

    public Room MarkForfeited(string connectionId, string roomCode)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionOrThrow(connectionId, roomCode);
            if (room.Status != RoomStatus.InGame)
            {
                throw new LobbyException("There is no active match to forfeit.");
            }

            var player = GetPlayerForConnectionOrThrow(room, connectionId);
            var now = DateTimeOffset.UtcNow;
            player.HasForfeited = true;
            player.IsReady = false;
            player.ConnectionStatus = PlayerConnectionStatus.Forfeited;
            player.ConnectionId = null;
            player.DisconnectedAt = now;
            RevokeSession(player.PlayerId, now);
            _connectionRooms.Remove(connectionId);
            _staleConnectionPlayerIds[connectionId] = player.PlayerId;
            if (string.Equals(room.HostPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                AssignNextHost(room);
            }

            Touch(room, now);
            return room;
        }
    }

    public Room ContinueWithoutPlayer(string connectionId, string roomCode, string timedOutPlayerId)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionOrThrow(connectionId, roomCode);
            EnsureHost(room, connectionId);
            var player = room.Players.FirstOrDefault(candidate => string.Equals(candidate.PlayerId, timedOutPlayerId, StringComparison.OrdinalIgnoreCase))
                ?? throw new LobbyException("Choose a timed out player.");
            if (player.ConnectionStatus != PlayerConnectionStatus.TimedOut)
            {
                throw new LobbyException("That player has not timed out.");
            }

            player.HasForfeited = true;
            player.ConnectionStatus = PlayerConnectionStatus.Forfeited;
            RevokeSession(player.PlayerId, DateTimeOffset.UtcNow);
            if (string.Equals(room.HostPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                AssignNextHost(room);
            }

            Touch(room, DateTimeOffset.UtcNow);
            return room;
        }
    }

    public Room MarkPostGame(string roomCode)
    {
        lock (_gate)
        {
            var room = GetRoomOrThrow(roomCode);
            room.Status = RoomStatus.PostGame;
            room.PostGameAt = DateTimeOffset.UtcNow;
            Touch(room, room.PostGameAt.Value);
            return room;
        }
    }

    public LifecycleDisconnectResult ReturnToLobby(string connectionId, string roomCode)
    {
        lock (_gate)
        {
            var room = GetRoomForConnectionOrThrow(connectionId, roomCode);
            EnsureHost(room, connectionId);
            if (room.Status != RoomStatus.PostGame)
            {
                throw new LobbyException("The match must be finished before returning to the lobby.", "MatchNotFinished");
            }

            var now = DateTimeOffset.UtcNow;
            var departing = room.Players
                .Where(player => player.HasForfeited || player.ConnectionStatus != PlayerConnectionStatus.Connected)
                .ToList();
            foreach (var player in departing)
            {
                RemovePlayer(room, player, player.ConnectionStatus, now);
            }

            if (room.Players.Count == 0)
            {
                _rooms.Remove(room.RoomCode);
                return new LifecycleDisconnectResult(room.RoomCode, null, null, true, false);
            }

            room.Status = RoomStatus.Waiting;
            room.PostGameAt = null;
            ResetReadiness(room);
            AssignNextHost(room, preserveCurrentWhenEligible: true);
            Touch(room, now);
            return new LifecycleDisconnectResult(room.RoomCode, null, room, false, false);
        }
    }

    public IReadOnlyList<RoomCleanupResult> ProcessLifecycle(DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            var timestamp = now ?? DateTimeOffset.UtcNow;
            var results = new List<RoomCleanupResult>();

            foreach (var room in _rooms.Values.ToList())
            {
                var timedOutPlayerIds = new List<string>();
                foreach (var player in room.Players.Where(player => player.ConnectionStatus == PlayerConnectionStatus.Reconnecting).ToList())
                {
                    if (player.DisconnectedAt is null || timestamp - player.DisconnectedAt.Value < _options.ReconnectGracePeriod)
                    {
                        continue;
                    }

                    player.ConnectionStatus = PlayerConnectionStatus.TimedOut;
                    player.IsReady = false;
                    RevokeSession(player.PlayerId, timestamp);
                    timedOutPlayerIds.Add(player.PlayerId);
                    if (room.Status == RoomStatus.Waiting)
                    {
                        RemovePlayer(room, player, PlayerConnectionStatus.TimedOut, timestamp);
                    }
                }

                if (timedOutPlayerIds.Count > 0 && room.Status != RoomStatus.Waiting)
                {
                    if (room.Players.Any(player => string.Equals(player.PlayerId, room.HostPlayerId, StringComparison.OrdinalIgnoreCase)
                        && player.ConnectionStatus == PlayerConnectionStatus.TimedOut))
                    {
                        AssignNextHost(room);
                    }

                    Touch(room, timestamp);
                }

                var noReconnectablePlayers = room.Players.All(player => player.ConnectionStatus is not PlayerConnectionStatus.Connected and not PlayerConnectionStatus.Reconnecting);
                var stalePostGame = room.Status == RoomStatus.PostGame
                    && room.PostGameAt is not null
                    && timestamp - room.PostGameAt.Value >= _options.PostGameRoomTtl;
                var staleDisconnectedRoom = noReconnectablePlayers
                    && timestamp - room.LastActivityAt >= _options.FullyDisconnectedRoomTtl;

                if (room.Players.Count == 0 || stalePostGame || staleDisconnectedRoom)
                {
                    foreach (var player in room.Players)
                    {
                        RevokeSession(player.PlayerId, timestamp);
                        if (!string.IsNullOrWhiteSpace(player.ConnectionId))
                        {
                            _connectionRooms.Remove(player.ConnectionId);
                        }
                    }

                    _rooms.Remove(room.RoomCode);
                    results.Add(new RoomCleanupResult(null, room.RoomCode, timedOutPlayerIds, true));
                    continue;
                }

                if (timedOutPlayerIds.Count > 0)
                {
                    results.Add(new RoomCleanupResult(room, room.RoomCode, timedOutPlayerIds, false));
                }
            }

            return results;
        }
    }

    private Room GetRoomForConnectionOrThrow(string connectionId, string roomCode)
    {
        var room = GetRoomOrThrow(roomCode);
        if (!_connectionRooms.TryGetValue(connectionId, out var connectedRoomCode)
            || !string.Equals(connectedRoomCode, room.RoomCode, StringComparison.OrdinalIgnoreCase))
        {
            if (_staleConnectionPlayerIds.ContainsKey(connectionId))
            {
                throw new LobbyException("This player session is active in another browser tab or window.", "SessionReplaced");
            }

            throw new LobbyException("You are not connected to this room.");
        }

        return room;
    }

    private Player GetPlayerForConnectionOrThrow(Room room, string connectionId)
    {
        return room.Players.FirstOrDefault(player => string.Equals(player.ConnectionId, connectionId, StringComparison.Ordinal))
            ?? throw new LobbyException(_staleConnectionPlayerIds.ContainsKey(connectionId)
                ? "This player session is active in another browser tab or window."
                : "You are not in this room.");
    }

    private Room? GetRoomForConnectionUnsafe(string connectionId)
    {
        return _connectionRooms.TryGetValue(connectionId, out var roomCode)
               && _rooms.TryGetValue(roomCode, out var room)
            ? room
            : null;
    }

    private Room GetRoomOrThrow(string roomCode)
    {
        roomCode = NormalizeRoomCode(roomCode);
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            throw new LobbyException("This room no longer exists.", "RoomNotFound");
        }

        return room;
    }

    private void EnsureConnectionIsAvailable(string connectionId)
    {
        if (_connectionRooms.ContainsKey(connectionId))
        {
            throw new LobbyException("This connection is already seated at a Karo table.");
        }
    }

    private static Player CreatePlayer(string connectionId, string playerName, bool isHost, int colorIndex, DateTimeOffset now)
    {
        return new Player
        {
            PlayerId = Guid.NewGuid().ToString("N"),
            PlayerName = NormalizePlayerName(playerName),
            ConnectionId = connectionId,
            ConnectionStatus = PlayerConnectionStatus.Connected,
            IsHost = isHost,
            PlayerColor = PlayerColors[colorIndex % PlayerColors.Length],
            JoinedAt = now,
            LastSeenAt = now
        };
    }

    private string IssueSession(Room room, Player player, DateTimeOffset now)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessionsByPlayerId[player.PlayerId] = new PlayerSession
        {
            PlayerId = player.PlayerId,
            RoomCode = room.RoomCode,
            ReconnectTokenHash = HashToken(token),
            IssuedAt = now
        };
        return token;
    }

    private void RevokeSession(string playerId, DateTimeOffset now)
    {
        if (_sessionsByPlayerId.TryGetValue(playerId, out var session))
        {
            session.RevokedAt = now;
        }
    }

    private static bool TokensMatch(string expectedHash, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var expected = Convert.FromHexString(expectedHash);
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private string GenerateRoomCode()
    {
        for (var attempts = 0; attempts < 100; attempts++)
        {
            var code = new string(Enumerable
                .Range(0, RoomCodeLength)
                .Select(_ => RoomCodeAlphabet[RandomNumberGenerator.GetInt32(RoomCodeAlphabet.Length)])
                .ToArray());
            if (!_rooms.ContainsKey(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a room code.");
    }

    private static IReadOnlyList<Player> EligibleRoomPlayers(Room room)
    {
        return room.Players
            .Where(player => !player.HasForfeited && player.ConnectionStatus is not PlayerConnectionStatus.Left and not PlayerConnectionStatus.TimedOut and not PlayerConnectionStatus.Forfeited)
            .ToList();
    }

    private static void ResetReadiness(Room room)
    {
        foreach (var player in room.Players)
        {
            player.IsReady = false;
        }
    }

    private static void Touch(Room room, DateTimeOffset now)
    {
        room.LastActivityAt = now;
        room.RoomStateVersion++;
    }

    private void RemovePlayer(Room room, Player player, PlayerConnectionStatus finalStatus, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(player.ConnectionId))
        {
            _connectionRooms.Remove(player.ConnectionId);
        }

        player.ConnectionStatus = finalStatus;
        player.IsReady = false;
        player.ConnectionId = null;
        player.DisconnectedAt = now;
        RevokeSession(player.PlayerId, now);
        room.Players.Remove(player);
        if (string.Equals(room.HostPlayerId, player.PlayerId, StringComparison.OrdinalIgnoreCase))
        {
            AssignNextHost(room);
        }

        Touch(room, now);
    }

    private LifecycleDisconnectResult FinishDeparture(Room room, string playerId)
    {
        if (room.Players.Count == 0)
        {
            _rooms.Remove(room.RoomCode);
            return new LifecycleDisconnectResult(room.RoomCode, playerId, null, true, true);
        }

        return new LifecycleDisconnectResult(room.RoomCode, playerId, room, false, true);
    }

    private static void EnsureHost(Room room, string connectionId)
    {
        var host = room.Players.FirstOrDefault(player => string.Equals(player.PlayerId, room.HostPlayerId, StringComparison.OrdinalIgnoreCase));
        if (host is null || !string.Equals(host.ConnectionId, connectionId, StringComparison.Ordinal))
        {
            throw new LobbyException("Only the room host can perform that action.", "NotRoomHost");
        }
    }

    private static void AssignNextHost(Room room, bool preserveCurrentWhenEligible = false)
    {
        var candidates = room.Players
            .Where(player => !player.HasForfeited
                && player.ConnectionStatus is (PlayerConnectionStatus.Connected or PlayerConnectionStatus.Reconnecting))
            .OrderBy(player => player.ConnectionStatus == PlayerConnectionStatus.Connected ? 0 : 1)
            .ThenBy(player => player.JoinedAt)
            .ToList();
        var currentHostIsEligible = candidates.Any(player => string.Equals(player.PlayerId, room.HostPlayerId, StringComparison.OrdinalIgnoreCase));
        var nextHost = preserveCurrentWhenEligible && currentHostIsEligible
            ? candidates.First(player => string.Equals(player.PlayerId, room.HostPlayerId, StringComparison.OrdinalIgnoreCase))
            : candidates.FirstOrDefault();

        foreach (var player in room.Players)
        {
            player.IsHost = nextHost is not null && string.Equals(player.PlayerId, nextHost.PlayerId, StringComparison.OrdinalIgnoreCase);
        }

        room.HostPlayerId = nextHost?.PlayerId ?? string.Empty;
    }

    private static string NormalizePlayerName(string playerName)
    {
        playerName = (playerName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(playerName))
        {
            throw new LobbyException("Player name is required.");
        }

        return playerName.Length > MaxPlayerNameLength ? playerName[..MaxPlayerNameLength] : playerName;
    }

    private static string NormalizeRoomCode(string roomCode)
    {
        return (roomCode ?? string.Empty).Trim().ToUpperInvariant();
    }
}
