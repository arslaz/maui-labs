using laba12.ViewModels;

namespace laba12.Views;

public partial class RouteMapPage : ContentPage
{
    private readonly RouteMapViewModel _viewModel;

    public RouteMapPage(RouteMapViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeMapAsync();
    }
}
