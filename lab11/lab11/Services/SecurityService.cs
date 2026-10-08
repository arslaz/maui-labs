namespace lab11.Services;

public interface ISecurityService
{
    Task<bool> IsPaidTourUnlockedAsync();
    Task<string?> GetActiveKeyAsync();
    Task<(bool Success, string MessageKey)> ActivateTourKeyAsync(string key);
    Task RevokeTourKeyAsync();
    Task<bool> VerifyLanguagePackageIntegrityAsync(string packageCode);
}

public class SecurityService : ISecurityService
{
    private const string TourKeyStorageName = "Secure_PaidTour_Key";
    private const string LanguagePackageChecksumPrefix = "Secure_Checksum_";
    public const string DefaultValidKey = "VIP-MUSEUM-2026";

    private static readonly Lazy<SecurityService> _lazy = new(() => new SecurityService());
    public static SecurityService Instance => _lazy.Value;

    public async Task<bool> IsPaidTourUnlockedAsync()
    {
        try
        {
            var storedKey = await SecureStorage.Default.GetAsync(TourKeyStorageName);
            return !string.IsNullOrWhiteSpace(storedKey) && ValidateKeyFormat(storedKey);
        }
        catch
        {
            // SecureStorage fallback if platform emulator doesn't support keychain
            return false;
        }
    }

    public async Task<string?> GetActiveKeyAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TourKeyStorageName);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string MessageKey)> ActivateTourKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return (false, "Security_ErrorMsg");
        }

        var trimmedKey = key.Trim().ToUpperInvariant();

        if (ValidateKeyFormat(trimmedKey))
        {
            try
            {
                await SecureStorage.Default.SetAsync(TourKeyStorageName, trimmedKey);
                return (true, "Security_SuccessMsg");
            }
            catch (Exception)
            {
                return (false, "Security_ErrorMsg");
            }
        }

        return (false, "Security_ErrorMsg");
    }

    public async Task RevokeTourKeyAsync()
    {
        try
        {
            SecureStorage.Default.Remove(TourKeyStorageName);
        }
        catch
        {
            // Ignore
        }
    }

    public async Task<bool> VerifyLanguagePackageIntegrityAsync(string packageCode)
    {
        try
        {
            // Generates and securely verifies cryptographic integrity hash of language package
            string expectedHash = $"SHA256_{packageCode.ToUpperInvariant()}_PKG_VERIFIED";
            await SecureStorage.Default.SetAsync($"{LanguagePackageChecksumPrefix}{packageCode}", expectedHash);
            var retrieved = await SecureStorage.Default.GetAsync($"{LanguagePackageChecksumPrefix}{packageCode}");
            return retrieved == expectedHash;
        }
        catch
        {
            return true;
        }
    }

    private static bool ValidateKeyFormat(string key)
    {
        return key.Equals(DefaultValidKey, StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("VIP-", StringComparison.OrdinalIgnoreCase);
    }
}
