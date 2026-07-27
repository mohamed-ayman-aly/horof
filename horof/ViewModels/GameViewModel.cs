using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using horof.Models;
using horof.Services;

namespace horof.ViewModels;

public partial class GameViewModel : ObservableObject
{
    private readonly IGameSessionService _session;

    [ObservableProperty]
    private IReadOnlyList<HexCell> _cells = [];

    [ObservableProperty]
    private int? _selectedHexIndex;

    [ObservableProperty]
    private string _phaseText = "";

    [ObservableProperty]
    private string _activeTeamText = "";

    [ObservableProperty]
    private string _questionText = "";

    [ObservableProperty]
    private string _answerHint = "";

    [ObservableProperty]
    private bool _showQuestion;

    [ObservableProperty]
    private bool _canSelectHex;

    [ObservableProperty]
    private bool _canBuzz;

    [ObservableProperty]
    private bool _showHostJudge;

    [ObservableProperty]
    private bool _showRoundEnd;

    [ObservableProperty]
    private string _roundWinnerText = "";

    public GameViewModel(IGameSessionService session)
    {
        _session = session;
        _session.GameChanged += Refresh;
        Refresh();
    }

    [RelayCommand]
    private async Task SelectHexAsync(int index)
    {
        await _session.SelectHexAsync(index);
    }

    [RelayCommand]
    private async Task BuzzAsync()
    {
        await _session.BuzzAsync();
    }

    [RelayCommand]
    private async Task JudgeCorrectAsync()
    {
        await _session.HostJudgeAsync(true);
    }

    [RelayCommand]
    private async Task JudgeWrongAsync()
    {
        await _session.HostJudgeAsync(false);
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        await _session.LeaveSessionAsync();
        await Shell.Current.GoToAsync("//home");
    }

    private void Refresh()
    {
        var state = _session.Game;
        if (state is null)
        {
            Cells = [];
            return;
        }

        Cells = state.Cells.Select(c => new HexCell
        {
            Row = c.Row,
            Col = c.Col,
            Index = c.Index,
            Letter = c.Letter,
            Owner = c.Owner
        }).ToList();
        SelectedHexIndex = state.SelectedHexIndex;

        ActiveTeamText = state.ActiveTeam == Team.Green ? "دور الفريق الأخضر" : "دور الفريق البرتقالي";
        PhaseText = state.Phase switch
        {
            GamePhase.PickHex => "اختر حرفاً من اللوحة",
            GamePhase.BuzzOpen => "اضغط الجرس!",
            GamePhase.Answering => "جاري الإجابة…",
            GamePhase.SecondChance => "فرصة للفريق الآخر",
            GamePhase.RoundEnded => "انتهت الجولة",
            _ => ""
        };

        ShowQuestion = state.Phase is GamePhase.BuzzOpen or GamePhase.Answering or GamePhase.SecondChance;
        QuestionText = state.CurrentQuestionText;
        AnswerHint = string.IsNullOrEmpty(state.ExpectedAnswerHint)
            ? ""
            : $"تلميح للمضيف: {state.ExpectedAnswerHint}";

        var localTeam = _session.LocalPlayer?.Team ?? Team.None;
        CanSelectHex = state.Phase == GamePhase.PickHex && localTeam == state.ActiveTeam;
        CanBuzz = state.Phase is GamePhase.BuzzOpen or GamePhase.SecondChance
                  && (state.Phase != GamePhase.SecondChance || localTeam != state.ActiveTeam);

        ShowHostJudge = state.Phase == GamePhase.Answering && (_session.LocalPlayer?.IsHost ?? false);
        ShowRoundEnd = state.Phase == GamePhase.RoundEnded;
        RoundWinnerText = state.RoundWinner switch
        {
            Team.Green => "فاز الفريق الأخضر!",
            Team.Orange => "فاز الفريق البرتقالي!",
            _ => ""
        };
    }
}
