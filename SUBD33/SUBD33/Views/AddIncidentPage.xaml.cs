using SUBD33.Data;
using SUBD33.Models;

namespace SUBD33.Views;

public partial class AddIncidentPage : ContentPage
{
    private List<Student> _students = new();
    private List<TriggerItem> _triggers = new();

    public AddIncidentPage()
    {
        InitializeComponent();
        LoadPickersData();
    }

    private void LoadPickersData()
    {
        _students = DatabaseHelper.Instance.GetStudents();
        PickerStudents.ItemsSource = _students;
        if (_students.Count > 0)
        {
            PickerStudents.SelectedIndex = 0;
        }

        _triggers = DatabaseHelper.Instance.GetTriggers();
        PickerTriggers.ItemsSource = _triggers;
        if (_triggers.Count > 0)
        {
            PickerTriggers.SelectedIndex = 0;
        }

        DatePickerIncident.Date = DateTime.Now;
        TimePickerIncident.Time = DateTime.Now.TimeOfDay;
    }

    private void OnPanicSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        int level = (int)Math.Round(e.NewValue);
        LblPanicValue.Text = $"Уровень паники (1-10): {level}";
    }

    private void OnHelpedToggled(object? sender, ToggledEventArgs e)
    {
        LblHelpedText.Text = e.Value ? "Способ помог (Да):" : "Способ помог (Нет):";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (PickerStudents.SelectedItem is not Student selectedStudent)
        {
            await DisplayAlertAsync("Внимание", "Пожалуйста, выберите студента.", "OK");
            return;
        }

        if (PickerTriggers.SelectedItem is not TriggerItem selectedTrigger)
        {
            await DisplayAlertAsync("Внимание", "Пожалуйста, выберите триггер.", "OK");
            return;
        }

        string calmingMethod = TxtCalmingMethod.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(calmingMethod))
        {
            await DisplayAlertAsync("Внимание", "Укажите способ успокоения.", "OK");
            return;
        }

        DateTime baseDate = DatePickerIncident.Date ?? DateTime.Today;
        TimeSpan time = TimePickerIncident.Time ?? DateTime.Now.TimeOfDay;
        DateTime fullDate = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, time.Hours, time.Minutes, 0);
        int panicLevel = (int)Math.Round(SliderPanic.Value);
        bool helped = SwitchHelped.IsToggled;

        try
        {
            bool success = DatabaseHelper.Instance.AddIncident(
                selectedStudent.Id,
                fullDate,
                selectedTrigger.Id,
                panicLevel,
                calmingMethod,
                helped
            );

            if (success)
            {
                await DisplayAlertAsync("Успешно", "Запись успешно сохранена в базе данных!", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlertAsync("Ошибка", "Не удалось сохранить запись.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка БД", $"Ошибка сохранения: {ex.Message}", "OK");
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
