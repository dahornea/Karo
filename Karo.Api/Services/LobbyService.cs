using Karo.Api.Models;

namespace Karo.Api.Services;

public sealed class LobbyService
{
    private const int RoomCodeLength = 6;
    private const int MaxPlayersPerRoom = 4;
    private const int MaxPlayerNameLength = 18;
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly object _gate = new();
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _connectionRooms = new(StringComparer.Ordinal);

    public Room CreateRoom(string connectionId, string playerName)
    {
        lock (_gate)
        {
            var roomCode = GenerateRoomCode();
            var player = CreatePlayer(connectionId, playerName, isHost: true);
            var room = new Room
            {
                RoomCode = roomCode,
                HostConnectionId = connectionId
            };

            room.Players.Add(player);
            _rooms[room.RoomCode] = room;
            _connectionRooms[connectionId] = room.RoomCode;
            return room;
        }
    }

    public Room JoinRoom(string connectionId, string roomCode, string playerName)
    {
        lock (_gate)
        {
            roomCode = NormalizeRoomCode(roomCode);

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                throw new LobbyException("Invalid room code.");
            }

            if (room.Status == RoomStatus.InGame)
            {
                throw new LobbyException("This room is already in game.");
            }

            if (room.Players.Count >= MaxPlayersPerRoom)
            {
                throw new LobbyException("This room is full.");
            }

            var player = CreatePlayer(connectionId, playerName, isHost: false);
            room.Players.Add(player);
            _connectionRooms[connectionId] = room.RoomCode;
            return room;
        }
    }

    public Room StartGame(string connectionId, string roomCode)
    {
        lock (_gate)
        {
            var room = GetRoomOrThrow(roomCode);

            if (room.HostConnectionId != connectionId)
            {
                throw new LobbyException("Only the host can start the game.");
            }

            room.Status = RoomStatus.InGame;
            return room;
        }
    }

    public Room? GetRoomForConnection(string connectionId)
    {
        lock (_gate)
        {
            return _connectionRooms.TryGetValue(connectionId, out var roomCode) &&
                   _rooms.TryGetValue(roomCode, out var room)
                ? room
                : null;
        }
    }

    public DisconnectResult Disconnect(string connectionId)
    {
        lock (_gate)
        {
            if (!_connectionRooms.Remove(connectionId, out var roomCode) ||
                !_rooms.TryGetValue(roomCode, out var room))
            {
                return new DisconnectResult(null, null, false);
            }

            var player = room.Players.FirstOrDefault(item => item.ConnectionId == connectionId);
            if (player is not null)
            {
                room.Players.Remove(player);
            }

            if (room.Players.Count == 0)
            {
                _rooms.Remove(room.RoomCode);
                return new DisconnectResult(room.RoomCode, null, true);
            }

            if (room.HostConnectionId == connectionId)
            {
                AssignNextHost(room);
            }

            return new DisconnectResult(room.RoomCode, room, false);
        }
    }

    private Room GetRoomOrThrow(string roomCode)
    {
        roomCode = NormalizeRoomCode(roomCode);

        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            throw new LobbyException("Invalid room code.");
        }

        return room;
    }

    private static Player CreatePlayer(string connectionId, string playerName, bool isHost)
    {
        playerName = NormalizePlayerName(playerName);

        return new Player
        {
            PlayerId = Guid.NewGuid().ToString("N"),
            PlayerName = playerName,
            ConnectionId = connectionId,
            IsHost = isHost
        };
    }

    private string GenerateRoomCode()
    {
        for (var attempts = 0; attempts < 100; attempts++)
        {
            var code = new string(Enumerable
                .Range(0, RoomCodeLength)
                .Select(_ => RoomCodeAlphabet[Random.Shared.Next(RoomCodeAlphabet.Length)])
                .ToArray());

            if (!_rooms.ContainsKey(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a room code.");
    }

    private static void AssignNextHost(Room room)
    {
        foreach (var player in room.Players)
        {
            player.IsHost = false;
        }

        var nextHost = room.Players.OrderBy(player => player.JoinedAt).First();
        nextHost.IsHost = true;
        room.HostConnectionId = nextHost.ConnectionId;
    }

    private static string NormalizePlayerName(string playerName)
    {
        playerName = (playerName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(playerName))
        {
            throw new LobbyException("Player name is required.");
        }

        return playerName.Length > MaxPlayerNameLength
            ? playerName[..MaxPlayerNameLength]
            : playerName;
    }

    private static string NormalizeRoomCode(string roomCode)
    {
        return (roomCode ?? "").Trim().ToUpperInvariant();
    }
}

public sealed record DisconnectResult(
    string? RoomCode,
    Room? Room,
    bool RoomRemoved);
