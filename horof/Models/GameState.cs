namespace horof.Models;

public class GameState
{
    public int MatchSeed { get; set; }
    public GamePhase Phase { get; set; } = GamePhase.PickHex;
    public Team ActiveTeam { get; set; } = Team.Orange;
    public Team? RoundWinner { get; set; }
    public int? SelectedHexIndex { get; set; }
    public string? BuzzingPlayerId { get; set; }
    public Team? BuzzingPlayerTeam { get; set; }
    public string? CurrentQuestionId { get; set; }
    public string CurrentQuestionText { get; set; } = "";
    public string ExpectedAnswerHint { get; set; } = "";
    public bool SecondChanceForOpponent { get; set; }
    public List<HexCell> Cells { get; set; } = [];

    public void ResetForNewRound(int seed)
    {
        MatchSeed = seed;
        Phase = GamePhase.PickHex;
        ActiveTeam = Team.Orange;
        RoundWinner = null;
        SelectedHexIndex = null;
        BuzzingPlayerId = null;
        BuzzingPlayerTeam = null;
        CurrentQuestionId = null;
        CurrentQuestionText = "";
        ExpectedAnswerHint = "";
        SecondChanceForOpponent = false;
        Cells = BoardLayout.CreateCells(seed).Select(c => new HexCell
        {
            Row = c.Row,
            Col = c.Col,
            Index = c.Index,
            Letter = c.Letter,
            Owner = Team.None
        }).ToList();
    }
}
