using System.Security.Cryptography;

namespace laba12.Services;

public interface ISecureStorageService
{
    Task SaveTokenAsync(string token);
    Task<string?> GetTokenAsync();
    Task SaveRefreshTokenAsync(string refreshToken);
    Task<string?> GetRefreshTokenAsync();
    Task SaveDriverIdAsync(string driverId);
    Task<string?> GetDriverIdAsync();
    Task SaveDriverNameAsync(string driverName);
    Task<string?> GetDriverNameAsync();
    Task SaveVehicleNumberAsync(string vehicleNumber);
    Task<string?> GetVehicleNumberAsync();
    Task SaveServerUrlAsync(string url);
    Task<string> GetServerUrlAsync();
    Task<byte[]> GetOrCreateEncryptionKeyAsync();
    Task SimulateExpireTokenAsync();
    Task ClearAuthAsync();
}

public class SecureStorageService : ISecureStorageService
{
    private const string TokenKey = "auth_token";
    private const string RefreshTokenKey = "refresh_token";
    private const string DriverIdKey = "driver_id";
    private const string DriverNameKey = "driver_name";
    private const string VehicleNumberKey = "vehicle_number";
    private const string ServerUrlKey = "server_url";
    private const string EncryptionKeyStorage = "fuel_report_aes_key";
    private const string DefaultServerUrl = "http://127.0.0.1:5055";

    public async Task SaveTokenAsync(string token)
    {
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
        }
        catch
        {
            Preferences.Default.Set(TokenKey, token);
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            return token ?? Preferences.Default.Get(TokenKey, (string?)null);
        }
        catch
        {
            return Preferences.Default.Get(TokenKey, (string?)null);
        }
    }

    public async Task SaveRefreshTokenAsync(string refreshToken)
    {
        try
        {
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
        }
        catch
        {
            Preferences.Default.Set(RefreshTokenKey, refreshToken);
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            return token ?? Preferences.Default.Get(RefreshTokenKey, (string?)null);
        }
        catch
        {
            return Preferences.Default.Get(RefreshTokenKey, (string?)null);
        }
    }

    public async Task SaveDriverIdAsync(string driverId)
    {
        try
        {
            await SecureStorage.Default.SetAsync(DriverIdKey, driverId);
        }
        catch
        {
            Preferences.Default.Set(DriverIdKey, driverId);
        }
    }

    public async Task<string?> GetDriverIdAsync()
    {
        try
        {
            var driverId = await SecureStorage.Default.GetAsync(DriverIdKey);
            return driverId ?? Preferences.Default.Get(DriverIdKey, "DRV-104");
        }
        catch
        {
            return Preferences.Default.Get(DriverIdKey, "DRV-104");
        }
    }

    public async Task SaveDriverNameAsync(string driverName)
    {
        try
        {
            await SecureStorage.Default.SetAsync(DriverNameKey, driverName);
        }
        catch
        {
            Preferences.Default.Set(DriverNameKey, driverName);
        }
    }

    public async Task<string?> GetDriverNameAsync()
    {
        try
        {
            var driverName = await SecureStorage.Default.GetAsync(DriverNameKey);
            return driverName ?? Preferences.Default.Get(DriverNameKey, (string?)null);
        }
        catch
        {
            return Preferences.Default.Get(DriverNameKey, (string?)null);
        }
    }

    public async Task SaveVehicleNumberAsync(string vehicleNumber)
    {
        try
        {
            await SecureStorage.Default.SetAsync(VehicleNumberKey, vehicleNumber);
        }
        catch
        {
            Preferences.Default.Set(VehicleNumberKey, vehicleNumber);
        }
    }

    public async Task<string?> GetVehicleNumberAsync()
    {
        try
        {
            var vehicle = await SecureStorage.Default.GetAsync(VehicleNumberKey);
            return vehicle ?? Preferences.Default.Get(VehicleNumberKey, (string?)null);
        }
        catch
        {
            return Preferences.Default.Get(VehicleNumberKey, (string?)null);
        }
    }

    public async Task SaveServerUrlAsync(string url)
    {
        Preferences.Default.Set(ServerUrlKey, url);
        await Task.CompletedTask;
    }

    public async Task<string> GetServerUrlAsync()
    {
        var url = Preferences.Default.Get(ServerUrlKey, DefaultServerUrl);
        return await Task.FromResult(url);
    }

    public async Task<byte[]> GetOrCreateEncryptionKeyAsync()
    {
        try
        {
            var existingKeyBase64 = await SecureStorage.Default.GetAsync(EncryptionKeyStorage) 
                                   ?? Preferences.Default.Get(EncryptionKeyStorage, (string?)null);

            if (!string.IsNullOrEmpty(existingKeyBase64))
            {
                return Convert.FromBase64String(existingKeyBase64);
            }

            var newKey = new byte[32];
            RandomNumberGenerator.Fill(newKey);
            var base64 = Convert.ToBase64String(newKey);

            try
            {
                await SecureStorage.Default.SetAsync(EncryptionKeyStorage, base64);
            }
            catch
            {
                Preferences.Default.Set(EncryptionKeyStorage, base64);
            }

            return newKey;
        }
        catch
        {
            return new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        }
    }

    public async Task SimulateExpireTokenAsync()
    {
        await SaveTokenAsync("expired-jwt-token-demonstration-trigger");
    }

    public async Task ClearAuthAsync()
    {
        try
        {
            SecureStorage.Default.Remove(TokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
            SecureStorage.Default.Remove(DriverIdKey);
            SecureStorage.Default.Remove(DriverNameKey);
            SecureStorage.Default.Remove(VehicleNumberKey);

            Preferences.Default.Remove(TokenKey);
            Preferences.Default.Remove(RefreshTokenKey);
            Preferences.Default.Remove(DriverIdKey);
            Preferences.Default.Remove(DriverNameKey);
            Preferences.Default.Remove(VehicleNumberKey);
        }
        catch
        {
            Preferences.Default.Remove(TokenKey);
            Preferences.Default.Remove(RefreshTokenKey);
            Preferences.Default.Remove(DriverIdKey);
            Preferences.Default.Remove(DriverNameKey);
            Preferences.Default.Remove(VehicleNumberKey);
        }
        await Task.CompletedTask;
    }
}
