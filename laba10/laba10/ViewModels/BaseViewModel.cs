using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace laba10.ViewModels;

/// <summary>
/// Базовый класс для всех ViewModel с реализацией INotifyPropertyChanged
/// и вспомогательными методами для привязки данных без написания лишнего кода.
/// </summary>
public abstract class BaseViewModel : INotifyPropertyChanged
{
    private bool _isBusy;
    private string _title = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected async Task DisplayAlertAsync(string title, string message, string cancel = "OK")
    {
        if (Shell.Current != null)
        {
            await Shell.Current.DisplayAlertAsync(title, message, cancel);
        }
    }

    protected async Task<bool> DisplayConfirmationAsync(string title, string message, string accept = "Да", string cancel = "Отмена")
    {
        if (Shell.Current != null)
        {
            return await Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
        }
        return false;
    }

    protected async Task<string> DisplayActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
    {
        if (Shell.Current != null)
        {
            return await Shell.Current.DisplayActionSheetAsync(title, cancel, destruction, buttons);
        }
        return cancel;
    }
}
