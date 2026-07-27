using horof.Models;

namespace horof.Services;

public class GameEngine
{
    private readonly IQuestionBank _questionBank;

    public GameEngine(IQuestionBank questionBank)
    {
        _questionBank = questionBank;
    }

    public GameState State { get; } = new();

    public void StartMatch(int seed)
    {
        State.ResetForNewRound(seed);
    }

    public bool TrySelectHex(int hexIndex, Team actingTeam)
    {
        if (State.Phase != GamePhase.PickHex || actingTeam != State.ActiveTeam)
            return false;

        if (hexIndex < 0 || hexIndex >= State.Cells.Count)
            return false;

        var cell = State.Cells[hexIndex];
        if (cell.Owner != Team.None)
            return false;

        State.SelectedHexIndex = hexIndex;
        var question = _questionBank.GetQuestion(cell.Letter);
        State.CurrentQuestionId = question.Id;
        State.CurrentQuestionText = question.Text;
        State.ExpectedAnswerHint = question.AnswerHint;
        State.BuzzingPlayerId = null;
        State.BuzzingPlayerTeam = null;
        State.SecondChanceForOpponent = false;
        State.Phase = GamePhase.BuzzOpen;
        return true;
    }

    public bool TryBuzz(string playerId, Team playerTeam)
    {
        if (State.Phase is not GamePhase.BuzzOpen and not GamePhase.SecondChance)
            return false;

        if (State.Phase == GamePhase.SecondChance)
        {
            var opponent = Opponent(State.ActiveTeam);
            if (playerTeam != opponent)
                return false;
        }

        if (State.BuzzingPlayerId is not null)
            return false;

        State.BuzzingPlayerId = playerId;
        State.BuzzingPlayerTeam = playerTeam;
        State.Phase = GamePhase.Answering;
        return true;
    }

    public void HostJudge(bool correct)
    {
        if (State.Phase != GamePhase.Answering || State.SelectedHexIndex is not int hexIndex)
            return;

        if (correct)
        {
            ClaimHex(hexIndex, State.BuzzingPlayerTeam ?? State.ActiveTeam);
            return;
        }

        if (!State.SecondChanceForOpponent && State.Phase == GamePhase.Answering)
        {
            State.SecondChanceForOpponent = true;
            State.BuzzingPlayerId = null;
            State.BuzzingPlayerTeam = null;
            State.Phase = GamePhase.SecondChance;
            return;
        }

        ReplaceQuestionAndReopenBuzz(hexIndex);
    }

    private void ClaimHex(int hexIndex, Team team)
    {
        State.Cells[hexIndex].Owner = team;
        State.ActiveTeam = team;

        if (HexPathChecker.HasWinningPath(State.Cells, team))
        {
            State.RoundWinner = team;
            State.Phase = GamePhase.RoundEnded;
            return;
        }

        ClearSelectionAndReturnToPick();
    }

    private void ReplaceQuestionAndReopenBuzz(int hexIndex)
    {
        var letter = State.Cells[hexIndex].Letter;
        var question = _questionBank.GetQuestion(letter);
        State.CurrentQuestionId = question.Id;
        State.CurrentQuestionText = question.Text;
        State.ExpectedAnswerHint = question.AnswerHint;
        State.BuzzingPlayerId = null;
        State.BuzzingPlayerTeam = null;
        State.SecondChanceForOpponent = false;
        State.Phase = GamePhase.BuzzOpen;
    }

    private void ClearSelectionAndReturnToPick()
    {
        State.SelectedHexIndex = null;
        State.CurrentQuestionId = null;
        State.CurrentQuestionText = "";
        State.ExpectedAnswerHint = "";
        State.BuzzingPlayerId = null;
        State.BuzzingPlayerTeam = null;
        State.SecondChanceForOpponent = false;
        State.Phase = GamePhase.PickHex;
    }

    private static Team Opponent(Team team) =>
        team == Team.Green ? Team.Orange : Team.Green;
}
