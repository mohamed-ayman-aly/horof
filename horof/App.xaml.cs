using Microsoft.Extensions.DependencyInjection;

namespace horof;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = MauiProgram.App.Services.GetRequiredService<AppShell>();
        return new Window(shell);
    }
}
