#if MACCATALYST
using AudioToolbox;
using laba12.Platforms.MacCatalyst;
using laba12.Services;
using Microsoft.Maui.ApplicationModel;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(MacCatalystNativeFeedbackService))]
namespace laba12.Platforms.MacCatalyst;

public class MacCatalystNativeFeedbackService : INativeFeedbackService
{
    public Task TriggerSuccessFeedbackAsync()
    {
        try
        {
            var sound = new SystemSound(1057);
            sound.PlayAlertSound();
        }
        catch { }
        return Task.CompletedTask;
    }

    public Task TriggerItemDeletedFeedbackAsync()
    {
        try
        {
            var sound = new SystemSound(1055);
            sound.PlayAlertSound();
        }
        catch { }
        return Task.CompletedTask;
    }

    public Task TriggerWarningFeedbackAsync()
    {
        try
        {
            var sound = new SystemSound(1053);
            sound.PlayAlertSound();
        }
        catch { }
        return Task.CompletedTask;
    }

    public Task TriggerClickFeedbackAsync()
    {
        try
        {
            var sound = new SystemSound(1104);
            sound.PlaySystemSound();
        }
        catch { }
        return Task.CompletedTask;
    }

    public Task ShowNativeToastAsync(string message, bool isLong = false)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Shell.Current?.CurrentPage != null)
                {
                    await Shell.Current.CurrentPage.DisplayAlertAsync("Уведомление", message, "OK");
                }
            }
            catch { }
        });

        return Task.CompletedTask;
    }
}
#endif
