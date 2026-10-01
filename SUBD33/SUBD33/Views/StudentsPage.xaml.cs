using SUBD33.Data;
using SUBD33.Models;

namespace SUBD33.Views;

public partial class StudentsPage : ContentPage
{
    public StudentsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadStudents();
    }

    private void LoadStudents()
    {
        var students = DatabaseHelper.Instance.GetStudents();
        StudentsCollection.ItemsSource = students;
    }

    private async void OnAddStudentClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new AddStudentPage());
    }

    private async void OnOpenStudentCardClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is int studentId)
        {
            await Navigation.PushAsync(new StudentCardPage(studentId));
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
