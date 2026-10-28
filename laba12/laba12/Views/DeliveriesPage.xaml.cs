using laba12.ViewModels;

namespace laba12.Views;

public partial class DeliveriesPage : ContentPage
{
    private readonly DeliveriesViewModel _viewModel;

    public DeliveriesPage(DeliveriesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesPage] OnAppearing exception: {ex}");
        }
    }
}
