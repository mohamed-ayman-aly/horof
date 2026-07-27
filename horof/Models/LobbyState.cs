namespace horof.Models;

public class LobbyState
{
    public const int MaxPlayers = 4;

    public string RoomCode { get; set; } = "";
    public string? HostAddress { get; set; }
    public List<Player> Players { get; set; } = [];

    public bool CanStart =>
        Players.Count >= 2 &&
        Players.All(p => p.IsReady) &&
        Players.Any(p => p.IsHost);
}
