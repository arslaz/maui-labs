namespace laba10.Services;

public interface ISettingsService
{
    string TargetLanguageLevel { get; set; }
    string StudentName { get; set; }
    bool IsTeacherModeActive { get; set; }

    Task<bool> VerifyTeacherTokenAsync(string inputToken);
    Task SetTeacherTokenAsync(string newToken);
    Task<string?> GetEncryptedMetricAsync(string key);
    Task SaveEncryptedMetricAsync(string key, string value);
}
