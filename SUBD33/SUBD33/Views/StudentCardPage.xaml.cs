using SUBD33.Data;
using SUBD33.Helpers;
using SUBD33.Models;

namespace SUBD33.Views;

[QueryProperty(nameof(StudentId), "studentId")]
public partial class StudentCardPage : ContentPage
{
    private int _studentId;
    public int StudentId
    {
        get => _studentId;
        set
        {
            _studentId = value;
            LoadStudentData(_studentId);
        }
    }

    public StudentCardPage()
    {
        InitializeComponent();
    }

    public StudentCardPage(int studentId) : this()
    {
        StudentId = studentId;
    }

    private void LoadStudentData(int studentId)
    {
        var card = DatabaseHelper.Instance.GetStudentCard(studentId);
        if (card == null) return;

        LblFullName.Text = card.FullName;
        LblGroup.Text = $"Группа: {card.GroupName}";
        LblCourse.Text = $"Курс: {card.Course}";
        LblIncidentCount.Text = $"Количество инцидентов: {card.IncidentCount}";
        LblAvgPanic.Text = $"Средний уровень паники: {card.AveragePanicLevel:F1}";
        LblTopTrigger.Text = $"Самый частый триггер: {card.MostFrequentTrigger}";

        // Загрузка фото студента (Zoom/AspectFit)
        ImgPhoto.Source = ImageHelper.GetImageSource(card.Photo);

        // Список инцидентов студента
        var incidents = DatabaseHelper.Instance.GetStudentIncidents(studentId);
        StudentIncidentsList.ItemsSource = incidents;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
