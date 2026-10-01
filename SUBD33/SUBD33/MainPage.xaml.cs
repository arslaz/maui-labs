using SUBD33.Data;
using SUBD33.Views;

namespace SUBD33;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LblDbLocation.Text = $"База данных: {DatabaseHelper.Instance.DatabaseLocation}";
    }

    private async void OnNavIncidentsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new IncidentsPage());
    }

    private async void OnNavAddIncidentClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new AddIncidentPage());
    }

    private async void OnNavStudentsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new StudentsPage());
    }

    private async void OnNavQueriesClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new QueriesPage());
    }
}