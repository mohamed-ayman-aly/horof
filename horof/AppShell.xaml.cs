using horof.Pages;

namespace horof;

public partial class AppShell : Shell
{
    public AppShell(HomePage homePage)
    {
        InitializeComponent();

        var home = new ShellContent
        {
            Title = "حروف",
            Route = "home",
            Content = homePage
        };
        Items.Add(home);

        Routing.RegisterRoute("lobby", typeof(LobbyPage));
        Routing.RegisterRoute("game", typeof(GamePage));
    }
}
