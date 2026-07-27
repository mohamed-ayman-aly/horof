using horof.Services;

namespace horof.Services.Network;

public class GameHostRunner
{
    private readonly LanGameHost _host;

    public GameHostRunner(IQuestionBank questionBank)
    {
        _host = new LanGameHost(questionBank);
    }

    public RoomSessionServer? Server => _host.Server;

    public Task<RoomSessionServer> EnsureStartedAsync(CancellationToken cancellationToken = default) =>
        _host.EnsureStartedAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _host.StopAsync(cancellationToken);
}
