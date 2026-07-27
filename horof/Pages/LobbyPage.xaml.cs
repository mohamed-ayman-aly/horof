using horof.ViewModels;

namespace horof.Pages;

public partial class LobbyPage : ContentPage
{
    public LobbyPage(LobbyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
