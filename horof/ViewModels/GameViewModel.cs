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
    private string _localPlayerName = "";

    [ObservableProperty]
    private string _localTeamLabel = "";

    [ObservableProperty]
    private Color _localTeamColor = Colors.Gray;

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
    private bool _showBuzz;

    [ObservableProperty]
    private bool _showHostJudge;

    [ObservableProperty]
    private bool _showRoundEnd;

    [ObservableProperty]
    private string _roundWinnerText = "";

    private bool _navigatedToLobby;
    private bool _handledRoundEnd;

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
        if (state is null || state.Cells.Count == 0)
        {
            Cells = [];
            if (_handledRoundEnd)
                NavigateToLobby();
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

        var local = _session.LocalPlayer;
        var isHost = local?.IsHost ?? false;
        LocalPlayerName = local?.DisplayName ?? "—";
        if (isHost)
        {
            LocalTeamLabel = "مضيف";
            LocalTeamColor = Color.FromArgb("#455A64");
        }
        else if (local?.Team == Team.Green)
        {
            LocalTeamLabel = "أخضر";
            LocalTeamColor = Color.FromArgb("#2E7D32");
        }
        else if (local?.Team == Team.Orange)
        {
            LocalTeamLabel = "برتقالي";
            LocalTeamColor = Color.FromArgb("#F57C00");
        }
        else
        {
            LocalTeamLabel = "";
            LocalTeamColor = Colors.Gray;
        }

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
        var questionPhase = state.Phase is GamePhase.BuzzOpen or GamePhase.Answering or GamePhase.SecondChance;
        ShowQuestion = isHost && questionPhase;
        QuestionText = isHost ? state.CurrentQuestionText : "";
        AnswerHint = isHost && !string.IsNullOrEmpty(state.ExpectedAnswerHint)
            ? $"الإجابة: {state.ExpectedAnswerHint}"
            : "";

        var localTeam = _session.LocalPlayer?.Team ?? Team.None;
        CanSelectHex = !isHost
                       && localTeam != Team.None
                       && state.Phase == GamePhase.PickHex
                       && localTeam == state.ActiveTeam;
        CanBuzz = !isHost
                  && localTeam != Team.None
                  && state.Phase is GamePhase.BuzzOpen or GamePhase.SecondChance
                  && (state.Phase != GamePhase.SecondChance || localTeam != state.ActiveTeam);
        ShowBuzz = !isHost;

        ShowHostJudge = state.Phase == GamePhase.Answering && isHost;
        ShowRoundEnd = state.Phase == GamePhase.RoundEnded;
        RoundWinnerText = state.RoundWinner switch
        {
            Team.Green => "فاز الفريق الأخضر!",
            Team.Orange => "فاز الفريق البرتقالي!",
            _ => ""
        };

        if (state.Phase == GamePhase.RoundEnded && !_handledRoundEnd)
        {
            _handledRoundEnd = true;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(2500);
                NavigateToLobby();
            });
        }
    }

    private void NavigateToLobby()
    {
        if (_navigatedToLobby)
            return;

        _navigatedToLobby = true;
        _session.GameChanged -= Refresh;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch
            {
                await Shell.Current.GoToAsync("//home");
            }
        });
    }
}
