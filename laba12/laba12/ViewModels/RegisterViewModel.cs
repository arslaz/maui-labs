using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using laba12.Models;
using laba12.Services;

namespace laba12.ViewModels;

/// <summary>
/// Лабораторная работа 18 (Требование 1 &amp; 2): ViewModel экрана регистрации (Register View).
/// Реализует полную валидацию текстовых полей (длина пароля, формат email/логина) на уровне ViewModel,
/// отправку данных на бэкенд и создание пустой структуры данных в SQLite под нового пользователя.
/// </summary>
public partial class RegisterViewModel : BaseViewModel
{
    private readonly ILogisticsApiService _apiService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ILocalDatabaseService _databaseService;
    private readonly ITelemetryService _telemetryService;
    private readonly INativeFeedbackService _nativeFeedbackService;

    [ObservableProperty]
    private string driverId = "DRV-107";

    [ObservableProperty]
    private string email = "ivanov@logistics.by";

    [ObservableProperty]
    private string fullName = "Иван Иванов (Водитель-экспедитор)";

    [ObservableProperty]
    private string vehicleNumber = "АВ 9012-7 (ГАЗель NEXT)";

    [ObservableProperty]
    private string phone = "+375 (29) 700-11-22";

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    // Свойства ошибок валидации для каждого поля
    [ObservableProperty]
    private string? driverIdError;

    [ObservableProperty]
    private string? emailError;

    [ObservableProperty]
    private string? fullNameError;

    [ObservableProperty]
    private string? vehicleNumberError;

    [ObservableProperty]
    private string? phoneError;

    [ObservableProperty]
    private string? passwordError;

    [ObservableProperty]
    private string? confirmPasswordError;

    [ObservableProperty]
    private string? statusMessage;

    public RegisterViewModel(
        ILogisticsApiService apiService,
        ISecureStorageService secureStorageService,
        ILocalDatabaseService databaseService,
        ITelemetryService telemetryService,
        INativeFeedbackService nativeFeedbackService)
    {
        _apiService = apiService;
        _secureStorageService = secureStorageService;
        _databaseService = databaseService;
        _telemetryService = telemetryService;
        _nativeFeedbackService = nativeFeedbackService;
        Title = "Регистрация водителя";
    }

    /// <summary>
    /// Лабораторная работа 18 (Требование 1): Полная валидация текстовых полей
    /// (проверка длины пароля, формата email/логина) на уровне ViewModel.
    /// </summary>
    public bool ValidateAll()
    {
        bool isValid = true;
        DriverIdError = null;
        EmailError = null;
        FullNameError = null;
        VehicleNumberError = null;
        PhoneError = null;
        PasswordError = null;
        ConfirmPasswordError = null;
        ErrorMessage = null;

        // Валидация логина / табельного номера водителя
        if (string.IsNullOrWhiteSpace(DriverId) || DriverId.Trim().Length < 3)
        {
            DriverIdError = "Табельный номер (ID) должен содержать минимум 3 символа.";
            isValid = false;
        }
        else if (!Regex.IsMatch(DriverId.Trim(), @"^[A-Za-z0-9\-_]+$"))
        {
            DriverIdError = "Табельный номер может содержать только латиницу, цифры, дефис и подчеркивание (напр. DRV-107).";
            isValid = false;
        }

        // Валидация формата email
        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Укажите адрес электронной почты.";
            isValid = false;
        }
        else if (!Regex.IsMatch(Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            EmailError = "Некорректный формат email (ожидается формат user@domain.com).";
            isValid = false;
        }

        // Валидация ФИО
        if (string.IsNullOrWhiteSpace(FullName) || FullName.Trim().Length < 3)
        {
            FullNameError = "Укажите полное имя водителя (не менее 3 символов).";
            isValid = false;
        }

        // Валидация госномера автомобиля
        if (string.IsNullOrWhiteSpace(VehicleNumber) || VehicleNumber.Trim().Length < 4)
        {
            VehicleNumberError = "Укажите госномер автомобиля (не менее 4 символов).";
            isValid = false;
        }

        // Валидация телефона
        if (string.IsNullOrWhiteSpace(Phone) || Phone.Trim().Length < 7)
        {
            PhoneError = "Укажите номер контактного телефона (не менее 7 цифр).";
            isValid = false;
        }

        // Валидация пароля (минимум 4 символа согласно ТЗ)
        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 4)
        {
            PasswordError = "Пароль должен содержать не менее 4 символов.";
            isValid = false;
        }

        // Валидация подтверждения пароля
        if (Password != ConfirmPassword)
        {
            ConfirmPasswordError = "Пароли не совпадают. Повторите ввод.";
            isValid = false;
        }

        if (!isValid)
        {
            ErrorMessage = "Обнаружены ошибки ввода. Проверьте подсвеченные поля.";
        }

        return isValid;
    }

    /// <summary>
    /// Лабораторная работа 18 (Требование 2 &amp; 3):
    /// При успешной регистрации данные отправляются на бэкенд, а в локальной SQLite
    /// создается пустая структура данных под нового пользователя.
    /// Токен доступа шифруется в SecureStorage, сбрасывается стек навигации и выполняется переход на рабочий экран.
    /// </summary>
    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (!ValidateAll())
        {
            await _nativeFeedbackService.TriggerWarningFeedbackAsync();
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Отправка регистрационных данных на сервер...";
        var token = CreateLinkedToken(TimeSpan.FromSeconds(15));

        try
        {
            var req = new RegisterRequest
            {
                DriverId = DriverId.Trim().ToUpperInvariant(),
                Email = Email.Trim().ToLowerInvariant(),
                DriverName = FullName.Trim(),
                VehicleNumber = VehicleNumber.Trim(),
                Phone = Phone.Trim(),
                Password = Password.Trim()
            };

            // 1. Отправка регистрационных данных на бэкенд (Требование 2)
            var result = await _apiService.RegisterAsync(req, token);

            if (result.Success && !string.IsNullOrEmpty(result.Token))
            {
                StatusMessage = "Учетная запись создана. Вход в систему...";

                // 2. В локальной SQLite создается пустая структура данных под нового пользователя (Требование 2)
                var newProfile = new UserProfileEntity
                {
                    DriverId = req.DriverId,
                    Email = req.Email,
                    FullName = req.DriverName,
                    VehicleNumber = req.VehicleNumber,
                    Phone = req.Phone,
                    IsSessionActive = true
                };
                await _databaseService.CreateEmptyUserStructureAsync(newProfile, req.Password);

                // 3. Токен доступа и реквизиты шифруются в SecureStorage (Требование 3)
                await _secureStorageService.SaveTokenAsync(result.Token);
                await _secureStorageService.SaveDriverIdAsync(result.DriverId);
                await _secureStorageService.SaveDriverNameAsync(result.DriverName);
                await _secureStorageService.SaveVehicleNumberAsync(result.VehicleNumber);

                // 4. Запуск фоновой службы телеметрии
                _telemetryService.Start(result.DriverId);

                // 5. Тактильный и звуковой отклик
                await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
                await _nativeFeedbackService.ShowNativeToastAsync($"Регистрация завершена! Добро пожаловать, {result.DriverName}!");

                // 6. Сброс стека навигации и переход на главный рабочий экран (Требование 3)
                await Shell.Current.GoToAsync("//DeliveriesPage");
            }
            else
            {
                await _nativeFeedbackService.TriggerWarningFeedbackAsync();
                ErrorMessage = result.ErrorMessage ?? "Ошибка регистрации. Проверьте введенные данные.";
                StatusMessage = null;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Регистрация отменена пользователем.";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            await _nativeFeedbackService.TriggerWarningFeedbackAsync();
            ErrorMessage = $"Ошибка сетевого взаимодействия: {ex.Message}";
            StatusMessage = null;
        }
        finally
        {
            IsBusy = false;
            ResetCancellation();
        }
    }

    /// <summary>
    /// Переход обратно на экран авторизации (Login View)
    /// </summary>
    [RelayCommand]
    private async Task NavigateToLoginAsync()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
