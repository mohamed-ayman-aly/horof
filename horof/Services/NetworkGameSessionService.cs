using horof.Models;
using horof.Services.Network;

namespace horof.Services;

public class NetworkGameSessionService : IGameSessionService, IAsyncDisposable
{
    private readonly GameHostRunner _hostRunner;
    private LanGameClient? _client;
    private string? _localPlayerId;
    private bool _isHost;
    private Action? _hostSessionHandler;

    public NetworkGameSessionService(GameHostRunner hostRunner)
    {
        _hostRunner = hostRunner;
        Lobby = new LobbyState();
    }

    public LobbyState Lobby { get; private set; }
    public GameState? Game { get; private set; }
    public Player? LocalPlayer { get; private set; }

    public event Action? LobbyChanged;
    public event Action? GameChanged;
    public event Action<string>? ErrorOccurred;

    public async Task CreateRoomAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ErrorOccurred?.Invoke("أدخل اسمك");
            return;
        }

        try
        {
            await DisconnectClientAsync();
            var server = await _hostRunner.EnsureStartedAsync();
            SubscribeHostSessionUpdates(server);
            var hostPlayer = server.CreateRoom(displayName.Trim());
            _localPlayerId = hostPlayer.Id;
            _isHost = true;
            ApplySnapshot(server.GetSnapshot(), hostPlayer.Id);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"تعذر تشغيل المضيف: {ex.Message}");
        }
    }

    public async Task JoinRoomAsync(string displayName, string roomCode, string hostAddress)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ErrorOccurred?.Invoke("أدخل اسمك");
            return;
        }

        if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Trim().Length < 4)
        {
            ErrorOccurred?.Invoke("رمز الغرفة غير صالح");
            return;
        }

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            ErrorOccurred?.Invoke("أدخل عنوان المضيف (IP) — يظهر في شاشة المضيف");
            return;
        }

        try
        {
            if (_isHost)
                await _hostRunner.StopAsync();

            await DisconnectClientAsync();

            var (host, port) = NetworkHelper.ParseHostAddress(hostAddress);
            var client = new LanGameClient();
            client.SessionUpdated += OnClientSessionUpdated;
            client.Disconnected += OnClientDisconnected;
            await client.ConnectAsync(host, port);
            _client = client;

            var result = await client.InvokeAsync<JoinResult>(
                LanMethods.JoinRoom,
                roomCode.Trim().ToUpperInvariant(),
                displayName.Trim());

            if (!result.Success || result.PlayerId is null)
            {
                ErrorOccurred?.Invoke(result.ErrorMessage ?? "تعذر الانضمام للغرفة");
                await DisconnectClientAsync();
                return;
            }

            _localPlayerId = result.PlayerId;
            _isHost = false;

            var snapshot = await client.InvokeAsync<SessionSnapshot>(LanMethods.GetSnapshot);
            ApplySnapshot(snapshot, _localPlayerId);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"تعذر الاتصال بالمضيف: {ex.Message}");
            await DisconnectClientAsync();
        }
    }

    public async Task SetReadyAsync(bool ready)
    {
        if (_localPlayerId is null)
            return;

        if (_isHost && _hostRunner.Server is not null)
        {
            _hostRunner.Server.SetReady(_localPlayerId, ready);
            ApplySnapshot(_hostRunner.Server.GetSnapshot(), _localPlayerId);
            return;
        }

        if (_client?.IsConnected == true)
            await _client.InvokeAsync<bool>(LanMethods.SetReady, _localPlayerId, ready);
    }

    public async Task StartGameAsync()
    {
        if (_localPlayerId is null)
            return;

        if (_isHost && _hostRunner.Server is not null)
        {
            if (!_hostRunner.Server.StartGame(_localPlayerId))
                ErrorOccurred?.Invoke("يلزم المضيف مع لاعبين أو أربعة لاعبين، وجميعهم جاهزون");
            else
                ApplySnapshot(_hostRunner.Server.GetSnapshot(), _localPlayerId);

            return;
        }

        ErrorOccurred?.Invoke("فقط المضيف يمكنه بدء اللعبة");
        await Task.CompletedTask;
    }

    public async Task SelectHexAsync(int hexIndex)
    {
        if (_localPlayerId is null)
            return;

        if (_isHost && _hostRunner.Server is not null)
        {
            _hostRunner.Server.SelectHex(_localPlayerId, hexIndex);
            ApplySnapshot(_hostRunner.Server.GetSnapshot(), _localPlayerId);
            return;
        }

        if (_client?.IsConnected == true)
            await _client.InvokeAsync<bool>(LanMethods.SelectHex, _localPlayerId, hexIndex);
    }

    public async Task BuzzAsync()
    {
        if (_localPlayerId is null)
            return;

        if (_isHost && _hostRunner.Server is not null)
        {
            _hostRunner.Server.Buzz(_localPlayerId);
            ApplySnapshot(_hostRunner.Server.GetSnapshot(), _localPlayerId);
            return;
        }

        if (_client?.IsConnected == true)
            await _client.InvokeAsync<bool>(LanMethods.Buzz, _localPlayerId);
    }

    public async Task HostJudgeAsync(bool correct)
    {
        if (_localPlayerId is null)
            return;

        if (_isHost && _hostRunner.Server is not null)
        {
            _hostRunner.Server.HostJudge(_localPlayerId, correct);
            ApplySnapshot(_hostRunner.Server.GetSnapshot(), _localPlayerId);
            return;
        }

        if (_client?.IsConnected == true)
            await _client.InvokeAsync<bool>(LanMethods.HostJudge, _localPlayerId, correct);
    }

    public async Task LeaveSessionAsync()
    {
        UnsubscribeHostSessionUpdates();

        if (_isHost && _hostRunner.Server is not null)
            _hostRunner.Server.Reset();

        await _hostRunner.StopAsync();
        await DisconnectClientAsync();

        _localPlayerId = null;
        _isHost = false;
        Lobby = new LobbyState();
        Game = null;
        LocalPlayer = null;
        LobbyChanged?.Invoke();
        GameChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await LeaveSessionAsync();
    }

    private void OnClientSessionUpdated(SessionSnapshot snapshot)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_localPlayerId is not null)
                ApplySnapshot(snapshot, _localPlayerId);
        });
    }

    private void OnClientDisconnected(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_isHost || _localPlayerId is null)
                return;

            ErrorOccurred?.Invoke(message);
        });
    }

    private void ApplySnapshot(SessionSnapshot snapshot, string localPlayerId)
    {
        Lobby = new LobbyState
        {
            RoomCode = snapshot.RoomCode,
            HostAddress = snapshot.HostAddress,
            Players = snapshot.Players.Select(p => SessionMapping.FromDto(p, p.Id == localPlayerId)).ToList()
        };

        LocalPlayer = Lobby.Players.FirstOrDefault(p => p.Id == localPlayerId);
        Game = snapshot.Game is null ? null : SessionMapping.FromDto(snapshot.Game);

        LobbyChanged?.Invoke();
        GameChanged?.Invoke();
    }

    private async Task DisconnectClientAsync()
    {
        if (_client is null)
            return;

        _client.SessionUpdated -= OnClientSessionUpdated;
        _client.Disconnected -= OnClientDisconnected;

        try
        {
            await _client.DisposeAsync();
        }
        catch
        {
            // ignore disconnect errors
        }

        _client = null;
    }

    private void SubscribeHostSessionUpdates(RoomSessionServer server)
    {
        UnsubscribeHostSessionUpdates();
        _hostSessionHandler = () =>
        {
            if (!_isHost || _localPlayerId is null)
                return;

            var snapshot = server.GetSnapshot();
            MainThread.BeginInvokeOnMainThread(() => ApplySnapshot(snapshot, _localPlayerId));
        };
        server.SessionChanged += _hostSessionHandler;
    }

    private void UnsubscribeHostSessionUpdates()
    {
        if (_hostRunner.Server is not null && _hostSessionHandler is not null)
            _hostRunner.Server.SessionChanged -= _hostSessionHandler;

        _hostSessionHandler = null;
    }
}
