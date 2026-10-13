using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using laba12.Services;

namespace laba12.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly ILogisticsApiService _apiService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ITelemetryService _telemetryService;
    private readonly MockBackendServer _mockBackendServer;

    [ObservableProperty]
    private string driverId = "DRV-104";

    [ObservableProperty]
    private string password = "1234";

    [ObservableProperty]
    private string serverUrl = "http://127.0.0.1:5055";

    [ObservableProperty]
    private bool useLocalMockServer = true;

    [ObservableProperty]
    private bool simulateOfflineServer = false;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? savedTokenPreview;

    public LoginViewModel(
        ILogisticsApiService apiService,
        ISecureStorageService secureStorageService,
        ITelemetryService telemetryService,
        MockBackendServer mockBackendServer)
    {
        _apiService = apiService;
        _secureStorageService = secureStorageService;
        _telemetryService = telemetryService;
        _mockBackendServer = mockBackendServer;
        Title = "Вход в систему доставки";

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var savedDriver = await _secureStorageService.GetDriverIdAsync();
        if (!string.IsNullOrEmpty(savedDriver))
        {
            DriverId = savedDriver;
        }

        var savedUrl = await _secureStorageService.GetServerUrlAsync();
        if (!string.IsNullOrEmpty(savedUrl))
        {
            ServerUrl = savedUrl;
        }

        var token = await _secureStorageService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            SavedTokenPreview = $"Активный токен: {token[..Math.Min(16, token.Length)]}... (SecureStorage)";
        }

        // Запуск локального тестового сервера REST API
        if (UseLocalMockServer && !_mockBackendServer.IsRunning)
        {
            _mockBackendServer.Start();
            ServerUrl = _mockBackendServer.BaseUrl;
        }
    }

    partial void OnUseLocalMockServerChanged(bool value)
    {
        if (value)
        {
            if (!_mockBackendServer.IsRunning)
            {
                _mockBackendServer.Start();
            }
            ServerUrl = _mockBackendServer.BaseUrl;
        }
    }

    partial void OnSimulateOfflineServerChanged(bool value)
    {
        _mockBackendServer.SimulateOffline = value;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(DriverId))
        {
            ErrorMessage = "Укажите табельный номер водителя (ID).";
            StatusMessage = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введите пароль доступа.";
            StatusMessage = null;
            return;
        }

        ErrorMessage = null;
        StatusMessage = "Проверка учетных данных на сервере компании (JWT-верификация)...";
        IsBusy = true;

        var token = CreateLinkedToken(TimeSpan.FromSeconds(15));

        try
        {
            if (UseLocalMockServer && !_mockBackendServer.IsRunning)
            {
                _mockBackendServer.Start();
                ServerUrl = _mockBackendServer.BaseUrl;
            }

            await _secureStorageService.SaveServerUrlAsync(ServerUrl);

            // Отправка запроса на сервер
            var result = await _apiService.LoginAsync(DriverId.Trim(), Password.Trim(), token);

            if (result.Success && !string.IsNullOrEmpty(result.Token))
            {
                StatusMessage = $"Вход выполнен: {result.DriverName}";
                ErrorMessage = null;
                
                // Запуск службы телеметрии
                _telemetryService.Start(result.DriverId);

                // Переход на экран путевого листа разрешен исключительно после валидного ответа API
                await Shell.Current.GoToAsync("//DeliveriesPage");
            }
            else
            {
                // Сервер отклонил вход (401 Unauthorized): доступ к контенту заблокирован!
                ErrorMessage = result.ErrorMessage ?? "Ошибка аутентификации: неверный логин или пароль.";
                StatusMessage = null;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Запрос на вход отменен пользователем.";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Сбой подключения к серверу: {ex.Message}";
            StatusMessage = null;
        }
        finally
        {
            IsBusy = false;
            ResetCancellation();
        }
    }

    [RelayCommand]
    private async Task ClearCredentialsAsync()
    {
        await _secureStorageService.ClearAuthAsync();
        SavedTokenPreview = null;
        DriverId = string.Empty;
        Password = string.Empty;
        ErrorMessage = null;
        StatusMessage = "Сессия и учетные данные полностью очищены из SecureStorage.";
    }
}
