using System.Text.Json;
using laba12.Models;

namespace laba12.Services;

public interface ISyncQueueService
{
    event Action<int>? QueueChanged;
    Task EnqueueAsync(SyncTransaction transaction);
    Task<List<SyncTransaction>> GetPendingTransactionsAsync();
    Task<int> GetPendingCountAsync();
    Task<(int Succeeded, int Failed, string Message)> ProcessQueueAsync(ILogisticsApiService apiService, CancellationToken cancellationToken = default);
    Task ClearQueueAsync();
}

public class SyncQueueService : ISyncQueueService
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public event Action<int>? QueueChanged;

    private string QueueFilePath => Path.Combine(FileSystem.AppDataDirectory, "pending_sync_queue.json");

    public async Task EnqueueAsync(SyncTransaction transaction)
    {
        await _fileLock.WaitAsync();
        try
        {
            var list = await ReadQueueInternalAsync();
            list.Add(transaction);
            await WriteQueueInternalAsync(list);
            QueueChanged?.Invoke(list.Count);
            System.Diagnostics.Debug.WriteLine($"[SyncQueueService] Транзакция {transaction.HttpMethod} сохранена в локальный JSON-лог. Всего в очереди: {list.Count}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<List<SyncTransaction>> GetPendingTransactionsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            return await ReadQueueInternalAsync();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<int> GetPendingCountAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var list = await ReadQueueInternalAsync();
            return list.Count;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<(int Succeeded, int Failed, string Message)> ProcessQueueAsync(ILogisticsApiService apiService, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var list = await ReadQueueInternalAsync();
            if (!list.Any())
            {
                return (0, 0, "Очередь отложенных транзакций пуста.");
            }

            int succeeded = 0;
            int failed = 0;
            var remaining = new List<SyncTransaction>();

            foreach (var tx in list)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    remaining.Add(tx);
                    continue;
                }

                try
                {
                    bool ok = false;
                    switch (tx.HttpMethod.ToUpperInvariant())
                    {
                        case "PATCH":
                            // Отметить прибытие
                            DateTime arrivalTime = DateTime.UtcNow;
                            if (!string.IsNullOrEmpty(tx.PayloadJson))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(tx.PayloadJson);
                                    if (doc.RootElement.TryGetProperty("arrivedAt", out var arrivedProp) && arrivedProp.TryGetDateTime(out var dt))
                                    {
                                        arrivalTime = dt;
                                    }
                                }
                                catch { }
                            }
                            var patchRes = await apiService.MarkArrivalAsync(tx.DeliveryId, arrivalTime, cancellationToken);
                            ok = patchRes != null;
                            break;

                        case "PUT":
                            // Зафиксировать отказ
                            string reason = "Отказ (синхронизация из офлайн-лога)";
                            if (!string.IsNullOrEmpty(tx.PayloadJson))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(tx.PayloadJson);
                                    if (doc.RootElement.TryGetProperty("reason", out var reasonProp))
                                    {
                                        reason = reasonProp.GetString() ?? reason;
                                    }
                                }
                                catch { }
                            }
                            var putRes = await apiService.RejectCargoAsync(tx.DeliveryId, reason, cancellationToken);
                            ok = putRes != null;
                            break;

                        case "DELETE":
                            // Удаление точки доставки
                            ok = await apiService.DeleteDeliveryAsync(tx.DeliveryId, cancellationToken);
                            break;

                        default:
                            ok = true; // Пропустить неизвестную
                            break;
                    }

                    if (ok)
                    {
                        succeeded++;
                        System.Diagnostics.Debug.WriteLine($"[SyncQueueService] Синхронизирована отложенная операция: {tx.HttpMethod} {tx.DeliveryId} ({tx.Description})");
                    }
                    else
                    {
                        failed++;
                        remaining.Add(tx);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SyncQueueService] Ошибка отправки транзакции {tx.Id}: {ex.Message}");
                    failed++;
                    remaining.Add(tx);
                    // Если произошел разрыв соединения, прекращаем дальнейшие попытки до следующего подключения
                    break;
                }
            }

            await WriteQueueInternalAsync(remaining);
            QueueChanged?.Invoke(remaining.Count);

            string msg = remaining.Count == 0
                ? $"Все отложенные транзакции ({succeeded}) успешно синхронизированы с сервером!"
                : $"Синхронизировано: {succeeded}, осталось в очереди: {remaining.Count}.";

            return (succeeded, failed, msg);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ClearQueueAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(QueueFilePath))
            {
                File.Delete(QueueFilePath);
            }
            QueueChanged?.Invoke(0);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<SyncTransaction>> ReadQueueInternalAsync()
    {
        try
        {
            if (!File.Exists(QueueFilePath)) return new List<SyncTransaction>();
            var json = await File.ReadAllTextAsync(QueueFilePath);
            return JsonSerializer.Deserialize<List<SyncTransaction>>(json, _jsonOptions) ?? new List<SyncTransaction>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SyncQueueService] Ошибка чтения JSON-лога: {ex.Message}");
            return new List<SyncTransaction>();
        }
    }

    private async Task WriteQueueInternalAsync(List<SyncTransaction> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, _jsonOptions);
            await File.WriteAllTextAsync(QueueFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SyncQueueService] Ошибка записи JSON-лога: {ex.Message}");
        }
    }
}
