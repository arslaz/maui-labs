#if ANDROID
#pragma warning disable CA1416, CA1422
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using laba12.Platforms.Android;
using laba12.Services;
using Microsoft.Maui.ApplicationModel;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(AndroidNativeFeedbackService))]
namespace laba12.Platforms.Android;

/// <summary>
/// Реализация нативного отклика для Android с использованием низкоуровневых API:
/// Android.OS.Vibrator / VibratorManager, VibrationEffect и Android.Widget.Toast.
/// </summary>
public class AndroidNativeFeedbackService : INativeFeedbackService
{
    private readonly Vibrator? _vibrator;

    public AndroidNativeFeedbackService()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                var manager = context.GetSystemService(Context.VibratorManagerService) as VibratorManager;
                _vibrator = manager?.DefaultVibrator;
            }
            else
            {
                _vibrator = context.GetSystemService(Context.VibratorService) as Vibrator;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidNativeFeedback] Init error: {ex.Message}");
        }
    }

    public Task TriggerSuccessFeedbackAsync()
    {
        try
        {
            if (_vibrator?.HasVibrator == true)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    // Паттерн успеха: мягкий двойной тактильный импульс (haptic feedback)
                    long[] timings = { 0, 45, 60, 65 };
                    int[] amplitudes = { 0, 160, 0, 240 };
                    using var effect = VibrationEffect.CreateWaveform(timings, amplitudes, -1);
                    _vibrator.Vibrate(effect);
                }
                else
                {
                    _vibrator.Vibrate(70);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidNativeFeedback] TriggerSuccess error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task TriggerItemDeletedFeedbackAsync()
    {
        try
        {
            if (_vibrator?.HasVibrator == true)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    // Выраженный тяжелый импульс при удалении записи
                    using var effect = VibrationEffect.CreateOneShot(130, VibrationEffect.DefaultAmplitude);
                    _vibrator.Vibrate(effect);
                }
                else
                {
                    _vibrator.Vibrate(130);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidNativeFeedback] TriggerItemDeleted error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task TriggerWarningFeedbackAsync()
    {
        try
        {
            if (_vibrator?.HasVibrator == true)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    // Тройной предупреждающий отклик
                    long[] timings = { 0, 80, 50, 80, 50, 80 };
                    int[] amplitudes = { 0, 200, 0, 200, 0, 255 };
                    using var effect = VibrationEffect.CreateWaveform(timings, amplitudes, -1);
                    _vibrator.Vibrate(effect);
                }
                else
                {
                    _vibrator.Vibrate(200);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidNativeFeedback] TriggerWarning error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task TriggerClickFeedbackAsync()
    {
        try
        {
            if (_vibrator?.HasVibrator == true)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    using var effect = VibrationEffect.CreatePredefined(VibrationEffect.EffectClick);
                    _vibrator.Vibrate(effect);
                }
                else if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    using var effect = VibrationEffect.CreateOneShot(25, 90);
                    _vibrator.Vibrate(effect);
                }
                else
                {
                    _vibrator.Vibrate(25);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidNativeFeedback] TriggerClick error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task ShowNativeToastAsync(string message, bool isLong = false)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                Toast.MakeText(
                    global::Android.App.Application.Context,
                    message,
                    isLong ? ToastLength.Long : ToastLength.Short)?.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidNativeFeedback] ShowToast error: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }
}
#endif
