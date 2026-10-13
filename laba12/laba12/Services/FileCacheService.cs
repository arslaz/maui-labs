using System.Text.Json;
using laba12.Models;

namespace laba12.Services;

public interface ICacheService
{
    Task SaveDeliveriesAsync(string driverId, List<DeliveryPoint> deliveries);
    Task<List<DeliveryPoint>?> GetCachedDeliveriesAsync(string driverId);
    Task<DateTime?> GetLastCacheTimeAsync(string driverId);
    Task UpdateCachedPointAsync(string driverId, DeliveryPoint updatedPoint);
    Task ClearCacheAsync(string driverId);
}

public class FileCacheService : ICacheService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private string GetCacheFilePath(string driverId)
    {
        var sanitized = string.IsNullOrWhiteSpace(driverId) ? "default" : driverId.Trim();
        return Path.Combine(FileSystem.AppDataDirectory, $"deliveries_cache_{sanitized}.json");
    }

    public async Task SaveDeliveriesAsync(string driverId, List<DeliveryPoint> deliveries)
    {
        try
        {
            var filePath = GetCacheFilePath(driverId);
            var json = JsonSerializer.Serialize(deliveries, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileCacheService] Error saving cache: {ex.Message}");
        }
    }

    public async Task<List<DeliveryPoint>?> GetCachedDeliveriesAsync(string driverId)
    {
        try
        {
            var filePath = GetCacheFilePath(driverId);
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<DeliveryPoint>>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileCacheService] Error reading cache: {ex.Message}");
            return null;
        }
    }

    public async Task<DateTime?> GetLastCacheTimeAsync(string driverId)
    {
        try
        {
            var filePath = GetCacheFilePath(driverId);
            if (File.Exists(filePath))
            {
                return await Task.FromResult(File.GetLastWriteTime(filePath));
            }
        }
        catch { }
        return null;
    }

    public async Task UpdateCachedPointAsync(string driverId, DeliveryPoint updatedPoint)
    {
        try
        {
            var list = await GetCachedDeliveriesAsync(driverId) ?? new List<DeliveryPoint>();
            var index = list.FindIndex(x => x.Id == updatedPoint.Id);
            if (index >= 0)
            {
                list[index] = updatedPoint;
            }
            else
            {
                list.Add(updatedPoint);
            }
            await SaveDeliveriesAsync(driverId, list);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileCacheService] Error updating cached point: {ex.Message}");
        }
    }

    public async Task ClearCacheAsync(string driverId)
    {
        try
        {
            var filePath = GetCacheFilePath(driverId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch { }
        await Task.CompletedTask;
    }
}
