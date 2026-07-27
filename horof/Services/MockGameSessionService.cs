using horof.Models;

namespace horof.Services;

/// <summary>
/// Local mock session for UI development: simulates host + optional fake players on one device.
/// </summary>
public class MockGameSessionService : IGameSessionService
{
    private readonly GameEngine _engine;
    private readonly Random _random = new();

    public MockGameSessionService(GameEngine engine)
    {
        _engine = engine;
        Lobby = new LobbyState();
    }

    public LobbyState Lobby { get; private set; }
    public GameState? Game => _engine.State.Cells.Count > 0 ? _engine.State : null;
    public Player? LocalPlayer { get; private set; }

    public event Action? LobbyChanged;
    public event Action? GameChanged;
    public event Action<string>? ErrorOccurred;

    public Task CreateRoomAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ErrorOccurred?.Invoke("أدخل اسمك");
            return Task.CompletedTask;
        }

        LocalPlayer = new Player
        {
            DisplayName = displayName.Trim(),
            IsHost = true,
            IsLocal = true,
            Team = Team.Orange,
            IsReady = false
        };

        Lobby = new LobbyState
        {
            RoomCode = GenerateRoomCode(),
            HostAddress = "127.0.0.1:5050",
            Players = [LocalPlayer]
        };

        LobbyChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task JoinRoomAsync(string displayName, string roomCode, string hostAddress)
    {
        _ = hostAddress;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ErrorOccurred?.Invoke("أدخل اسمك");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Trim().Length < 4)
        {
            ErrorOccurred?.Invoke("رمز الغرفة غير صالح");
            return Task.CompletedTask;
        }

        if (Lobby.Players.Count == 0)
        {
            ErrorOccurred?.Invoke("لا توجد غرفة — أنشئ غرفة أولاً (وضع تجريبي)");
            return Task.CompletedTask;
        }

        if (Lobby.Players.Count >= LobbyState.MaxPlayers)
        {
            ErrorOccurred?.Invoke("الغرفة ممتلئة");
            return Task.CompletedTask;
        }

        if (!string.Equals(Lobby.RoomCode, roomCode.Trim().ToUpperInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            ErrorOccurred?.Invoke("رمز الغرفة غير صحيح (في الوضع التجريبي استخدم غرفة المضيف)");
            return Task.CompletedTask;
        }

        var team = Lobby.Players.Count(p => p.Team == Team.Green) <= Lobby.Players.Count(p => p.Team == Team.Orange)
            ? Team.Green
            : Team.Orange;

        LocalPlayer = new Player
        {
            DisplayName = displayName.Trim(),
            IsHost = false,
            IsLocal = true,
            Team = team,
            IsReady = false
        };

        Lobby.Players.Add(LocalPlayer);
        LobbyChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task SetReadyAsync(bool ready)
    {
        if (LocalPlayer is null)
            return Task.CompletedTask;

        LocalPlayer.IsReady = ready;
        LobbyChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task StartGameAsync()
    {
        if (LocalPlayer is null || !LocalPlayer.IsHost)
        {
            ErrorOccurred?.Invoke("فقط المضيف يمكنه بدء اللعبة");
            return Task.CompletedTask;
        }

        if (!Lobby.CanStart)
        {
            ErrorOccurred?.Invoke("يجب أن يكون جميع اللاعبين جاهزين (حد أدنى لاعبان)");
            return Task.CompletedTask;
        }

        var seed = _random.Next();
        _engine.StartMatch(seed);
        GameChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task SelectHexAsync(int hexIndex)
    {
        if (LocalPlayer is null || Game is null)
            return Task.CompletedTask;

        if (_engine.TrySelectHex(hexIndex, LocalPlayer.Team))
            GameChanged?.Invoke();

        return Task.CompletedTask;
    }

    public Task BuzzAsync()
    {
        if (LocalPlayer is null)
            return Task.CompletedTask;

        if (_engine.TryBuzz(LocalPlayer.Id, LocalPlayer.Team))
            GameChanged?.Invoke();

        return Task.CompletedTask;
    }

    public Task HostJudgeAsync(bool correct)
    {
        if (LocalPlayer is null || !LocalPlayer.IsHost)
            return Task.CompletedTask;

        _engine.HostJudge(correct);
        GameChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task LeaveSessionAsync()
    {
        LocalPlayer = null;
        Lobby = new LobbyState();
        _engine.StartMatch(0);
        _engine.State.Cells.Clear();
        LobbyChanged?.Invoke();
        GameChanged?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>Dev helper: add a fake ready player to fill the lobby.</summary>
    public void AddSimulatedPlayer(string name)
    {
        if (Lobby.Players.Count >= LobbyState.MaxPlayers)
            return;

        var team = Lobby.Players.Count(p => p.Team == Team.Green) <= Lobby.Players.Count(p => p.Team == Team.Orange)
            ? Team.Green
            : Team.Orange;

        Lobby.Players.Add(new Player
        {
            DisplayName = name,
            IsHost = false,
            Team = team,
            IsReady = true
        });
        LobbyChanged?.Invoke();
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
