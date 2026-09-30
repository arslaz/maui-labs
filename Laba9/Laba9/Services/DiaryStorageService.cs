using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Laba9.Models;

namespace Laba9.Services;

public class DiaryStorageService : IDiaryStorageService
{
    private const string SecureKeyName = "diary_encryption_master_key";
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string StorageFilePath => Path.Combine(FileSystem.AppDataDirectory, "diary_entries.json");
    public string BackupFilePath => Path.Combine(FileSystem.AppDataDirectory, "diary_backup.json");

    public event EventHandler? EntriesChanged;

    public async Task<List<DiaryEntry>> LoadEntriesAsync()
    {
        if (!File.Exists(StorageFilePath))
        {
            await CreateSampleDataAsync();
        }

        try
        {
            var json = await File.ReadAllTextAsync(StorageFilePath, Encoding.UTF8);
            var entries = JsonSerializer.Deserialize<List<DiaryEntry>>(json, _jsonOptions) ?? new List<DiaryEntry>();

            var key = await GetOrCreateEncryptionKeyAsync();

            foreach (var entry in entries)
            {
                if (entry.IsConfidential && entry.IsEncrypted && !string.IsNullOrEmpty(entry.Content))
                {
                    entry.Content = DecryptText(entry.Content, key);
                }
            }

            return entries;
        }
        catch (JsonException)
        {
            // Защита от поврежденного файла: сохраняем резервную копию и создаем чистый список
            var corruptBackup = StorageFilePath + ".corrupted";
            if (File.Exists(StorageFilePath))
            {
                File.Copy(StorageFilePath, corruptBackup, true);
            }
            return new List<DiaryEntry>();
        }
        catch (Exception)
        {
            return new List<DiaryEntry>();
        }
    }

    public async Task SaveEntriesAsync(IEnumerable<DiaryEntry> entries)
    {
        var key = await GetOrCreateEncryptionKeyAsync();

        // Создаем копию для сериализации, чтобы зашифровать конфиденциальные записи
        var serializableList = new List<DiaryEntry>();

        foreach (var original in entries)
        {
            var copy = new DiaryEntry
            {
                Id = original.Id,
                Title = original.Title,
                Content = original.Content,
                CreatedAt = original.CreatedAt,
                UpdatedAt = original.UpdatedAt,
                Mood = original.Mood,
                Tags = original.Tags != null ? new List<string>(original.Tags) : new List<string>(),
                IsFavorite = original.IsFavorite,
                IsConfidential = original.IsConfidential,
                IsEncrypted = false
            };

            if (copy.IsConfidential && !string.IsNullOrEmpty(copy.Content))
            {
                copy.Content = EncryptText(copy.Content, key);
                copy.IsEncrypted = true;
            }

            serializableList.Add(copy);
        }

        var json = JsonSerializer.Serialize(serializableList, _jsonOptions);
        await File.WriteAllTextAsync(StorageFilePath, json, Encoding.UTF8);
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<string> ExportBackupAsync()
    {
        if (!File.Exists(StorageFilePath))
        {
            await SaveEntriesAsync(new List<DiaryEntry>());
        }

        File.Copy(StorageFilePath, BackupFilePath, true);
        return BackupFilePath;
    }

    public async Task<bool> RestoreBackupAsync()
    {
        if (!File.Exists(BackupFilePath))
            return false;

        File.Copy(BackupFilePath, StorageFilePath, true);
        EntriesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task ResetAllDataAsync()
    {
        if (File.Exists(StorageFilePath))
            File.Delete(StorageFilePath);

        if (File.Exists(BackupFilePath))
            File.Delete(BackupFilePath);

        try
        {
            SecureStorage.Default.Remove(SecureKeyName);
        }
        catch
        {
            // ignored
        }

        await Task.CompletedTask;
        EntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task CreateSampleDataAsync()
    {
        var samples = new List<DiaryEntry>
        {
            new()
            {
                Title = "Успешная защита проекта",
                Content = "Сегодня успешно реализовал архитектуру MVVM на платформе .NET MAUI. Команды работают отлично, интерфейс отзывчивый и чистый!",
                CreatedAt = DateTime.Now.AddHours(-2),
                UpdatedAt = DateTime.Now.AddHours(-2),
                Mood = "😊 Отличное",
                Tags = new List<string> { "учеба", "maui", "csharp" },
                IsFavorite = true,
                IsConfidential = false
            },
            new()
            {
                Title = "Личные размышления и планы",
                Content = "Составил план целей на ближайший месяц. Важно не забывать про отдых, прогулки на свежем воздухе и баланс работы.",
                CreatedAt = DateTime.Now.AddDays(-1),
                UpdatedAt = DateTime.Now.AddDays(-1),
                Mood = "😌 Спокойное",
                Tags = new List<string> { "цели", "мысли" },
                IsFavorite = false,
                IsConfidential = true
            },
            new()
            {
                Title = "Идеи для мобильного дневника",
                Content = "Добавить гибкие фильтры по настроению, быстрый поиск по тегам и шифрование через SecureStorage. Получается очень красиво!",
                CreatedAt = DateTime.Now.AddDays(-3),
                UpdatedAt = DateTime.Now.AddDays(-2),
                Mood = "⚡ Продуктивное",
                Tags = new List<string> { "идеи", "разработка" },
                IsFavorite = true,
                IsConfidential = false
            }
        };

        await SaveEntriesAsync(samples);
    }

    private async Task<byte[]> GetOrCreateEncryptionKeyAsync()
    {
        try
        {
            var storedKeyBase64 = await SecureStorage.Default.GetAsync(SecureKeyName);
            if (!string.IsNullOrEmpty(storedKeyBase64))
            {
                return Convert.FromBase64String(storedKeyBase64);
            }

            // Генерируем новый 256-битный ключ
            var newKey = new byte[32];
            RandomNumberGenerator.Fill(newKey);
            var newKeyBase64 = Convert.ToBase64String(newKey);
            await SecureStorage.Default.SetAsync(SecureKeyName, newKeyBase64);
            return newKey;
        }
        catch
        {
            // Безопасный резервный ключ, если SecureStorage недоступен на платформе/эмуляторе
            var fallback = "Laba9_DiarySecureMasterKey_2026!#";
            return Encoding.UTF8.GetBytes(fallback.PadRight(32).Substring(0, 32));
        }
    }

    private string EncryptText(string plainText, byte[] key)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length); // Первые 16 байт - IV

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private string DecryptText(string cipherBase64, byte[] key)
    {
        if (string.IsNullOrEmpty(cipherBase64)) return string.Empty;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherBase64);
            using var aes = Aes.Create();
            aes.Key = key;

            var ivLength = aes.BlockSize / 8;
            if (fullCipher.Length < ivLength) return cipherBase64;

            var iv = new byte[ivLength];
            Array.Copy(fullCipher, 0, iv, 0, ivLength);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(fullCipher, ivLength, fullCipher.Length - ivLength);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
        catch
        {
            return "[Конфиденциальная запись: расшифровка не удалась]";
        }
    }
}
