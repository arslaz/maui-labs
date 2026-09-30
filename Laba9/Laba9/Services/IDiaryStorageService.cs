using Laba9.Models;

namespace Laba9.Services;

public interface IDiaryStorageService
{
    string StorageFilePath { get; }
    string BackupFilePath { get; }

    event EventHandler? EntriesChanged;

    Task<List<DiaryEntry>> LoadEntriesAsync();
    Task SaveEntriesAsync(IEnumerable<DiaryEntry> entries);
    Task<string> ExportBackupAsync();
    Task<bool> RestoreBackupAsync();
    Task ResetAllDataAsync();
    Task CreateSampleDataAsync();
}
