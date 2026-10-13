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
        PickerSortField.SelectedIndex = 0;
        PickerSortDirection.SelectedIndex = 0;
        _isInitialized = true;
    }

    private void LoadData()
    {
        string? searchQuery = TxtSearch.Text;
        string sortField = PickerSortField.SelectedItem as string ?? "Дата";
        bool ascending = (PickerSortDirection.SelectedIndex == 1);

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
        PickerSortField.SelectedIndex = 0;
        PickerSortDirection.SelectedIndex = 0;
        LoadData();
    }

    private DateTime _lastTapTime = DateTime.MinValue;
    private int _lastTappedId = -1;

    private void SelectIncident(Incident? incident)
    {
        _selectedIncident = incident;
        foreach (var item in _incidents)
        {
            item.IsSelected = (incident != null && item.Id == incident.Id);
        }
        UpdateButtonsState();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var item = e.CurrentSelection.FirstOrDefault() as Incident;
        if (item != null)
        {
            SelectIncident(item);
        }
    }

    private async void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is Incident incident)
        {
            var now = DateTime.Now;
            bool isDoubleTap = (_lastTappedId == incident.Id && (now - _lastTapTime).TotalMilliseconds < 500);
            _lastTapTime = now;
            _lastTappedId = incident.Id;

            SelectIncident(incident);

            if (isDoubleTap)
            {
                await Navigation.PushAsync(new StudentCardPage(incident.StudentId));
            }
        }
    }

    private void UpdateButtonsState()
    {
        bool hasSelection = _selectedIncident != null;
        BtnDelete.IsEnabled = hasSelection;
        BtnDelete.Opacity = hasSelection ? 1.0 : 0.5;
        BtnOpenCard.IsEnabled = hasSelection;
        BtnOpenCard.Opacity = hasSelection ? 1.0 : 0.5;

        if (hasSelection && _selectedIncident != null)
        {
            LblRecordsCount.Text = $"Выделен: {_selectedIncident.StudentName} (Инцидент №{_selectedIncident.Id}) • Нажмите «Карточка» или дважды кликните по строке";
            LblRecordsCount.TextColor = Color.FromArgb("#38BDF8");
        }
        else
        {
            LblRecordsCount.Text = $"Всего записей: {_incidents.Count} (кликните по строке для выбора, двойной клик открывает карточку)";
            LblRecordsCount.TextColor = Color.FromArgb("#94A3B8");
        }
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
