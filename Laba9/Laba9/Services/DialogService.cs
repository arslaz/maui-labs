namespace Laba9.Services;

public static class DialogService
{
    public static async Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        if (Shell.Current != null)
        {
            await Shell.Current.DisplayAlertAsync(title, message, cancel);
        }
    }

    public static async Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel = "Отмена")
    {
        if (Shell.Current != null)
        {
            return await Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
        }
        return false;
    }
}
