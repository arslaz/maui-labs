using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace laba12.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NetworkStatusBadgeText))]
    [NotifyPropertyChangedFor(nameof(NetworkStatusColor))]
    [NotifyPropertyChangedFor(nameof(NetworkBadgeBgColor))]
    private bool isOffline;

    public string NetworkStatusBadgeText => IsOffline ? "ОФЛАЙН" : "ОНЛАЙН";
    public Color NetworkStatusColor => IsOffline ? Color.FromArgb("#EF4444") : Color.FromArgb("#10B981");
    public Color NetworkBadgeBgColor => IsOffline ? Color.FromArgb("#450A0A") : Color.FromArgb("#064E3B");

    [ObservableProperty]
    private string? offlineMessage;

    [ObservableProperty]
    private bool canCancel;

    protected CancellationTokenSource? ActiveCts;

    [RelayCommand]
    protected virtual void CancelCurrentOperation()
    {
        if (ActiveCts != null && !ActiveCts.IsCancellationRequested)
        {
            try
            {
                ActiveCts.Cancel();
                ErrorMessage = "Операция отменена пользователем.";
            }
            catch { }
            finally
            {
                IsBusy = false;
                CanCancel = false;
            }
        }
    }

    protected CancellationToken CreateLinkedToken(TimeSpan? timeout = null)
    {
        ActiveCts?.Dispose();
        ActiveCts = timeout.HasValue 
            ? new CancellationTokenSource(timeout.Value) 
            : new CancellationTokenSource();
        CanCancel = true;
        return ActiveCts.Token;
    }

    protected void ResetCancellation()
    {
        ActiveCts?.Dispose();
        ActiveCts = null;
        CanCancel = false;
    }
}
