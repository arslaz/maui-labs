namespace laba8;

public partial class SettingsPage : ContentPage
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _loading = true;

        string sort = Preferences.Default.Get("sort", "По автору");
        SortPicker.SelectedItem = sort;
        RatedSwitch.IsToggled = Preferences.Default.Get("only_rated", false);

        _loading = false;
    }

    private void OnSortChanged(object? sender, EventArgs e)
    {
        if (!_loading && SortPicker.SelectedItem != null)
        {
            Preferences.Default.Set("sort", SortPicker.SelectedItem.ToString());
        }
    }

    private void OnRatedChanged(object? sender, ToggledEventArgs e)
    {
        if (!_loading)
        {
            Preferences.Default.Set("only_rated", e.Value);
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        await LibraryService.ExportAsync();
        StatusLabel.Text = $"Экспортировано в:\n{LibraryService.ExportPath}";
        await DisplayAlertAsync("Успех", "Каталог экспортирован в файл", "OK");
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        if (File.Exists(LibraryService.ExportPath))
        {
            await LibraryService.ImportAsync();
            StatusLabel.Text = "Каталог восстановлен из файла";
            await DisplayAlertAsync("Успех", "Каталог восстановлен", "OK");
        }
        else
        {
            await DisplayAlertAsync("Ошибка", "Файл для восстановления не найден", "OK");
        }
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Сброс", "Удалить все данные?", "Да", "Нет");
        if (confirm)
        {
            LibraryService.ResetAll();
            _loading = true;
            SortPicker.SelectedIndex = 0;
            RatedSwitch.IsToggled = false;
            StatusLabel.Text = "Все данные удалены";
            _loading = false;
        }
    }
}
