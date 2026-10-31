using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using laba12.Models;
using laba12.Services;

namespace laba12.ViewModels;

public partial class DeliveryDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ILogisticsApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly ILocalDatabaseService _databaseService;
    private readonly INativeFeedbackService _nativeFeedbackService;

    [ObservableProperty]
    private DeliveryPoint? delivery;

    [ObservableProperty]
    private string selectedStatus = "Ожидает";

    [ObservableProperty]
    private string notesText = string.Empty;

    [ObservableProperty]
    private string? statusFeedback;

    [ObservableProperty]
    private bool isSuccessNotice;

    // Вариант 7: Свойства справочника требований к грузу и агрегированных данных из СУБД (JOIN)
    [ObservableProperty]
    private string categoryOrdersSummary = "Загрузка данных категории...";

    [ObservableProperty]
    private string categoryAggregatedShiftStats = string.Empty;

    public List<string> AvailableStatuses { get; } = new()
    {
        "Ожидает",
        "В пути",
        "Доставлен",
        "Отказ"
    };

    public DeliveryDetailViewModel(
        ILogisticsApiService apiService,
        ICacheService cacheService,
        ISecureStorageService secureStorageService,
        ILocalDatabaseService databaseService,
        INativeFeedbackService nativeFeedbackService)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _secureStorageService = secureStorageService;
        _databaseService = databaseService;
        _nativeFeedbackService = nativeFeedbackService;
        Title = "Детали заказа и груза";
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("DeliveryPoint", out var pointObj) && pointObj is DeliveryPoint point)
        {
            Delivery = point;
            SelectedStatus = point.Status;
            NotesText = point.Notes ?? string.Empty;
            Title = $"Заказ {point.OrderNumber}";

            await LoadCargoRegulationsAndAggregatesAsync(point);
        }
    }

    /// <summary>
    /// Вариант 7: Выполнение связывающего JOIN-запроса к таблице cargo_categories
    /// и агрегирующего запроса по смене водителя без вложенных циклов в C#.
    /// </summary>
    private async Task LoadCargoRegulationsAndAggregatesAsync(DeliveryPoint point)
    {
        try
        {
            var driverId = await _secureStorageService.GetDriverIdAsync() ?? "DRV-104";

            // 1. Извлечение полных данных ограничений груза через SQL JOIN
            var detail = await _databaseService.GetDeliveryPointRegulationDetailAsync(point.Id);
            if (detail != null)
            {
                point.CargoCategoryCode = detail.CargoCategoryCode;
                point.CargoCategoryName = detail.CargoCategoryName;
                point.CargoTemperatureMode = detail.CargoTemperatureMode;
                point.CargoHumidityAndVentilation = detail.CargoHumidityAndVentilation;
                point.CargoHandlingRequirements = detail.CargoHandlingRequirements;
                point.CargoSafetyClass = detail.CargoSafetyClass;
                point.CargoRequiresSeal = detail.CargoRequiresSeal;
                point.CargoIconGlyph = detail.CargoIconGlyph;
            }

            // 2. Извлечение агрегированных данных смены по данной категории через сложный JOIN
            var reports = await _databaseService.GetCargoAggregatedReportAsync(driverId);
            var catReport = reports.FirstOrDefault(x => x.CategoryId == point.CargoCategoryId);
            if (catReport != null)
            {
                CategoryOrdersSummary = $"{catReport.TotalOrdersCount} наряда в смене • Суммарная масса: {catReport.TotalWeightKg:F1} кг ({catReport.TotalVolumeM3:F2} м³)";
                CategoryAggregatedShiftStats = $"Рейсы по категории в смене: {catReport.TotalDistanceKm:F1} км (израсходовано {catReport.TotalFuelLiters:F2} л топлива)";
            }
            else
            {
                CategoryOrdersSummary = $"Категория: {point.CargoCategoryName}";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveryDetailViewModel] Ошибка загрузки данных JOIN: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SaveChangesAsync()
    {
        if (Delivery == null) return;

        IsBusy = true;
        StatusFeedback = null;
        ErrorMessage = null;
        var token = CreateLinkedToken(TimeSpan.FromSeconds(10));

        // Create updated copy
        Delivery.Status = SelectedStatus;
        Delivery.Notes = NotesText;
        Delivery.UpdatedAt = DateTime.UtcNow;

        var driverId = await _secureStorageService.GetDriverIdAsync() ?? "DRV-104";
        bool isOffline = false;

        try
        {
            // Attempt to update via REST API
            var updated = await _apiService.UpdateDeliveryStatusAsync(Delivery.Id, Delivery, token);

            if (updated != null)
            {
                Delivery.Status = updated.Status;
                Delivery.Notes = updated.Notes;
            }

            await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
            await _nativeFeedbackService.ShowNativeToastAsync("Изменения наряда успешно сохранены!");
            IsSuccessNotice = true;
            StatusFeedback = "Изменения успешно сохранены!";
        }
        catch (OperationCanceledException)
        {
            await _nativeFeedbackService.TriggerWarningFeedbackAsync();
            ErrorMessage = "Сохранение изменений отменено пользователем.";
            IsSuccessNotice = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveryDetailViewModel] Update failed: {ex.Message}. Сохранение в локальную SQLite.");
            isOffline = true;
            IsSuccessNotice = true;
            StatusFeedback = "Автономный режим: изменения сохранены на устройстве и будут отправлены при подключении.";
        }
        finally
        {
            // Атомарное сохранение в локальную SQLite базу
            if (Delivery.Status == "Прибыл")
            {
                await _databaseService.MarkArrivalAsync(Delivery.Id, Delivery.UpdatedAt, isOffline);
            }
            else if (Delivery.Status == "Отказ")
            {
                await _databaseService.MarkRejectAsync(Delivery.Id, Delivery.Notes, isOffline);
            }
            else
            {
                await _databaseService.SaveOrUpdateDeliveriesFromApiAsync(driverId, new[] { Delivery });
            }

            // Always update local cache in AppDataDirectory
            await _cacheService.UpdateCachedPointAsync(driverId, Delivery);

            IsBusy = false;
            ResetCancellation();
        }
    }

    [RelayCommand]
    public async Task CallClientAsync()
    {
        if (Delivery == null || string.IsNullOrWhiteSpace(Delivery.Phone)) return;

        try
        {
            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(Delivery.Phone);
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Звонок", $"Номер телефона клиента: {Delivery.Phone}", "OK");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось запустить набор номера: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task OpenExternalMapAsync()
    {
        if (Delivery == null) return;

        try
        {
            var location = new Location(Delivery.Latitude, Delivery.Longitude);
            var options = new MapLaunchOptions { Name = $"{Delivery.OrderNumber} - {Delivery.ClientName}" };
            await Map.Default.OpenAsync(location, options);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось открыть внешние карты: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
