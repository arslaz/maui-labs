using SUBD33.Data;
using SUBD33.Models;

namespace SUBD33.Views;

public partial class IncidentsPage : ContentPage
{
    private List<Incident> _incidents = new();
    private Incident? _selectedIncident;
    private bool _isInitialized = false;

    public IncidentsPage()
    {
        InitializeComponent();
        SetupControls();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadData();
    }

    private void SetupControls()
    {
        PickerSortField.ItemsSource = new List<string>
        {
            "Дата",
            "Уровень паники",
            "Студент",
            "Триггер"
        };
        PickerSortField.SelectedIndex = 0; // Дата
        PickerSortDirection.SelectedIndex = 0; // По убыванию
        _isInitialized = true;
    }

    private void LoadData()
    {
        string? searchQuery = TxtSearch.Text;
        string sortField = PickerSortField.SelectedItem as string ?? "Дата";
        bool ascending = (PickerSortDirection.SelectedIndex == 1); // 1 = По возрастанию

        _incidents = DatabaseHelper.Instance.GetIncidents(searchQuery, sortField, ascending);
        IncidentsCollection.ItemsSource = null;
        IncidentsCollection.ItemsSource = _incidents;

        LblRecordsCount.Text = $"Всего записей: {_incidents.Count}";
        _selectedIncident = null;
        UpdateButtonsState();
    }

    private void OnSearchClicked(object? sender, EventArgs e)
    {
        LoadData();
    }

    private void OnSortParameterChanged(object? sender, EventArgs e)
    {
        if (_isInitialized)
        {
            LoadData();
        }
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        TxtSearch.Text = string.Empty;
        PickerSortField.SelectedIndex = 0; // Дата
        PickerSortDirection.SelectedIndex = 0; // По убыванию
        LoadData();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedIncident = e.CurrentSelection.FirstOrDefault() as Incident;
        UpdateButtonsState();
    }

    private void UpdateButtonsState()
    {
        bool hasSelection = _selectedIncident != null;
        BtnDelete.Opacity = hasSelection ? 1.0 : 0.6;
        BtnOpenCard.Opacity = hasSelection ? 1.0 : 0.6;
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_selectedIncident == null)
        {
            await DisplayAlertAsync("Внимание", "Пожалуйста, выберите строку для удаления.", "OK");
            return;
        }

        bool confirm = await DisplayAlertAsync(
            "Подтверждение удаления", 
            $"Вы действительно хотите удалить инцидент №{_selectedIncident.Id} студента {_selectedIncident.StudentName}?", 
            "Удалить", 
            "Отмена"
        );

        if (confirm)
        {
            bool deleted = DatabaseHelper.Instance.DeleteIncident(_selectedIncident.Id);
            if (deleted)
            {
                await DisplayAlertAsync("Успешно", "Запись успешно удалена из базы данных.", "OK");
                LoadData();
            }
            else
            {
                await DisplayAlertAsync("Ошибка", "Не удалось удалить запись из базы данных.", "OK");
            }
        }
    }

    // Задание 4: Открытие карточки студента по двойному клику
    private async void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is Incident incident)
        {
            await Navigation.PushAsync(new StudentCardPage(incident.StudentId));
        }
    }

    private async void OnOpenCardClicked(object? sender, EventArgs e)
    {
        if (_selectedIncident == null)
        {
            await DisplayAlertAsync("Внимание", "Пожалуйста, выделите строку с инцидентом студента.", "OK");
            return;
        }

        await Navigation.PushAsync(new StudentCardPage(_selectedIncident.StudentId));
    }

    private async void OnAddIncidentClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new AddIncidentPage());
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
