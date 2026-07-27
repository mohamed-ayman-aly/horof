using horof.Pages;
using horof.Services;
using horof.Services.Network;
using horof.ViewModels;
using Microsoft.Extensions.Logging;

namespace horof;

public static class MauiProgram
{
    public static MauiApp App { get; private set; } = null!;

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IQuestionBank, JsonQuestionBank>();
        builder.Services.AddSingleton<GameEngine>();
        builder.Services.AddSingleton<GameHostRunner>();
        builder.Services.AddSingleton<IGameSessionService, NetworkGameSessionService>();

        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<LobbyViewModel>();
        builder.Services.AddTransient<GameViewModel>();

        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<LobbyPage>();
        builder.Services.AddTransient<GamePage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        App = builder.Build();
        return App;
    }
}
