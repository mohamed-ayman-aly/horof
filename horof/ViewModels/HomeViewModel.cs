using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using horof.Services;

namespace horof.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IGameSessionService _session;

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _roomCode = "";

    [ObservableProperty]
    private string _hostAddress = "";

    [ObservableProperty]
    private string? _statusMessage;

    public HomeViewModel(IGameSessionService session)
    {
        _session = session;
        _session.ErrorOccurred += msg => StatusMessage = msg;
    }

    [RelayCommand]
    private async Task CreateGameAsync()
    {
        StatusMessage = null;
        await _session.CreateRoomAsync(DisplayName);
        if (_session.LocalPlayer is not null)
            await Shell.Current.GoToAsync("lobby");
    }

    [RelayCommand]
    private async Task JoinGameAsync()
    {
        StatusMessage = null;
        await _session.JoinRoomAsync(DisplayName, RoomCode, HostAddress);
        if (_session.LocalPlayer is not null && !_session.LocalPlayer.IsHost)
            await Shell.Current.GoToAsync("lobby");
    }
}
