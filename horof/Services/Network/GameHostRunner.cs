using horof.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace horof.Services.Network;

public class GameHostRunner
{
    private readonly IQuestionBank _questionBank;
    private WebApplication? _app;
    private Task? _runTask;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    public GameHostRunner(IQuestionBank questionBank)
    {
        _questionBank = questionBank;
    }

    public RoomSessionServer? Server { get; private set; }

    public async Task<RoomSessionServer> EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (Server is not null)
                return Server;

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://0.0.0.0:{NetworkHelper.DefaultPort}");
            builder.Services.AddSingleton(_questionBank);
            builder.Services.AddSingleton<RoomSessionServer>();
            builder.Services.AddSignalR();

            var app = builder.Build();
            app.MapHub<LobbyHub>("/gamehub");

            Server = app.Services.GetRequiredService<RoomSessionServer>();
            var hubContext = app.Services.GetRequiredService<IHubContext<LobbyHub>>();
            Server.SetHubContext(hubContext);

            var ip = NetworkHelper.GetLocalIPv4() ?? "127.0.0.1";
            Server.SetHostAddress($"{ip}:{NetworkHelper.DefaultPort}");

            _app = app;
            _runTask = app.RunAsync();
            await Task.Delay(200, cancellationToken);

            return Server;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
            return;

        await _app.StopAsync(cancellationToken);
        if (_runTask is not null)
            await _runTask;

        _app = null;
        _runTask = null;
        Server = null;
    }
}
