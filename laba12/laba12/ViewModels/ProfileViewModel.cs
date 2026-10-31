using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using laba12.Models;
using laba12.Services;

namespace laba12.ViewModels;

/// <summary>
/// Лабораторная работа 18 (Требование 4): ViewModel экрана профиля / личного кабинета (Profile View).
/// Реализует отображение профиля водителя, статуса активной сессии в SQLite,
/// связку путевого листа смены (Вариант 7) и атомарную процедуру выхода (Logout) с остановкой телеметрии.
/// </summary>
public partial class ProfileViewModel : BaseViewModel
{
    private readonly ILocalDatabaseService _databaseService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ITelemetryService _telemetryService;
    private readonly INativeFeedbackService _nativeFeedbackService;

    [ObservableProperty]
    private string driverId = string.Empty;

    [ObservableProperty]
    private string fullName = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string phone = string.Empty;

    [ObservableProperty]
    private string vehicleNumber = string.Empty;

    [ObservableProperty]
    private string passwordHashPreview = string.Empty;

    [ObservableProperty]
    private bool isSessionActive = true;

    [ObservableProperty]
    private string sessionStatusText = "АКТИВНА";

    [ObservableProperty]
    private Color sessionStatusColor = Color.FromArgb("#10B981");

    [ObservableProperty]
    private string waybillNumber = string.Empty;

    [ObservableProperty]
    private string waybillStatus = "Активен";

    [ObservableProperty]
    private double odometerKm;

    [ObservableProperty]
    private double fuelRemainingLiters;

    [ObservableProperty]
    private string tokenPreview = string.Empty;

    [ObservableProperty]
    private string databasePath = string.Empty;

    [ObservableProperty]
    private string lastLoginFormatted = string.Empty;

    public ProfileViewModel(
        ILocalDatabaseService databaseService,
        ISecureStorageService secureStorageService,
        ITelemetryService telemetryService,
        INativeFeedbackService nativeFeedbackService)
    {
        _databaseService = databaseService;
        _secureStorageService = secureStorageService;
        _telemetryService = telemetryService;
        _nativeFeedbackService = nativeFeedbackService;
        Title = "Личный кабинет водителя";

        _ = LoadProfileAsync();
    }

    [RelayCommand]
    public async Task LoadProfileAsync()
    {
        IsBusy = true;
        try
        {
            DatabasePath = _databaseService.DatabasePath;

            var currentDriverId = await _secureStorageService.GetDriverIdAsync();
            var profile = await _databaseService.GetUserProfileAsync(currentDriverId ?? string.Empty);

            if (profile != null)
            {
                DriverId = profile.DriverId;
                FullName = profile.FullName;
                Email = !string.IsNullOrEmpty(profile.Email) ? profile.Email : "Не указан";
                Phone = !string.IsNullOrEmpty(profile.Phone) ? profile.Phone : "Не указан";
                VehicleNumber = profile.VehicleNumber;
                IsSessionActive = profile.IsSessionActive;
                SessionStatusText = profile.IsSessionActive ? "АКТИВНА" : "ЗАВЕРШЕНА";
                SessionStatusColor = profile.IsSessionActive ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
                PasswordHashPreview = !string.IsNullOrEmpty(profile.PasswordHash)
                    ? $"{profile.PasswordHash[..Math.Min(16, profile.PasswordHash.Length)]}..."
                    : "Хэш не сохранен";
                LastLoginFormatted = profile.LastLoginAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "Первый вход";
            }
            else
            {
                DriverId = currentDriverId ?? "DRV-104";
                FullName = await _secureStorageService.GetDriverNameAsync() ?? "Водитель";
                VehicleNumber = await _secureStorageService.GetVehicleNumberAsync() ?? "Не назначен";
                SessionStatusText = "АВТОНОМНО";
                SessionStatusColor = Color.FromArgb("#F59E0B");
            }

            // Загрузка путевого листа смены, привязанного к водителю (Вариант 7)
            var waybill = await _databaseService.GetOrCreateWaybillAsync(DriverId, FullName, VehicleNumber);
            if (waybill != null)
            {
                WaybillNumber = waybill.WaybillNumber;
                WaybillStatus = waybill.Status;
                OdometerKm = waybill.CurrentOdometerKm;
                FuelRemainingLiters = waybill.FuelRemainingLiters;
            }

            // Загрузка зашифрованного токена из SecureStorage
            var token = await _secureStorageService.GetTokenAsync();
            TokenPreview = !string.IsNullOrEmpty(token)
                ? $"{token[..Math.Min(24, token.Length)]}... [SecureStorage]"
                : "Отсутствует (Офлайн-режим)";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка загрузки профиля: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Лабораторная работа 18 (Требование 4) + Вариант 7:
    /// Кнопка «Выйти из аккаунта» (Logout):
    /// 1. Принудительно останавливает фоновый сбор телеметрии и запись координат в базу данных (Вариант 7).
    /// 2. Атомарно очищает токены в SecureStorage (Требование 4).
    /// 3. Помечает сессию в SQLite как неактивную (Требование 4).
    /// 4. Возвращает приложение на стартовый экран ввода данных с полной блокировкой основного контента.
    /// </summary>
    [RelayCommand]
    public async Task LogoutAsync()
    {
        IsBusy = true;
        try
        {
            // 1. ВАРИАНТ 7: При выходе из системы фоновый сбор телеметрии и запись координат поездки в БД принудительно останавливаются
            _telemetryService.Stop();

            // 2. ТРЕБОВАНИЕ 4: Атомарная очистка токенов в SecureStorage
            await _secureStorageService.ClearAuthAsync();

            // 3. ТРЕБОВАНИЕ 4: Помечает сессию в SQLite как неактивную
            await _databaseService.DeactivateUserSessionAsync(DriverId);

            // 4. Нативный виброотклик и тост
            await _nativeFeedbackService.TriggerClickFeedbackAsync();
            await _nativeFeedbackService.ShowNativeToastAsync("Выход из аккаунта выполнен. Сессия завершена.");

            // 5. ТРЕБОВАНИЕ 4: Сброс стека навигации и блокировка основного контента (возврат на Login Page)
            await Shell.Current.GoToAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка при выходе из системы: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task NavigateBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
