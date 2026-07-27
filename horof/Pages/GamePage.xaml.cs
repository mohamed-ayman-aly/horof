using horof.Controls;
using horof.ViewModels;

namespace horof.Pages;

public partial class GamePage : ContentPage
{
    private readonly GameViewModel _viewModel;

    public GamePage(GameViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        HexBoard.HexSelected += OnHexSelected;
    }

    private async void OnHexSelected(object? sender, int index)
    {
        if (_viewModel.CanSelectHex)
            await _viewModel.SelectHexCommand.ExecuteAsync(index);
    }
}
