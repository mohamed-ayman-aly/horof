using Microsoft.AspNetCore.SignalR;

namespace horof.Services.Network;

public class LobbyHub : Hub
{
    private readonly RoomSessionServer _server;

    public LobbyHub(RoomSessionServer server)
    {
        _server = server;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _server.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task<JoinResult> JoinRoom(string roomCode, string displayName)
    {
        var result = _server.TryJoin(Context.ConnectionId, roomCode, displayName);
        if (result.Success && !string.IsNullOrEmpty(_server.Lobby.RoomCode))
            await Groups.AddToGroupAsync(Context.ConnectionId, _server.Lobby.RoomCode);

        return result;
    }

    public Task<bool> SetReady(string playerId, bool ready) =>
        Task.FromResult(_server.SetReady(playerId, ready));

    public Task<bool> StartGame(string playerId) =>
        Task.FromResult(_server.StartGame(playerId));

    public Task<bool> SelectHex(string playerId, int hexIndex) =>
        Task.FromResult(_server.SelectHex(playerId, hexIndex));

    public Task<bool> Buzz(string playerId) =>
        Task.FromResult(_server.Buzz(playerId));

    public Task<bool> HostJudge(string playerId, bool correct) =>
        Task.FromResult(_server.HostJudge(playerId, correct));

    public SessionSnapshot GetSnapshot() => _server.GetSnapshot();
}
