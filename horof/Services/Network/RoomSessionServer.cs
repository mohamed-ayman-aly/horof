using horof.Models;
using horof.Services;
using Microsoft.AspNetCore.SignalR;

namespace horof.Services.Network;

public class RoomSessionServer
{
    private readonly GameEngine _engine;
    private readonly Random _random = new();
    private readonly Dictionary<string, string> _connectionToPlayer = new();
    private IHubContext<LobbyHub>? _hub;
    private string _hostAddress = "";

    public event Action? SessionChanged;

    public RoomSessionServer(IQuestionBank questionBank)
    {
        _engine = new GameEngine(questionBank);
        Lobby = new LobbyState();
    }

    public LobbyState Lobby { get; private set; }

    public GameState? Game => _engine.State.Cells.Count > 0 ? _engine.State : null;

    public void SetHubContext(IHubContext<LobbyHub> hub) => _hub = hub;

    public void SetHostAddress(string hostAddress) => _hostAddress = hostAddress;

    public Player CreateRoom(string displayName)
    {
        ResetInternal();

        var host = new Player
        {
            DisplayName = displayName.Trim(),
            IsHost = true,
            Team = Team.Orange,
            IsReady = false
        };

        Lobby = new LobbyState
        {
            RoomCode = GenerateRoomCode(),
            HostAddress = _hostAddress,
            Players = [host]
        };

        return host;
    }

    public JoinResult TryJoin(string connectionId, string roomCode, string displayName)
    {
        if (string.IsNullOrWhiteSpace(Lobby.RoomCode))
            return new JoinResult(false, null, "لا توجد غرفة على هذا المضيف");

        if (!string.Equals(Lobby.RoomCode, roomCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return new JoinResult(false, null, "رمز الغرفة غير صحيح");

        if (Lobby.Players.Count >= LobbyState.MaxPlayers)
            return new JoinResult(false, null, "الغرفة ممتلئة");

        var team = Lobby.Players.Count(p => p.Team == Team.Green) <= Lobby.Players.Count(p => p.Team == Team.Orange)
            ? Team.Green
            : Team.Orange;

        var player = new Player
        {
            DisplayName = displayName.Trim(),
            IsHost = false,
            Team = team,
            IsReady = false
        };

        Lobby.Players.Add(player);
        _connectionToPlayer[connectionId] = player.Id;

        _ = BroadcastAsync();
        return new JoinResult(true, player.Id, null);
    }

    public bool SetReady(string playerId, bool ready)
    {
        var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return false;

        player.IsReady = ready;
        _ = BroadcastAsync();
        return true;
    }

    public bool StartGame(string playerId)
    {
        var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null || !player.IsHost)
            return false;

        if (!Lobby.CanStart)
            return false;

        _engine.StartMatch(_random.Next());
        _ = BroadcastAsync();
        return true;
    }

    public bool SelectHex(string playerId, int hexIndex)
    {
        var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return false;

        if (!_engine.TrySelectHex(hexIndex, player.Team))
            return false;

        _ = BroadcastAsync();
        return true;
    }

    public bool Buzz(string playerId)
    {
        var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return false;

        if (!_engine.TryBuzz(player.Id, player.Team))
            return false;

        _ = BroadcastAsync();
        return true;
    }

    public bool HostJudge(string playerId, bool correct)
    {
        var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null || !player.IsHost)
            return false;

        _engine.HostJudge(correct);
        _ = BroadcastAsync();
        return true;
    }

    public void RemoveConnection(string connectionId)
    {
        if (!_connectionToPlayer.TryGetValue(connectionId, out var playerId))
            return;

        _connectionToPlayer.Remove(connectionId);
        var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return;

        if (player.IsHost)
        {
            ResetInternal();
        }
        else
        {
            Lobby.Players.Remove(player);
        }

        _ = BroadcastAsync();
    }

    public SessionSnapshot GetSnapshot() =>
        SessionMapping.ToSnapshot(Lobby, Game, _hostAddress);

    public void Reset()
    {
        ResetInternal();
        _ = BroadcastAsync();
    }

    private void ResetInternal()
    {
        Lobby = new LobbyState();
        _connectionToPlayer.Clear();
        _engine.StartMatch(0);
        _engine.State.Cells.Clear();
    }

    private async Task BroadcastAsync()
    {
        SessionChanged?.Invoke();

        if (_hub is null)
            return;

        await _hub.Clients.All.SendAsync("SessionUpdated", GetSnapshot());
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(6, Random.Shared, (span, rng) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = chars[rng.Next(chars.Length)];
        });
    }
}
