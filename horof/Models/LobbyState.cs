namespace horof.Models;

public class LobbyState
{
    /// <summary>1 quizmaster Host + up to 4 team players.</summary>
    public const int MaxPlayers = 5;

    public string RoomCode { get; set; } = "";
    public string? HostAddress { get; set; }
    public List<Player> Players { get; set; } = [];

    public bool CanStart
    {
        get
        {
            if (Players.Count == 0 || !Players.All(p => p.IsReady))
                return false;

            if (Players.Count(p => p.IsHost) != 1)
                return false;

            var teamPlayers = Players.Count(p => !p.IsHost && p.Team is Team.Green or Team.Orange);
            return teamPlayers is 2 or 4;
        }
    }
}
