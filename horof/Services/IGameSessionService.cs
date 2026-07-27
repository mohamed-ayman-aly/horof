using horof.Models;

namespace horof.Services;

public interface IGameSessionService
{
    LobbyState Lobby { get; }
    GameState? Game { get; }
    Player? LocalPlayer { get; }

    event Action? LobbyChanged;
    event Action? GameChanged;
    event Action<string>? ErrorOccurred;

    Task CreateRoomAsync(string displayName);
    Task JoinRoomAsync(string displayName, string roomCode, string hostAddress);
    Task SetReadyAsync(bool ready);
    Task StartGameAsync();
    Task SelectHexAsync(int hexIndex);
    Task BuzzAsync();
    Task HostJudgeAsync(bool correct);
    Task LeaveSessionAsync();
}
