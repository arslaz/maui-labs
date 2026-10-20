namespace laba12.Services;

/// <summary>
/// Общий интерфейс для вызова нативного платформенного отклика (вибрация, тактильные паттерны, звуки, всплывающие тосты).
/// Реализуется отдельно для Android (Android.OS.Vibrator, Toast) и для iOS (UIKit UINotificationFeedbackGenerator, AudioToolbox).
/// </summary>
public interface INativeFeedbackService
{
    /// <summary>
    /// Тактильный/звуковой отклик об успешной операции (успешный вход водителя, сохранение статуса, синхронизация)
    /// </summary>
    Task TriggerSuccessFeedbackAsync();

    /// <summary>
    /// Тактильный отклик при удалении наряда на доставку (выраженный вибро-импульс)
    /// </summary>
    Task TriggerItemDeletedFeedbackAsync();

    /// <summary>
    /// Предупреждающий тактильный отклик (ошибка авторизации, отказ от груза, сбой сети)
    /// </summary>
    Task TriggerWarningFeedbackAsync();

    /// <summary>
    /// Мягкий тактильный клик при нажатии навигационных кнопок управления
    /// </summary>
    Task TriggerClickFeedbackAsync();

    /// <summary>
    /// Вызов нативного платформенного уведомления/тоста
    /// </summary>
    Task ShowNativeToastAsync(string message, bool isLong = false);
}
