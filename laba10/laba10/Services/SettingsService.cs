namespace laba10.Services;

/// <summary>
/// Сервис для работы с Preferences (глобальные настройки) и SecureStorage (секретные токены преподавателя).
/// </summary>
public class SettingsService : ISettingsService
{
    private const string PrefTargetLevel = "pref_target_level";
    private const string PrefStudentName = "pref_student_name";
    private const string SecureTeacherTokenKey = "sec_teacher_token";
    private const string DefaultTeacherToken = "TEACHER2026";

    // Локальное кэширование на случай отсутствия Keychain прав на Mac Catalyst
    private readonly Dictionary<string, string> _fallbackSecureMemory = new();

    public string TargetLanguageLevel
    {
        get => Preferences.Default.Get(PrefTargetLevel, "Все");
        set => Preferences.Default.Set(PrefTargetLevel, value);
    }

    public string StudentName
    {
        get => Preferences.Default.Get(PrefStudentName, "Александр Смирнов");
        set => Preferences.Default.Set(PrefStudentName, value);
    }

    public bool IsTeacherModeActive { get; set; } = false;

    public async Task<bool> VerifyTeacherTokenAsync(string inputToken)
    {
        if (string.IsNullOrWhiteSpace(inputToken))
            return false;

        string storedToken = await GetSecureValueAsync(SecureTeacherTokenKey) ?? DefaultTeacherToken;
        return string.Equals(inputToken.Trim(), storedToken.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetTeacherTokenAsync(string newToken)
    {
        await SetSecureValueAsync(SecureTeacherTokenKey, newToken);
    }

    public async Task<string?> GetEncryptedMetricAsync(string key)
    {
        return await GetSecureValueAsync(key);
    }

    public async Task SaveEncryptedMetricAsync(string key, string value)
    {
        await SetSecureValueAsync(key, value);
    }

    private async Task<string?> GetSecureValueAsync(string key)
    {
        try
        {
            var value = await SecureStorage.Default.GetAsync(key);
            if (value != null) return value;
        }
        catch
        {
            // Fallback если SecureStorage требует Mac Keychain entitlements
        }

        if (_fallbackSecureMemory.TryGetValue(key, out var val))
            return val;

        // По умолчанию для токена учителя возвращаем DefaultTeacherToken
        if (key == SecureTeacherTokenKey)
            return DefaultTeacherToken;

        return null;
    }

    private async Task SetSecureValueAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            // Fallback если SecureStorage требует Mac Keychain entitlements
        }

        _fallbackSecureMemory[key] = value;
    }
}
