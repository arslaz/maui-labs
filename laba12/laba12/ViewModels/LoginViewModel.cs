using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using laba12.Models;
using laba12.Services;
using laba12.Views;

namespace laba12.ViewModels;

/// <summary>
/// Лабораторная работа 18: ViewModel экрана авторизации водителей (Login View).
/// Реализует:
/// 1. Валидацию полей (логин/табельный номер, длина пароля >= 4) на уровне ViewModel (Требование 1).
/// 2. Онлайн-авторизацию с сохранением токена в SecureStorage и сбросом стека навигации (Требование 3).
/// 3. Офлайн-авторизацию по табельному номеру со сверкой хэша пароля SHA-256 в SQLite (Требование 5).
/// 4. Связывание путевого листа смены с профилем водителя в SQLite (Вариант 7).
/// 5. Переход к экрану регистрации водителя (Register View) (Требование 1).
/// </summary>
public partial class LoginViewModel : BaseViewModel
{
    private readonly ILogisticsApiService _apiService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ITelemetryService _telemetryService;
    private readonly MockBackendServer _mockBackendServer;
    private readonly ILocalDatabaseService _databaseService;
    private readonly INativeFeedbackService _nativeFeedbackService;

    [ObservableProperty]
    private string driverId = "DRV-104";

    [ObservableProperty]
    private string password = "1234";

    [ObservableProperty]
    private string? driverIdError;

    [ObservableProperty]
    private string? passwordError;

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
        MockBackendServer mockBackendServer,
        ILocalDatabaseService databaseService,
        INativeFeedbackService nativeFeedbackService)
    {
        _apiService = apiService;
        _secureStorageService = secureStorageService;
        _telemetryService = telemetryService;
        _mockBackendServer = mockBackendServer;
        _databaseService = databaseService;
        _nativeFeedbackService = nativeFeedbackService;
        Title = "Вход в систему доставки";

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _databaseService.EnsureDatabaseInitializedAsync();

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

    /// <summary>
    /// Лабораторная работа 18 (Требование 1): Валидация полей логина и длины пароля на уровне ViewModel.
    /// </summary>
    public bool ValidateInputs()
    {
        bool isValid = true;
        DriverIdError = null;
        PasswordError = null;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(DriverId) || DriverId.Trim().Length < 3)
        {
            DriverIdError = "Табельный номер водителя / логин должен содержать минимум 3 символа.";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 4)
        {
            PasswordError = "Пароль должен содержать минимум 4 символа (согласно требованиям ТЗ).";
            isValid = false;
        }

        if (!isValid)
        {
            ErrorMessage = "Исправьте ошибки валидации перед отправкой запроса.";
        }

        return isValid;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (!ValidateInputs())
        {
            await _nativeFeedbackService.TriggerWarningFeedbackAsync();
            return;
        }

        ErrorMessage = null;
        StatusMessage = "Выполняется вход в систему...";
        IsBusy = true;

        var token = CreateLinkedToken(TimeSpan.FromSeconds(15));

        try
        {
            // Если включена принудительная симуляция офлайн-сервера, переходим напрямую к офлайн-проверке SQLite
            if (SimulateOfflineServer)
            {
                StatusMessage = "Выполняется вход в автономном режиме...";
                await PerformOfflineLoginFallbackAsync(DriverId.Trim(), Password.Trim());
                return;
            }

            if (UseLocalMockServer && !_mockBackendServer.IsRunning)
            {
                _mockBackendServer.Start();
                ServerUrl = _mockBackendServer.BaseUrl;
            }

            await _secureStorageService.SaveServerUrlAsync(ServerUrl);

            // 1. Отправка запроса на бэкенд
            var result = await _apiService.LoginAsync(DriverId.Trim(), Password.Trim(), token);

            if (result.Success && !string.IsNullOrEmpty(result.Token))
            {
                StatusMessage = $"Вход выполнен: {result.DriverName}";
                ErrorMessage = null;

                // Нативный тактильный и системный отклик
                await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
                await _nativeFeedbackService.ShowNativeToastAsync($"Добро пожаловать, {result.DriverName}!");

                // Лаб 18 (Требование 3): Токен доступа шифруется в SecureStorage
                await _secureStorageService.SaveTokenAsync(result.Token);
                await _secureStorageService.SaveDriverIdAsync(result.DriverId);
                await _secureStorageService.SaveDriverNameAsync(result.DriverName);
                await _secureStorageService.SaveVehicleNumberAsync(result.VehicleNumber);

                // Лаб 18 (Требование 3 & 5 + Вариант 7): Активация сессии в SQLite и связывание путевого листа смены
                await _databaseService.ActivateUserSessionAsync(result.DriverId, Password.Trim(), result.DriverName, result.VehicleNumber);
                await _databaseService.LinkWaybillToDriverProfileAsync(result.DriverId, result.DriverName, result.VehicleNumber);

                // Запуск службы телеметрии
                _telemetryService.Start(result.DriverId);

                // Проверка наличия БД и копирование файла справочника из ресурсов
                await _databaseService.EnsureDatabaseInitializedAsync();
                await _databaseService.MigrateFromLegacyJsonAsync(result.DriverId);
                await _databaseService.GetOrCreateWaybillAsync(result.DriverId, result.DriverName, result.VehicleNumber);

                // Сброс стека навигации и переход на главный рабочий экран
                await Shell.Current.GoToAsync("//DeliveriesPage");
            }
            else
            {
                // Если сервер вернул отказ или недоступен — проверяем офлайн-хэш пароля в локальной SQLite
                await PerformOfflineLoginFallbackAsync(DriverId.Trim(), Password.Trim(), result.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Запрос на вход отменен пользователем.";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            // Ошибка сети — автоматический откат к локальной офлайн-авторизации SQLite
            await PerformOfflineLoginFallbackAsync(DriverId.Trim(), Password.Trim(), $"Сбой подключения к серверу ({ex.Message})");
        }
        finally
        {
            IsBusy = false;
            ResetCancellation();
        }
    }

    /// <summary>
    /// Лабораторная работа 18 (Требование 5): Офлайн-авторизация.
    /// Сверяет введенный хэш пароля (SHA-256) с локально сохраненным в SQLite профилем
    /// последнего активного пользователя для предоставления ограниченного автономного доступа к закэшированным спискам.
    /// Вариант 7: Авторизация водителей по табельному номеру и паролю со связыванием путевого листа смены.
    /// </summary>
    [RelayCommand]
    private async Task LoginOfflineExplicitAsync()
    {
        if (!ValidateInputs())
        {
            await _nativeFeedbackService.TriggerWarningFeedbackAsync();
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Автономная проверка профиля...";

        try
        {
            await PerformOfflineLoginFallbackAsync(DriverId.Trim(), Password.Trim());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PerformOfflineLoginFallbackAsync(string login, string rawPassword, string? onlineFailureReason = null)
    {
        var (success, user, errorMsg) = await _databaseService.AuthenticateOfflineAsync(login, rawPassword);

        if (success && user != null)
        {
            // Офлайн-токен шифруется в SecureStorage
            var offlineToken = $"offline-auth-{Guid.NewGuid():N}";
            await _secureStorageService.SaveTokenAsync(offlineToken);
            await _secureStorageService.SaveDriverIdAsync(user.DriverId);
            await _secureStorageService.SaveDriverNameAsync(user.FullName);
            await _secureStorageService.SaveVehicleNumberAsync(user.VehicleNumber);

            // Вариант 7: Связывание путевого листа смены с профилем водителя
            await _databaseService.LinkWaybillToDriverProfileAsync(user.DriverId, user.FullName, user.VehicleNumber);

            // Запуск телеметрии
            _telemetryService.Start(user.DriverId);

            await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
            await _nativeFeedbackService.ShowNativeToastAsync($"Офлайн-доступ предоставлен: {user.FullName}");

            StatusMessage = "Автономный вход выполнен успешно.";
            
            // Сброс стека навигации и переход на экран путевого листа
            await Shell.Current.GoToAsync("//DeliveriesPage");
        }
        else
        {
            await _nativeFeedbackService.TriggerWarningFeedbackAsync();
            ErrorMessage = errorMsg ?? onlineFailureReason ?? "Неверный табельный номер или пароль.";
            StatusMessage = null;
        }
    }

    /// <summary>
    /// Лабораторная работа 18 (Требование 1): Переход к связанному экрану регистрации (Register View).
    /// </summary>
    [RelayCommand]
    private async Task NavigateToRegisterAsync()
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }

    /// <summary>
    /// Быстрый выбор предустановленных водителей для демонстрации преподавателю
    /// </summary>
    [RelayCommand]
    private void SelectPresetDriver(string id)
    {
        DriverId = id;
        Password = id switch
        {
            "DRV-104" => "1234",
            "DRV-105" => "4321",
            "DRV-106" => "5678",
            _ => "1234"
        };
        ErrorMessage = null;
        DriverIdError = null;
        PasswordError = null;
        StatusMessage = $"Выбран тестовый профиль водителя {id}.";
    }

    [RelayCommand]
    private async Task ClearCredentialsAsync()
    {
        await _nativeFeedbackService.TriggerClickFeedbackAsync();
        await _secureStorageService.ClearAuthAsync();
        SavedTokenPreview = null;
        DriverId = string.Empty;
        Password = string.Empty;
        ErrorMessage = null;
        DriverIdError = null;
        PasswordError = null;
        StatusMessage = "Сессия и учетные данные полностью очищены из SecureStorage.";
    }
}
