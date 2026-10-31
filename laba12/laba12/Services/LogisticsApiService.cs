using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using laba12.Models;

namespace laba12.Services;

public interface ILogisticsApiService
{
    Task<AuthResponse> LoginAsync(string driverId, string password, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<DeliveryListResponse> GetDeliveriesAsync(string driverId, int page = 1, int pageSize = 5, CancellationToken cancellationToken = default);
    Task<DeliveryPoint?> UpdateDeliveryStatusAsync(string deliveryId, DeliveryPoint updatedPoint, CancellationToken cancellationToken = default);
    Task<DeliveryPoint?> MarkArrivalAsync(string deliveryId, DateTime arrivalTime, CancellationToken cancellationToken = default);
    Task<DeliveryPoint?> RejectCargoAsync(string deliveryId, string reason, CancellationToken cancellationToken = default);
    Task<bool> DeleteDeliveryAsync(string deliveryId, CancellationToken cancellationToken = default);
    Task<FuelReportUploadResult> UploadFuelReportAsync(string driverId, string filePath, CancellationToken cancellationToken = default);
    Task<bool> SendTelemetryAsync(TelemetryData telemetry, CancellationToken cancellationToken = default);
    Task<bool> SendBatchTelemetryAsync(IEnumerable<TelemetryData> telemetryBatch, CancellationToken cancellationToken = default);
}

public class LogisticsApiService : ILogisticsApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecureStorageService _secureStorageService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LogisticsApiService(IHttpClientFactory httpClientFactory, ISecureStorageService secureStorageService)
    {
        _httpClientFactory = httpClientFactory;
        _secureStorageService = secureStorageService;
    }

    private async Task<HttpClient> CreateConfiguredClientAsync()
    {
        // Клиент "LogisticsClient" настроен с AuthHeaderHandler,
        // который автоматически инжектирует заголовок Authorization: Bearer {token}
        // и скрыто вызывает /api/auth/refresh при получении 401 Unauthorized.
        var client = _httpClientFactory.CreateClient("LogisticsClient");
        var baseUrl = await _secureStorageService.GetServerUrlAsync();
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }
        else
        {
            client.BaseAddress = new Uri("http://127.0.0.1:5055/");
        }

        return client;
    }

    public async Task<AuthResponse> LoginAsync(string driverId, string password, CancellationToken cancellationToken = default)
    {
        int maxRetries = 2;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var client = await CreateConfiguredClientAsync();
                var loginData = new LoginRequest { DriverId = driverId, Password = password };
                var jsonContent = new StringContent(JsonSerializer.Serialize(loginData, _jsonOptions), Encoding.UTF8, "application/json");

                using var response = await client.PostAsync("api/auth/login", jsonContent, cancellationToken);
                
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    try
                    {
                        var errJson = await response.Content.ReadAsStringAsync(cancellationToken);
                        var errRes = JsonSerializer.Deserialize<AuthResponse>(errJson, _jsonOptions);
                        return errRes ?? new AuthResponse { Success = false, ErrorMessage = "Неверный логин или пароль водителя (401 Unauthorized)." };
                    }
                    catch
                    {
                        return new AuthResponse { Success = false, ErrorMessage = "Неверный логин или пароль водителя (401 Unauthorized)." };
                    }
                }

                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var authResult = JsonSerializer.Deserialize<AuthResponse>(responseJson, _jsonOptions);

                if (authResult != null && authResult.Success)
                {
                    // Безопасное сохранение сессионных токенов через SecureStorage
                    await _secureStorageService.SaveTokenAsync(authResult.Token);
                    if (!string.IsNullOrEmpty(authResult.RefreshToken))
                    {
                        await _secureStorageService.SaveRefreshTokenAsync(authResult.RefreshToken);
                    }
                    await _secureStorageService.SaveDriverIdAsync(authResult.DriverId);
                    await _secureStorageService.SaveDriverNameAsync(authResult.DriverName);
                    await _secureStorageService.SaveVehicleNumberAsync(authResult.VehicleNumber);
                    return authResult;
                }

                return authResult ?? new AuthResponse { Success = false, ErrorMessage = "Пустой ответ сервера." };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                System.Diagnostics.Debug.WriteLine($"[LogisticsApiService] Повтор запроса авторизации ({attempt}) из-за ошибки: {ex.Message}");
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    return new AuthResponse { Success = false, ErrorMessage = $"Ошибка сети: {ex.Message}" };
                }
            }
        }

        return new AuthResponse { Success = false, ErrorMessage = "Не удалось подключиться к серверу после нескольких попыток." };
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateConfiguredClientAsync();
            var jsonContent = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");

            using var response = await client.PostAsync("api/auth/register", jsonContent, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var authResult = JsonSerializer.Deserialize<AuthResponse>(responseJson, _jsonOptions);
                if (authResult != null && authResult.Success)
                {
                    await _secureStorageService.SaveTokenAsync(authResult.Token);
                    if (!string.IsNullOrEmpty(authResult.RefreshToken))
                    {
                        await _secureStorageService.SaveRefreshTokenAsync(authResult.RefreshToken);
                    }
                    await _secureStorageService.SaveDriverIdAsync(authResult.DriverId);
                    await _secureStorageService.SaveDriverNameAsync(authResult.DriverName);
                    await _secureStorageService.SaveVehicleNumberAsync(authResult.VehicleNumber);
                    return authResult;
                }
            }

            try
            {
                var errRes = JsonSerializer.Deserialize<AuthResponse>(responseJson, _jsonOptions);
                return errRes ?? new AuthResponse { Success = false, ErrorMessage = $"Ошибка регистрации (HTTP {response.StatusCode})" };
            }
            catch
            {
                return new AuthResponse { Success = false, ErrorMessage = $"Ошибка сервера: {responseJson}" };
            }
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, ErrorMessage = $"Сетевой сбой при регистрации: {ex.Message}" };
        }
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateConfiguredClientAsync();
            var payload = JsonSerializer.Serialize(new { refreshToken }, _jsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync("api/auth/refresh", content, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<AuthResponse>(json, _jsonOptions);
            if (result != null && result.Success)
            {
                await _secureStorageService.SaveTokenAsync(result.Token);
                if (!string.IsNullOrEmpty(result.RefreshToken))
                {
                    await _secureStorageService.SaveRefreshTokenAsync(result.RefreshToken);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogisticsApiService] Ошибка обновления токена: {ex.Message}");
            return null;
        }
    }

    public async Task<DeliveryListResponse> GetDeliveriesAsync(string driverId, int page = 1, int pageSize = 5, CancellationToken cancellationToken = default)
    {
        var client = await CreateConfiguredClientAsync();
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var url = $"api/drivers/{Uri.EscapeDataString(driverId)}/deliveries?date={today}&page={page}&pageSize={pageSize}";

        using var response = await client.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Токен авторизации устарел или неверен. Требуется повторный вход.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new DeliveryListResponse { Items = new(), TotalCount = 0, Page = page, PageSize = pageSize, HasMore = false };
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<DeliveryListResponse>(json, _jsonOptions);

        return result ?? new DeliveryListResponse();
    }

    public async Task<DeliveryPoint?> UpdateDeliveryStatusAsync(string deliveryId, DeliveryPoint updatedPoint, CancellationToken cancellationToken = default)
    {
        var client = await CreateConfiguredClientAsync();
        var json = JsonSerializer.Serialize(updatedPoint, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PutAsync($"api/deliveries/{Uri.EscapeDataString(deliveryId)}", content, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Сессия истекла.");
        }

        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<DeliveryPoint>(responseJson, _jsonOptions);
    }

    /// <summary>
    /// Вариант 7: Команда «Отметить прибытие» — PATCH-запрос со штампом времени
    /// </summary>
    public async Task<DeliveryPoint?> MarkArrivalAsync(string deliveryId, DateTime arrivalTime, CancellationToken cancellationToken = default)
    {
        var client = await CreateConfiguredClientAsync();
        var payload = JsonSerializer.Serialize(new { arrivedAt = arrivalTime }, _jsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"api/deliveries/{Uri.EscapeDataString(deliveryId)}/arrival")
        {
            Content = content
        };

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<DeliveryPoint>(json, _jsonOptions);
    }

    /// <summary>
    /// Вариант 7: Команда «Зафиксировать отказ от груза» — PUT-запрос с указанием причины
    /// </summary>
    public async Task<DeliveryPoint?> RejectCargoAsync(string deliveryId, string reason, CancellationToken cancellationToken = default)
    {
        var client = await CreateConfiguredClientAsync();
        var payload = JsonSerializer.Serialize(new { reason, rejectedAt = DateTime.UtcNow }, _jsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.PutAsync($"api/deliveries/{Uri.EscapeDataString(deliveryId)}/reject", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<DeliveryPoint>(json, _jsonOptions);
    }

    /// <summary>
    /// Общее требование: Удаление (DELETE) ресурсов сервера через контекстное меню
    /// </summary>
    public async Task<bool> DeleteDeliveryAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        var client = await CreateConfiguredClientAsync();
        using var response = await client.DeleteAsync($"api/deliveries/{Uri.EscapeDataString(deliveryId)}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Вариант 7 + Теоретическая часть: Отправка зашифрованного файла отчета о расходе топлива
    /// через HTTP POST MultipartFormDataContent со StreamContent и бинарными данными
    /// </summary>
    public async Task<FuelReportUploadResult> UploadFuelReportAsync(string driverId, string filePath, CancellationToken cancellationToken = default)
    {
        var client = await CreateConfiguredClientAsync();

        using var content = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(filePath);
        using var streamContent = new StreamContent(fileStream);

        // Бинарный контент зашифрованного файла с mime-типом application/octet-stream
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", Path.GetFileName(filePath));

        // Метаданные запроса
        content.Add(new StringContent(driverId), "driverId");
        content.Add(new StringContent(DateTime.UtcNow.ToString("O")), "reportDate");
        content.Add(new StringContent("AES-256-CBC"), "encryptionType");

        using var response = await client.PostAsync("api/reports/fuel", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<FuelReportUploadResult>(json, _jsonOptions) 
               ?? new FuelReportUploadResult { Success = true, ServerMessage = "Отчет принят" };
    }

    public async Task<bool> SendTelemetryAsync(TelemetryData telemetry, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateConfiguredClientAsync();
            var json = JsonSerializer.Serialize(telemetry, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync("api/telemetry", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogisticsApiService] Сбой отправки телеметрии: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendBatchTelemetryAsync(IEnumerable<TelemetryData> telemetryBatch, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await CreateConfiguredClientAsync();
            var json = JsonSerializer.Serialize(telemetryBatch, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync("api/telemetry/batch", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogisticsApiService] Сбой отправки пакета телеметрии: {ex.Message}");
            return false;
        }
    }
}
