using laba10.ViewModels;

namespace laba10.Views;

public partial class SettingsAndStatsPage : ContentPage
{
    private readonly SettingsAndStatsViewModel _viewModel;

    public SettingsAndStatsPage(SettingsAndStatsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
