using horof.Models;

namespace horof.Services.Network;

public record JoinResult(bool Success, string? PlayerId, string? ErrorMessage);

public record SessionSnapshot(
    string RoomCode,
    string HostAddress,
    IReadOnlyList<PlayerDto> Players,
    GameStateDto? Game);

public record PlayerDto(
    string Id,
    string DisplayName,
    Team Team,
    bool IsHost,
    bool IsReady);

public record HexCellDto(int Row, int Col, int Index, char Letter, Team Owner);

public record GameStateDto(
    int MatchSeed,
    GamePhase Phase,
    Team ActiveTeam,
    Team? RoundWinner,
    int? SelectedHexIndex,
    string? BuzzingPlayerId,
    Team? BuzzingPlayerTeam,
    string? CurrentQuestionId,
    string CurrentQuestionText,
    string ExpectedAnswerHint,
    bool SecondChanceForOpponent,
    IReadOnlyList<HexCellDto> Cells);

public static class SessionMapping
{
    public static SessionSnapshot ToSnapshot(LobbyState lobby, GameState? game, string hostAddress) =>
        new(
            lobby.RoomCode,
            hostAddress,
            lobby.Players.Select(ToDto).ToList(),
            game is null || game.Cells.Count == 0 ? null : ToDto(game));

    public static PlayerDto ToDto(Player p) =>
        new(p.Id, p.DisplayName, p.Team, p.IsHost, p.IsReady);

    public static Player FromDto(PlayerDto dto, bool isLocal = false) =>
        new()
        {
            Id = dto.Id,
            DisplayName = dto.DisplayName,
            Team = dto.Team,
            IsHost = dto.IsHost,
            IsReady = dto.IsReady,
            IsLocal = isLocal
        };

    public static GameStateDto ToDto(GameState state) =>
        new(
            state.MatchSeed,
            state.Phase,
            state.ActiveTeam,
            state.RoundWinner,
            state.SelectedHexIndex,
            state.BuzzingPlayerId,
            state.BuzzingPlayerTeam,
            state.CurrentQuestionId,
            state.CurrentQuestionText,
            state.ExpectedAnswerHint,
            state.SecondChanceForOpponent,
            state.Cells.Select(c => new HexCellDto(c.Row, c.Col, c.Index, c.Letter, c.Owner)).ToList());

    public static GameState FromDto(GameStateDto dto) =>
        new()
        {
            MatchSeed = dto.MatchSeed,
            Phase = dto.Phase,
            ActiveTeam = dto.ActiveTeam,
            RoundWinner = dto.RoundWinner,
            SelectedHexIndex = dto.SelectedHexIndex,
            BuzzingPlayerId = dto.BuzzingPlayerId,
            BuzzingPlayerTeam = dto.BuzzingPlayerTeam,
            CurrentQuestionId = dto.CurrentQuestionId,
            CurrentQuestionText = dto.CurrentQuestionText,
            ExpectedAnswerHint = dto.ExpectedAnswerHint,
            SecondChanceForOpponent = dto.SecondChanceForOpponent,
            Cells = dto.Cells.Select(c => new HexCell
            {
                Row = c.Row,
                Col = c.Col,
                Index = c.Index,
                Letter = c.Letter,
                Owner = c.Owner
            }).ToList()
        };
}
