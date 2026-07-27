using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using horof.Models;
using horof.Services;

namespace horof.ViewModels;

public partial class LobbyViewModel : ObservableObject
{
    private readonly IGameSessionService _session;
    private readonly MockGameSessionService? _mock;

    [ObservableProperty]
    private string _roomCode = "";

    [ObservableProperty]
    private string? _hostAddress;

    [ObservableProperty]
    private string _hostIp = "";

    [ObservableProperty]
    private IReadOnlyList<PlayerSlotViewModel> _playerSlots = [];

    [ObservableProperty]
    private bool _isReady;

    [ObservableProperty]
    private bool _isHost;

    [ObservableProperty]
    private bool _canStart;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _copyFeedback;

    [ObservableProperty]
    private string _localPlayerName = "";

    [ObservableProperty]
    private string _localReadyStatusText = "";

    public string ReadyToggleText => IsReady ? "غير جاهز" : "جاهز";

    public Color ReadyToggleColor => IsReady
        ? Color.FromArgb("#C62828")
        : Color.FromArgb("#2E7D32");

    public Color LocalReadyStatusColor => IsReady
        ? Color.FromArgb("#2E7D32")
        : Color.FromArgb("#757575");

    partial void OnIsReadyChanged(bool value)
    {
        UpdateLocalReadyStatusText();
        OnPropertyChanged(nameof(ReadyToggleText));
        OnPropertyChanged(nameof(ReadyToggleColor));
        OnPropertyChanged(nameof(LocalReadyStatusColor));
    }

    private void UpdateLocalReadyStatusText() =>
        LocalReadyStatusText = IsReady ? "أنت جاهز للعب" : "أنت غير جاهز بعد";

    public LobbyViewModel(IGameSessionService session)
    {
        _session = session;
        _mock = session as MockGameSessionService;
        _session.LobbyChanged += Refresh;
        _session.GameChanged += OnGameChanged;
        _session.ErrorOccurred += msg => StatusMessage = msg;
        Refresh();
    }

    private bool _navigatedToGame;

    private void OnGameChanged()
    {
        if (_navigatedToGame || _session.Game is not { Cells.Count: > 0 })
            return;

        _navigatedToGame = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.GoToAsync("game");
        });
    }

    [RelayCommand]
    private async Task ToggleReadyAsync()
    {
        IsReady = !IsReady;
        await _session.SetReadyAsync(IsReady);
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        await _session.StartGameAsync();
        if (_session.Game is not null)
            await Shell.Current.GoToAsync("game");
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        _navigatedToGame = false;
        await _session.LeaveSessionAsync();
        await Shell.Current.GoToAsync("//home");
    }

    [RelayCommand]
    private async Task CopyRoomCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(RoomCode))
            return;

        await Clipboard.Default.SetTextAsync($"Code: {RoomCode.Trim()}");
        CopyFeedback = "تم نسخ رمز الغرفة";
    }

    [RelayCommand]
    private async Task CopyHostIpAsync()
    {
        var ip = HostIp;
        if (string.IsNullOrWhiteSpace(ip) && !string.IsNullOrWhiteSpace(HostAddress))
            ip = HostAddress.Split(':')[0];

        if (string.IsNullOrWhiteSpace(ip))
            return;

        await Clipboard.Default.SetTextAsync($"IP: {ip}");
        CopyFeedback = "تم نسخ عنوان IP";
    }

    [RelayCommand]
    private void AddTestPlayer()
    {
        _mock?.AddSimulatedPlayer($"لاعب {PlayerSlots.Count(p => p.IsOccupied) + 1}");
    }

    private void Refresh()
    {
        var lobby = _session.Lobby;
        RoomCode = lobby.RoomCode;
        HostAddress = lobby.HostAddress;
        HostIp = string.IsNullOrWhiteSpace(lobby.HostAddress)
            ? ""
            : lobby.HostAddress.Split(':')[0];
        IsHost = _session.LocalPlayer?.IsHost ?? false;
        IsReady = _session.LocalPlayer?.IsReady ?? false;
        LocalPlayerName = _session.LocalPlayer?.DisplayName ?? "—";
        UpdateLocalReadyStatusText();
        CanStart = lobby.CanStart && IsHost;

        var localId = _session.LocalPlayer?.Id;
        var slots = new List<PlayerSlotViewModel>(LobbyState.MaxPlayers);
        for (var i = 0; i < LobbyState.MaxPlayers; i++)
        {
            var player = i < lobby.Players.Count ? lobby.Players[i] : null;
            slots.Add(new PlayerSlotViewModel
            {
                Index = i + 1,
                DisplayName = player?.DisplayName ?? "—",
                TeamLabel = player?.Team switch
                {
                    Team.Green => "أخضر",
                    Team.Orange => "برتقالي",
                    _ => ""
                },
                TeamColor = player?.Team switch
                {
                    Team.Green => Color.FromArgb("#2E7D32"),
                    Team.Orange => Color.FromArgb("#F57C00"),
                    _ => Colors.Gray
                },
                IsOccupied = player is not null,
                IsReady = player?.IsReady ?? false,
                IsHost = player?.IsHost ?? false,
                IsLocalPlayer = player?.Id == localId
            });
        }

        PlayerSlots = slots;
    }
}

public class PlayerSlotViewModel
{
    public int Index { get; init; }
    public string DisplayName { get; init; } = "";
    public string TeamLabel { get; init; } = "";
    public Color TeamColor { get; init; } = Colors.Gray;
    public bool IsOccupied { get; init; }
    public bool IsReady { get; init; }
    public bool IsNotReady => IsOccupied && !IsReady;
    public bool IsHost { get; init; }
    public bool IsLocalPlayer { get; init; }

    public double SlotBorderThickness => IsLocalPlayer ? 3 : 2;
}
