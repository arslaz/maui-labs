using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using laba12.Models;

namespace laba12.Services;

/// <summary>
/// Перехватчик DelegatingHandler:
/// 1. Автоматически внедряет заголовок Authorization: Bearer {token} во все исходящие запросы.
/// 2. При получении 401 Unauthorized приостанавливает поток, отправляет фоновый запрос /api/auth/refresh,
///    перезаписывает новые токены в SecureStorage и повторяет исходный запрос.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ISecureStorageService _secureStorageService;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthHeaderHandler(ISecureStorageService secureStorageService)
    {
        _secureStorageService = secureStorageService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        // Эндпоинты входа и обновления сессии не требуют Bearer-токена
        if (path.Contains("/api/auth/login") || path.Contains("/api/auth/refresh"))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // 1. Автоматическая подпись запроса токеном из SecureStorage
        var token = await _secureStorageService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 2. Выполнение исходного запроса
        var response = await base.SendAsync(request, cancellationToken);

        // 3. Если токен устарел (401 Unauthorized) — перехватываем и скрыто обновляем сессию через Refresh Token
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                // Проверяем, возможно, токен уже обновлен параллельным потоком
                var currentToken = await _secureStorageService.GetTokenAsync();
                if (currentToken != token && !string.IsNullOrEmpty(currentToken))
                {
                    // Токен уже обновлен, повторяем запрос с новым токеном
                    var retryRequest = await CloneHttpRequestMessageAsync(request);
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
                    return await base.SendAsync(retryRequest, cancellationToken);
                }

                var refreshToken = await _secureStorageService.GetRefreshTokenAsync();
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    System.Diagnostics.Debug.WriteLine("[AuthHeaderHandler] Истек срок действия токена. Отправка фонового запроса /api/auth/refresh...");

                    var refreshed = await TryRefreshTokenAsync(request.RequestUri, refreshToken, cancellationToken);
                    if (refreshed != null && refreshed.Success && !string.IsNullOrEmpty(refreshed.Token))
                    {
                        // Перезаписываем ключи в SecureStorage
                        await _secureStorageService.SaveTokenAsync(refreshed.Token);
                        if (!string.IsNullOrEmpty(refreshed.RefreshToken))
                        {
                            await _secureStorageService.SaveRefreshTokenAsync(refreshed.RefreshToken);
                        }

                        System.Diagnostics.Debug.WriteLine("[AuthHeaderHandler] Токен успешно обновлен! Повтор исходного запроса.");

                        // Повторяем исходный запрос пользователя с новым токеном
                        var retryRequest = await CloneHttpRequestMessageAsync(request);
                        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.Token);
                        return await base.SendAsync(retryRequest, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthHeaderHandler] Ошибка при обновлении сессии: {ex.Message}");
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        return response;
    }

    private async Task<AuthResponse?> TryRefreshTokenAsync(Uri? baseUri, string refreshToken, CancellationToken ct)
    {
        try
        {
            var serverUrl = await _secureStorageService.GetServerUrlAsync();
            var refreshUri = new Uri(new Uri(serverUrl.TrimEnd('/') + "/"), "api/auth/refresh");

            using var rawClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var payload = JsonSerializer.Serialize(new { refreshToken }, _jsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var res = await rawClient.PostAsync(refreshUri, content, ct);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<AuthResponse>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthHeaderHandler] RefreshToken request failed: {ex.Message}");
            return null;
        }
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version
        };

        if (req.Content != null)
        {
            var ms = new MemoryStream();
            await req.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);

            foreach (var h in req.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
        }

        foreach (var header in req.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
