using horof.Models;
using horof.Services;

namespace horof.Services.Network;

public class RoomSessionServer
{
    private readonly GameEngine _engine;
    private readonly Random _random = new();
    private readonly Dictionary<string, string> _connectionToPlayer = new();
    private Func<string, SessionSnapshot, Task>? _pushSession;
    private string _hostAddress = "";

    public event Action? SessionChanged;

    public RoomSessionServer(IQuestionBank questionBank)
    {
        _engine = new GameEngine(questionBank);
    }

    public LobbyState Lobby { get; private set; } = new();

    public GameState? Game => _engine.State.Cells.Count > 0 ? _engine.State : null;

    public void SetSessionPusher(Func<string, SessionSnapshot, Task> push) => _pushSession = push;

    public void SetHostAddress(string hostAddress) => _hostAddress = hostAddress;

    public Player CreateRoom(string displayName)
    {
        ResetInternal();

        var host = new Player
        {
            DisplayName = displayName.Trim(),
            IsHost = true,
            Team = Team.None,
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

        var team = NextBalancedTeam(Lobby.Players);

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
        if (player is null || player.IsHost || player.Team == Team.None)
            return false;

        if (!_engine.TrySelectHex(hexIndex, player.Team))
            return false;

        _ = BroadcastAsync();
        return true;
    }

    public bool Buzz(string playerId)
    {
        var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null || player.IsHost || player.Team == Team.None)
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

        if (_engine.State.Phase == GamePhase.RoundEnded)
            _ = ReturnToLobbyAfterWinAsync();

        return true;
    }

    private async Task ReturnToLobbyAfterWinAsync()
    {
        await Task.Delay(2500);

        if (_engine.State.Phase != GamePhase.RoundEnded)
            return;

        // Keep ready flags so the host can start a rematch immediately.
        _engine.StartMatch(0);
        _engine.State.Cells.Clear();
        await BroadcastAsync();
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

    public SessionSnapshot GetSnapshot(bool includeQuestionSecrets = true) =>
        SessionMapping.ToSnapshot(Lobby, Game, _hostAddress, includeQuestionSecrets);

    public SessionSnapshot GetSnapshotForConnection(string connectionId)
    {
        var includeSecrets = false;
        if (_connectionToPlayer.TryGetValue(connectionId, out var playerId))
        {
            var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
            includeSecrets = player?.IsHost ?? false;
        }

        return GetSnapshot(includeSecrets);
    }

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

        if (_pushSession is null || _connectionToPlayer.Count == 0)
            return;

        foreach (var (connectionId, playerId) in _connectionToPlayer)
        {
            var player = Lobby.Players.FirstOrDefault(p => p.Id == playerId);
            var includeSecrets = player?.IsHost ?? false;
            await _pushSession(connectionId, GetSnapshot(includeSecrets));
        }
    }

    private static Team NextBalancedTeam(IEnumerable<Player> players)
    {
        var green = players.Count(p => p.Team == Team.Green);
        var orange = players.Count(p => p.Team == Team.Orange);
        return green <= orange ? Team.Green : Team.Orange;
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
