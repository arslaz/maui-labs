#if IOS
using AudioToolbox;
using Foundation;
using laba12.Platforms.iOS;
using laba12.Services;
using Microsoft.Maui.ApplicationModel;
using UIKit;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(IosNativeFeedbackService))]
namespace laba12.Platforms.iOS;

/// <summary>
/// Реализация нативного тактильного и звукового отклика для iOS с использованием
/// UINotificationFeedbackGenerator, UIImpactFeedbackGenerator и AudioToolbox.AudioServices.
/// </summary>
public class IosNativeFeedbackService : INativeFeedbackService
{
#pragma warning disable CA1422

    public Task TriggerSuccessFeedbackAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Нативный тактильный Taptic Engine импульс успеха
                using var generator = new UINotificationFeedbackGenerator();
                generator.Prepare();
                generator.NotificationOccurred(UINotificationFeedbackType.Success);

                // Дополнительный системный звуковой сигнал подтверждения (SystemSoundID 1057 / 1103)
                var sound = new SystemSound(1057);
                sound.PlaySystemSound();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IosNativeFeedback] Success error: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }

    public Task TriggerItemDeletedFeedbackAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Жесткий тактильный отклик удаления
                using var generator = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Heavy);
                generator.Prepare();
                generator.ImpactOccurred();

                // Звук удаления / корзины
                var sound = new SystemSound(1055);
                sound.PlaySystemSound();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IosNativeFeedback] Delete error: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }

    public Task TriggerWarningFeedbackAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                using var generator = new UINotificationFeedbackGenerator();
                generator.Prepare();
                generator.NotificationOccurred(UINotificationFeedbackType.Warning);

                var sound = new SystemSound(1053);
                sound.PlaySystemSound();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IosNativeFeedback] Warning error: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }

    public Task TriggerClickFeedbackAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                using var generator = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light);
                generator.Prepare();
                generator.ImpactOccurred();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IosNativeFeedback] Click error: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }

    public Task ShowNativeToastAsync(string message, bool isLong = false)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var window = UIApplication.SharedApplication.ConnectedScenes
                    .OfType<UIWindowScene>()
                    .SelectMany(s => s.Windows)
                    .FirstOrDefault(w => w.IsKeyWindow);

                var rootVc = window?.RootViewController;
                if (rootVc == null) return;

                while (rootVc.PresentedViewController != null)
                {
                    rootVc = rootVc.PresentedViewController;
                }

                // Всплывающее системное уведомление UIAlertController в стиле тоста
                var alert = UIAlertController.Create(null, message, UIAlertControllerStyle.Alert);
                rootVc.PresentViewController(alert, true, () =>
                {
                    int delayMs = isLong ? 2500 : 1500;
                    NSTimer.CreateScheduledTimer(delayMs / 1000.0, false, _ =>
                    {
                        alert.DismissViewController(true, null);
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IosNativeFeedback] ShowNativeToast error: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }
}
#endif
