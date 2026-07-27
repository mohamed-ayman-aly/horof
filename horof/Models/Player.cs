namespace horof.Models;

public class Player
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "";
    public Team Team { get; set; }
    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
    public bool IsLocal { get; set; }
}
