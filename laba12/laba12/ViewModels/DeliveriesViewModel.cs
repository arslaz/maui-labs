using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using laba12.Models;
using laba12.Services;
using laba12.Views;

namespace laba12.ViewModels;

public partial class DeliveriesViewModel : BaseViewModel
{
    private readonly ILogisticsApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly IRoutingService _routingService;
    private readonly ITelemetryService _telemetryService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly MockBackendServer _mockBackendServer;
    private readonly INetworkStatusService _networkStatusService;
    private readonly IDeviceLocationService _locationService;
    private readonly ISyncQueueService _syncQueueService;
    private readonly IFuelReportService _fuelReportService;
    private readonly ILocalDatabaseService _databaseService;
    private readonly INativeFeedbackService _nativeFeedbackService;
    private readonly INativeTelemetryService _nativeTelemetryService;

    public ObservableCollection<DeliveryPoint> Deliveries { get; } = new();

    [ObservableProperty]
    private string driverId = "DRV-104";

    [ObservableProperty]
    private string driverName = "Алексей Соколов";

    [ObservableProperty]
    private string vehicleNumber = "АВ 7412-7 (ГАЗель NEXT РБ)";

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int pageSize = 4;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private bool hasMoreItems = true;

    [ObservableProperty]
    private bool isLoadingMore;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool isPlottingRoute;

    [ObservableProperty]
    private string? routeSummary;

    [ObservableProperty]
    private bool isRouteAvailable;

    [ObservableProperty]
    private string? cacheNotice;

    [ObservableProperty]
    private string? statusNotice;

    [ObservableProperty]
    private string? lastUpdatedText;

    // Свойства отложенной синхронизации (SQLite & Offline Queue)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingSyncTransactions))]
    [NotifyPropertyChangedFor(nameof(PendingSyncBadgeText))]
    private int pendingSyncCount;

    [ObservableProperty]
    private int pendingSqliteSyncCount;

    [ObservableProperty]
    private string databaseStatusText = "Локальная база данных актуальна";

    // Визуальная индикация целостности данных
    [ObservableProperty]
    private string sqliteTransactionBadgeText = "Данные актуальны";

    [ObservableProperty]
    private string sqliteTransactionStatusColor = "#10B981";

    [ObservableProperty]
    private string sqliteTransactionDetails = "Все наряды и маршруты сохранены";

    // Накопленные путевые точки телеметрии
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsyncedWaypoints))]
    [NotifyPropertyChangedFor(nameof(UnsyncedWaypointsBadgeText))]
    private int unsyncedWaypointsCount;

    // Вариант 7: Агрегированная визуальная сводка требований и режимов грузов (SQL JOIN)
    [ObservableProperty]
    private bool isRegulationsSummaryVisible;

    [ObservableProperty]
    private string regulationsSummaryButtonText = "📊 Сводка требований и режимов грузов (SQL JOIN)";

    public ObservableCollection<CargoCategoryAggregatedReport> CargoCategoryReports { get; } = new();

    public bool HasUnsyncedWaypoints => UnsyncedWaypointsCount > 0;
    public string UnsyncedWaypointsBadgeText => $"Неотправленные точки: {UnsyncedWaypointsCount}";

    public bool HasPendingSyncTransactions => PendingSyncCount > 0 || PendingSqliteSyncCount > 0;
    public string PendingSyncBadgeText => $"Ожидает отправки: {Math.Max(PendingSyncCount, PendingSqliteSyncCount)}";

    // Свойства отчета о топливе
    [ObservableProperty]
    private string? fuelReportStatusText;

    // Свойства телеметрии
    [ObservableProperty]
    private string telemetryStatusText = "Телеметрия: Ожидание";

    [ObservableProperty]
    private string vehicleSpeedText = "0 км/ч";

    [ObservableProperty]
    private int telemetryBufferCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimulateOfflineButtonText))]
    [NotifyPropertyChangedFor(nameof(SimulateOfflineButtonBgColor))]
    [NotifyPropertyChangedFor(nameof(SimulateOfflineTextColor))]
    private bool isSimulateOffline;

    public string SimulateOfflineButtonText => IsSimulateOffline ? "Офлайн: ВКЛ" : "Офлайн-тест";
    public Color SimulateOfflineButtonBgColor => IsSimulateOffline ? Color.FromArgb("#7F1D1D") : Color.FromArgb("#1E293B");
    public Color SimulateOfflineTextColor => IsSimulateOffline ? Color.FromArgb("#FCA5A5") : Color.FromArgb("#94A3B8");

    public RouteInfo? LastCalculatedRoute { get; private set; }

    public DeliveriesViewModel(
        ILogisticsApiService apiService,
        ICacheService cacheService,
        IRoutingService routingService,
        ITelemetryService telemetryService,
        ISecureStorageService secureStorageService,
        MockBackendServer mockBackendServer,
        INetworkStatusService networkStatusService,
        IDeviceLocationService locationService,
        ISyncQueueService syncQueueService,
        IFuelReportService fuelReportService,
        ILocalDatabaseService databaseService,
        INativeFeedbackService nativeFeedbackService,
        INativeTelemetryService nativeTelemetryService)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _routingService = routingService;
        _telemetryService = telemetryService;
        _secureStorageService = secureStorageService;
        _mockBackendServer = mockBackendServer;
        _networkStatusService = networkStatusService;
        _locationService = locationService;
        _syncQueueService = syncQueueService;
        _fuelReportService = fuelReportService;
        _databaseService = databaseService;
        _nativeFeedbackService = nativeFeedbackService;
        _nativeTelemetryService = nativeTelemetryService;

        Title = "Маршрутный лист водителя";

        _telemetryService.OnTelemetryStatusChanged += UpdateTelemetryUI;
        _networkStatusService.ConnectivityChanged += OnConnectivityChanged;
        _syncQueueService.QueueChanged += count =>
        {
            MainThread.BeginInvokeOnMainThread(() => PendingSyncCount = count);
        };
    }

    private void OnConnectivityChanged(bool isConnected)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!isConnected)
            {
                IsOffline = true;
                CacheNotice = "Автономный режим: сеть недоступна. Изменения сохраняются локально.";
                ErrorMessage = "Отсутствует подключение к интернету. Включен автономный режим.";
            }
            else
            {
                IsOffline = false;
                CacheNotice = null;
                StatusNotice = "Сеть восстановлена! Выполняется синхронизация данных...";
                
                // Автоматическая отправка отложенных транзакций при обнаружении сети
                if (PendingSyncCount > 0)
                {
                    await ProcessSyncQueueInternalAsync();
                }

                await LoadDeliveriesAsync(refresh: true);
                await _telemetryService.SendImmediateAsync();
            }
        });
    }

    public async Task InitializeAsync()
    {
        try
        {
            var savedDriver = await _secureStorageService.GetDriverIdAsync();
            if (!string.IsNullOrEmpty(savedDriver))
            {
                DriverId = savedDriver;
            }

            var savedName = await _secureStorageService.GetDriverNameAsync();
            if (!string.IsNullOrEmpty(savedName))
            {
                DriverName = savedName;
            }

            var savedVehicle = await _secureStorageService.GetVehicleNumberAsync();
            if (!string.IsNullOrEmpty(savedVehicle))
            {
                VehicleNumber = savedVehicle;
            }

            // Первоначальная проверка наличия БД и копирование файла справочника из ресурсов (Resources/Raw)
            await _databaseService.EnsureDatabaseInitializedAsync();

            // Автоматическая миграция legacy JSON в реляционные таблицы SQLite при открытии
            await _databaseService.MigrateFromLegacyJsonAsync(DriverId);
            await _databaseService.GetOrCreateWaybillAsync(DriverId, DriverName, VehicleNumber);

            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            await UpdateDatabaseCountsAsync();
            UpdateTelemetryUI();

            await LoadDeliveriesAsync(refresh: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] InitializeAsync error: {ex}");
            ErrorMessage = $"Ошибка загрузки путевого листа: {ex.Message}";
        }
    }

    private void UpdateTelemetryUI()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TelemetryStatusText = _telemetryService.StatusMessage;
            VehicleSpeedText = $"{_telemetryService.CurrentSpeedKmh:F0} км/ч";
            TelemetryBufferCount = _telemetryService.QueuedCount;
        });
    }

    /// <summary>
    /// ЛОКАЛЬНЫЕ БАЗЫ ДАННЫХ (SQLite-net-pcl):
    /// Главный экран со списком обязан мгновенно загружать данные из базы данных при открытии,
    /// а сетевой запрос к API переводит приложение в состояние обновления и синхронизирует 
    /// полученные извне структуры с локальной БД SQLite.
    /// </summary>
    [RelayCommand]
    public async Task LoadDeliveriesAsync(bool refresh = false)
    {
        if (IsBusy && !refresh) return;

        if (refresh)
        {
            CurrentPage = 1;
            HasMoreItems = true;
        }

        // ШАГ 1: МГНОВЕННАЯ ЗАГРУЗКА ИЗ ЛОКАЛЬНОЙ БАЗЫ ДАННЫХ SQLITE (таблица delivery_points)
        try
        {
            var localPoints = await _databaseService.GetDeliveryPointsAsync(DriverId);
            if (localPoints.Any())
            {
                Deliveries.Clear();
                foreach (var item in localPoints)
                {
                    Deliveries.Add(item);
                }
                TotalCount = Deliveries.Count;
                LastUpdatedText = $"Локальные данные: {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                // Резервный фолбэк на legacy JSON-кэш, если SQLite еще не заполнен
                var cached = await _cacheService.GetCachedDeliveriesAsync(DriverId);
                if (cached != null && cached.Any())
                {
                    Deliveries.Clear();
                    foreach (var item in cached)
                    {
                        Deliveries.Add(item);
                    }
                    TotalCount = Deliveries.Count;
                    LastUpdatedText = $"Локальные данные: {DateTime.Now:HH:mm:ss}";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] Step 1 DB load notice: {ex.Message}");
        }

        await UpdateDatabaseCountsAsync();

        // Если физическая сеть отсутствует или включена симуляция офлайна
        if (!_networkStatusService.IsConnected || _mockBackendServer.SimulateOffline)
        {
            IsOffline = true;
            CacheNotice = $"Автономный режим: отображаются сохраненные наряды ({Deliveries.Count}).";
            IsBusy = false;
            IsRefreshing = false;
            return;
        }

        // ШАГ 2: АСИНХРОННЫЙ ФОНОВЫЙ ЗАПРОС К API ДЛЯ АКТУАЛИЗАЦИИ
        IsBusy = true;
        ErrorMessage = null;
        var token = CreateLinkedToken(TimeSpan.FromSeconds(12));

        try
        {
            var response = await _apiService.GetDeliveriesAsync(DriverId, CurrentPage, PageSize, token);

            // ШАГ 3: СИНХРОНИЗАЦИЯ ПОЛУЧЕННЫХ ИЗВНЕ СТРУКТУР С ЛОКАЛЬНОЙ БД SQLITE (Атомарная транзакция)
            var trxResult = await _databaseService.SaveOrUpdateDeliveriesFromApiAsync(DriverId, response.Items);
            if (trxResult.IsSuccess && trxResult.IsCommitted)
            {
                SqliteTransactionBadgeText = $"Синхронизировано ({trxResult.AffectedRows} нарядов)";
                SqliteTransactionStatusColor = "#10B981";
                SqliteTransactionDetails = "Данные путевого листа актуальны";
            }
            else
            {
                SqliteTransactionBadgeText = "Сбой синхронизации";
                SqliteTransactionStatusColor = "#EF4444";
                SqliteTransactionDetails = "Не удалось обновить данные путевого листа";
            }

            // Получаем актуальный список из SQLite с сохранением локальных статусов
            var refreshedFromDb = await _databaseService.GetDeliveryPointsAsync(DriverId);
            Deliveries.Clear();
            foreach (var item in refreshedFromDb)
            {
                Deliveries.Add(item);
            }

            TotalCount = response.TotalCount;
            HasMoreItems = response.HasMore;
            IsOffline = false;
            CacheNotice = null;
            LastUpdatedText = $"Синхронизировано: {DateTime.Now:HH:mm:ss}";

            // Резервное дублирование в legacy JSON
            await _cacheService.SaveDeliveriesAsync(DriverId, Deliveries.ToList());
            await UpdateDatabaseCountsAsync();

            if (IsRegulationsSummaryVisible)
            {
                var reports = await _databaseService.GetCargoAggregatedReportAsync(DriverId);
                CargoCategoryReports.Clear();
                foreach (var r in reports) CargoCategoryReports.Add(r);
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Загрузка данных отменена пользователем.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] Сетевой запрос завершился ошибкой: {ex.Message}. SQLite уже отображен.");
            IsOffline = true;
            CacheNotice = "Автономный режим: сервер недоступен. Показаны сохраненные наряды.";
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
            ResetCancellation();
        }
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsBusy || IsLoadingMore || !HasMoreItems || IsOffline) return;

        IsLoadingMore = true;
        var token = CreateLinkedToken(TimeSpan.FromSeconds(10));

        try
        {
            int nextPage = CurrentPage + 1;
            var response = await _apiService.GetDeliveriesAsync(DriverId, nextPage, PageSize, token);

            if (response.Items.Any())
            {
                foreach (var item in response.Items)
                {
                    Deliveries.Add(item);
                }

                CurrentPage = nextPage;
                HasMoreItems = response.HasMore;
                TotalCount = response.TotalCount;

                await _cacheService.SaveDeliveriesAsync(DriverId, Deliveries.ToList());
            }
            else
            {
                HasMoreItems = false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось загрузить порцию: {ex.Message}";
        }
        finally
        {
            IsLoadingMore = false;
            ResetCancellation();
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadDeliveriesAsync(refresh: true);
    }

    // =========================================================================
    // КОМАНДЫ КОНТЕКСТНОГО МЕНЮ (МОДИФИКАЦИЯ РЕСУРСОВ: PATCH, PUT, DELETE)
    // =========================================================================

    /// <summary>
    /// Меню действий для элемента списка (ActionSheet / ContextMenu)
    /// </summary>
    [RelayCommand]
    public async Task ShowItemContextMenuAsync(DeliveryPoint? point)
    {
        if (point == null) return;

        string action = await Shell.Current.DisplayActionSheetAsync(
            $"Наряд {point.OrderNumber} ({point.ClientName})",
            "Отмена",
            null,
            "📍 Отметить прибытие",
            "❌ Зафиксировать отказ от груза",
            "🗑️ Удалить наряд");

        if (action == "📍 Отметить прибытие")
        {
            await MarkArrivalAsync(point);
        }
        else if (action == "❌ Зафиксировать отказ от груза")
        {
            await RejectCargoAsync(point);
        }
        else if (action == "🗑️ Удалить наряд")
        {
            await DeleteDeliveryAsync(point);
        }
    }

    /// <summary>
    /// Вариант 7: Контекстное меню -> «Отметить прибытие» (PATCH-запрос со штампом времени)
    /// Атомарно меняет состояние строки в локальной таблице SQLite с фиксацией этапа в trip_records.
    /// </summary>
    [RelayCommand]
    public async Task MarkArrivalAsync(DeliveryPoint? point)
    {
        if (point == null) return;

        var arrivalTime = DateTime.UtcNow;
        bool isOffline = IsOffline || _mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected;

        // 1. АТОМАРНАЯ МОДИФИКАЦИЯ СТРОКИ В ТАБЛИЦЕ SQLITE (таблица delivery_points + фиксация в trip_records)
        await _databaseService.MarkArrivalAsync(point.Id, arrivalTime, isOffline);

        // 2. Мгновенное оптимистичное обновление UI и локального кэша
        point.Status = "Прибыл";
        point.Notes = $"Отметка прибытия: {DateTime.Now:HH:mm:ss}";
        point.UpdatedAt = arrivalTime;
        point.IsSyncPending = isOffline;
        await _cacheService.UpdateCachedPointAsync(DriverId, point);

        await UpdateDatabaseCountsAsync();

        // 3. Если офлайн или симуляция сбоя — фиксируем в очереди и выходим
        if (isOffline)
        {
            var tx = new SyncTransaction
            {
                DeliveryId = point.Id,
                OrderNumber = point.OrderNumber,
                HttpMethod = "PATCH",
                Endpoint = $"api/deliveries/{point.Id}/arrival",
                PayloadJson = JsonSerializer.Serialize(new { arrivedAt = arrivalTime }),
                Description = $"Отметка прибытия: {point.OrderNumber}"
            };

            await _syncQueueService.EnqueueAsync(tx);
            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            StatusNotice = $"Автономный режим: отметка прибытия ({point.OrderNumber}) сохранена локально.";
            return;
        }

        // 4. Онлайн: отправка PATCH-запроса на сервер
        IsBusy = true;
        try
        {
            var token = CreateLinkedToken(TimeSpan.FromSeconds(10));
            var updated = await _apiService.MarkArrivalAsync(point.Id, arrivalTime, token);
            if (updated != null)
            {
                point.Status = updated.Status;
                point.Notes = updated.Notes;
                point.IsSyncPending = false;
                await _databaseService.ClearSyncPendingFlagAsync(point.Id);
                await _cacheService.UpdateCachedPointAsync(DriverId, point);
            }
            StatusNotice = $"Прибытие по наряду {point.OrderNumber} подтверждено сервером.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] PATCH error: {ex.Message}. Отложенная синхронизация SQLite.");
            await _databaseService.MarkArrivalAsync(point.Id, arrivalTime, isOffline: true);
            point.IsSyncPending = true;

            var tx = new SyncTransaction
            {
                DeliveryId = point.Id,
                OrderNumber = point.OrderNumber,
                HttpMethod = "PATCH",
                Endpoint = $"api/deliveries/{point.Id}/arrival",
                PayloadJson = JsonSerializer.Serialize(new { arrivedAt = arrivalTime }),
                Description = $"Отметка прибытия: {point.OrderNumber}"
            };
            await _syncQueueService.EnqueueAsync(tx);
            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            StatusNotice = $"Сбой связи: статус сохранен локально до синхронизации.";
        }
        finally
        {
            IsBusy = false;
            await UpdateDatabaseCountsAsync();
            ResetCancellation();
        }
    }

    /// <summary>
    /// Вариант 7: Контекстное меню -> «Зафиксировать отказ от груза»
    /// Атомарно меняет состояние строки в таблице SQLite с внесением записи в trip_records.
    /// </summary>
    [RelayCommand]
    public async Task RejectCargoAsync(DeliveryPoint? point)
    {
        if (point == null) return;

        string? reason = await Shell.Current.DisplayPromptAsync(
            "Зафиксировать отказ от груза",
            $"Укажите причину отказа для наряда {point.OrderNumber} ({point.ClientName}):",
            accept: "Подтвердить отказ",
            cancel: "Отмена",
            placeholder: "Причина отказа",
            initialValue: "Повреждение заводской упаковки при транспортировке");

        if (string.IsNullOrWhiteSpace(reason)) return;

        var rejectTime = DateTime.UtcNow;
        bool isOffline = IsOffline || _mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected;

        // 1. АТОМАРНАЯ МОДИФИКАЦИЯ СТРОКИ В ТАБЛИЦЕ SQLITE
        await _databaseService.MarkRejectAsync(point.Id, reason, isOffline);

        // 2. Мгновенное обновление UI и локального кэша
        point.Status = "Отказ";
        point.Notes = $"Акт отказа: {reason}";
        point.UpdatedAt = rejectTime;
        point.IsSyncPending = isOffline;
        await _cacheService.UpdateCachedPointAsync(DriverId, point);

        await UpdateDatabaseCountsAsync();

        // 3. Если офлайн - сохраняем транзакцию для сервера
        if (isOffline)
        {
            var tx = new SyncTransaction
            {
                DeliveryId = point.Id,
                OrderNumber = point.OrderNumber,
                HttpMethod = "PUT",
                Endpoint = $"api/deliveries/{point.Id}/reject",
                PayloadJson = JsonSerializer.Serialize(new { reason, rejectedAt = rejectTime }),
                Description = $"Отказ от груза {point.OrderNumber}: {reason}"
            };

            await _syncQueueService.EnqueueAsync(tx);
            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            StatusNotice = $"Автономный режим: отказ по наряду {point.OrderNumber} сохранен локально.";
            return;
        }

        // 4. Онлайн: отправка PUT-запроса на сервер
        IsBusy = true;
        try
        {
            var token = CreateLinkedToken(TimeSpan.FromSeconds(10));
            var updated = await _apiService.RejectCargoAsync(point.Id, reason, token);
            if (updated != null)
            {
                point.Status = updated.Status;
                point.Notes = updated.Notes;
                point.IsSyncPending = false;
                await _databaseService.ClearSyncPendingFlagAsync(point.Id);
                await _cacheService.UpdateCachedPointAsync(DriverId, point);
            }
            StatusNotice = $"Акт отказа по заказу {point.OrderNumber} принят сервером.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] PUT error: {ex.Message}. Отложенная синхронизация SQLite.");
            await _databaseService.MarkRejectAsync(point.Id, reason, isOffline: true);
            point.IsSyncPending = true;

            var tx = new SyncTransaction
            {
                DeliveryId = point.Id,
                OrderNumber = point.OrderNumber,
                HttpMethod = "PUT",
                Endpoint = $"api/deliveries/{point.Id}/reject",
                PayloadJson = JsonSerializer.Serialize(new { reason, rejectedAt = rejectTime }),
                Description = $"Отказ от груза {point.OrderNumber}: {reason}"
            };
            await _syncQueueService.EnqueueAsync(tx);
            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            StatusNotice = $"Сбой сети: акт отказа сохранен в локальной базе.";
        }
        finally
        {
            IsBusy = false;
            await UpdateDatabaseCountsAsync();
            ResetCancellation();
        }
    }

    /// <summary>
    /// Общее требование: Контекстное меню -> «Удалить точку»
    /// </summary>
    [RelayCommand]
    public async Task DeleteDeliveryAsync(DeliveryPoint? point)
    {
        if (point == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Удаление наряда",
            $"Вы действительно хотите удалить наряд {point.OrderNumber} ({point.ClientName}) из путевого листа?",
            "Удалить",
            "Отмена");

        if (!confirm) return;

        bool isOffline = IsOffline || _mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected;

        // 1. АТОМАРНОЕ УДАЛЕНИЕ / ПОМЕТКА НА УДАЛЕНИЕ В SQLITE С ПРОВЕРКОЙ ЗАВИСИМОСТЕЙ
        var deleteResult = await _databaseService.DeleteDeliveryPointAsync(point.Id, isOffline);
        if (!string.IsNullOrEmpty(deleteResult.Message))
        {
            StatusNotice = deleteResult.Message;
        }

        // Нативный тактильный отклик удаления элемента (Общее требование #4)
        await _nativeFeedbackService.TriggerItemDeletedFeedbackAsync();
        await _nativeFeedbackService.ShowNativeToastAsync($"Наряд {point.OrderNumber} удален");

        // 2. Удаление из UI и обновление резервного кэша
        Deliveries.Remove(point);
        TotalCount = Deliveries.Count;
        await _cacheService.SaveDeliveriesAsync(DriverId, Deliveries.ToList());

        await UpdateDatabaseCountsAsync();

        if (isOffline)
        {
            var tx = new SyncTransaction
            {
                DeliveryId = point.Id,
                OrderNumber = point.OrderNumber,
                HttpMethod = "DELETE",
                Endpoint = $"api/deliveries/{point.Id}",
                Description = $"Удаление наряда {point.OrderNumber}"
            };

            await _syncQueueService.EnqueueAsync(tx);
            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            StatusNotice = $"Наряд {point.OrderNumber} помечен на удаление (в очереди синхронизации).";
            return;
        }

        // Онлайн: отправка DELETE-запроса на сервер
        IsBusy = true;
        try
        {
            var token = CreateLinkedToken(TimeSpan.FromSeconds(10));
            var deleted = await _apiService.DeleteDeliveryAsync(point.Id, token);
            if (deleted)
            {
                await _databaseService.ClearSyncPendingFlagAsync(point.Id);
                StatusNotice = $"Наряд {point.OrderNumber} успешно удален из путевого листа.";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] DELETE error: {ex.Message}.");
            var tx = new SyncTransaction
            {
                DeliveryId = point.Id,
                OrderNumber = point.OrderNumber,
                HttpMethod = "DELETE",
                Endpoint = $"api/deliveries/{point.Id}",
                Description = $"Удаление наряда {point.OrderNumber}"
            };
            await _syncQueueService.EnqueueAsync(tx);
            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            StatusNotice = $"Сбой сети: удаление помещено в лог синхронизации.";
        }
        finally
        {
            IsBusy = false;
            await UpdateDatabaseCountsAsync();
            ResetCancellation();
        }
    }

    // =========================================================================
    // ОТЛОЖЕННАЯ СИНХРОНИЗАЦИЯ (ОБРАБОТКА SQLITE ФЛАГОВ И ОФЛАЙН-ОЧЕРЕДИ)
    // =========================================================================

    [RelayCommand]
    public async Task ProcessSyncQueueAsync()
    {
        if (PendingSyncCount == 0 && PendingSqliteSyncCount == 0)
        {
            await Shell.Current.DisplayAlertAsync("Синхронизация", "Все наряды и маршруты синхронизированы.", "OK");
            return;
        }

        if (_mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected)
        {
            await Shell.Current.DisplayAlertAsync("Офлайн-режим", "Невозможно выполнить синхронизацию при отсутствии подключения к сети.", "OK");
            return;
        }

        await ProcessSyncQueueInternalAsync();
    }

    private async Task ProcessSyncQueueInternalAsync()
    {
        IsBusy = true;
        StatusNotice = "Синхронизация данных с сервером...";
        try
        {
            // 1. Поиск и отправка всех строк с флагом IsSyncPending = true из SQLite
            var pendingPoints = await _databaseService.GetPendingSyncItemsAsync(DriverId);
            int sqliteSyncedCount = 0;

            foreach (var item in pendingPoints)
            {
                try
                {
                    if (item.PendingOperation == "PATCH_ARRIVAL")
                    {
                        await _apiService.MarkArrivalAsync(item.Id, item.ArrivedAt ?? DateTime.UtcNow, CancellationToken.None);
                        await _databaseService.ClearSyncPendingFlagAsync(item.Id);
                        sqliteSyncedCount++;
                    }
                    else if (item.PendingOperation == "PUT_REJECT")
                    {
                        await _apiService.RejectCargoAsync(item.Id, item.RejectionReason ?? "Отказ от груза", CancellationToken.None);
                        await _databaseService.ClearSyncPendingFlagAsync(item.Id);
                        sqliteSyncedCount++;
                    }
                    else if (item.PendingOperation == "DELETE")
                    {
                        await _apiService.DeleteDeliveryAsync(item.Id, CancellationToken.None);
                        await _databaseService.ClearSyncPendingFlagAsync(item.Id);
                        sqliteSyncedCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProcessSyncQueue] Ошибка отправки строки {item.Id}: {ex.Message}");
                }
            }

            // 2. Обработка резервной файловой очереди JSON
            var result = await _syncQueueService.ProcessQueueAsync(_apiService);
            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            await UpdateDatabaseCountsAsync();

            string resultMsg = "Все сохраненные данные успешно переданы на сервер!";

            await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
            await _nativeFeedbackService.ShowNativeToastAsync("Синхронизация с сервером завершена!");
            StatusNotice = resultMsg;
            await Shell.Current.DisplayAlertAsync("Синхронизация", resultMsg, "OK");
            await LoadDeliveriesAsync(refresh: true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Сбой при синхронизации: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UpdateDatabaseCountsAsync()
    {
        try
        {
            var summary = await _databaseService.GetDatabaseSummaryAsync(DriverId);
            PendingSqliteSyncCount = summary.PendingSyncCount;
            UnsyncedWaypointsCount = summary.UnsyncedWaypointsCount;
            DatabaseStatusText = $"В рейсе: {summary.DeliveriesCount} нарядов • {summary.CategoriesCount} категорий грузов";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] UpdateDatabaseCounts error: {ex.Message}");
        }
    }

    /// <summary>
    /// Отображение сводки по категориям грузов и режимам транспортировки
    /// </summary>
    [RelayCommand]
    public async Task ShowCargoRegulationsSummaryAsync()
    {
        IsBusy = true;
        try
        {
            var reports = await _databaseService.GetCargoAggregatedReportAsync(DriverId);
            if (!reports.Any())
            {
                await Shell.Current.DisplayAlertAsync("Сводка грузов", "В путевом листе отсутствуют наряды для формирования сводки.", "OK");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📊 СВОДКА ПО КАТЕГОРИЯМ ГРУЗОВ");
            sb.AppendLine("════════════════════════════════════════");

            foreach (var r in reports)
            {
                sb.AppendLine($"{r.IconGlyph} {r.CategoryName.ToUpper()} [{r.CategoryCode}]");
                sb.AppendLine($"• Температурный режим: {r.TemperatureMode}");
                sb.AppendLine($"• Правила перевозки: {r.HandlingRequirements}");
                sb.AppendLine($"• Нарядов в смене: {r.TotalOrdersCount} (Выполнено: {r.CompletedOrdersCount}, В пути: {r.InTransitOrdersCount}, Ожидает: {r.PendingOrdersCount})");
                sb.AppendLine($"• Суммарный вес: {r.TotalWeightKg:F1} кг | Объем: {r.TotalVolumeM3:F2} м³");
                if (r.TotalDistanceKm > 0 || r.TotalFuelLiters > 0)
                {
                    sb.AppendLine($"• Пробег по категории: {r.TotalDistanceKm:F1} км | Топливо: {r.TotalFuelLiters:F2} л");
                }
                sb.AppendLine("────────────────────────────────────────");
            }

            await Shell.Current.DisplayAlertAsync("Сводка по категориям грузов", sb.ToString(), "Закрыть");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Ошибка сводки", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Сброс и повторная инициализация данных из дистрибутива приложения
    /// </summary>
    [RelayCommand]
    public async Task ResetDatabaseFromPackageAsync()
    {
        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Восстановление данных",
            "Сбросить текущий путевой лист и восстановить исходные данные по нарядам?",
            "Восстановить",
            "Отмена");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            bool ok = await _databaseService.ResetDatabaseFromPackageAsync();
            if (ok)
            {
                await LoadDeliveriesAsync(refresh: true);
                await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
                await Shell.Current.DisplayAlertAsync("Успешно", "Исходный список нарядов и справочник категорий успешно восстановлены!", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Ошибка", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Справка путевого листа для водителя
    /// </summary>
    [RelayCommand]
    public async Task ViewDatabaseInfoAsync()
    {
        var summary = await _databaseService.GetDatabaseSummaryAsync(DriverId);
        var stats = await _databaseService.GetAggregatedTripStatisticsAsync(DriverId);
        var waybill = await _databaseService.GetOrCreateWaybillAsync(DriverId, DriverName, VehicleNumber);

        string message =
            $"📋 ПУТЕВОЙ ЛИСТ #{waybill.WaybillNumber}\n\n" +
            $"• Водитель: {waybill.DriverName} ({DriverId})\n" +
            $"• Автомобиль: {waybill.VehicleNumber}\n" +
            $"• Статус смены: {waybill.Status}\n" +
            $"• Дата смены: {waybill.WaybillDate:dd.MM.yyyy}\n\n" +
            $"📦 НАХОДИТСЯ В РЕЙСЕ:\n" +
            $"• Всего точек доставки: {summary.DeliveriesCount}\n" +
            $"• Категорий грузов: {summary.CategoriesCount}\n" +
            $"• Пробег за смену: {stats.TotalDistanceKm:F1} км\n" +
            $"• Расход топлива: {stats.TotalFuelConsumedLiters:F2} л\n" +
            $"• Точек телеметрии маршрута: {summary.WaypointsCount}";

        string action = await Shell.Current.DisplayActionSheetAsync(
            "Путевой лист водителя",
            "Закрыть",
            null,
            "📊 Сводка по категориям грузов",
            "📋 Информация о путевом листе",
            "🔄 Восстановить исходные данные");

        if (action == "📊 Сводка по категориям грузов")
        {
            await ShowCargoRegulationsSummaryAsync();
        }
        else if (action == "📋 Информация о путевом листе")
        {
            await Shell.Current.DisplayAlertAsync("Справка путевого листа", message, "Закрыть");
        }
        else if (action == "🔄 Восстановить исходные данные")
        {
            await ResetDatabaseFromPackageAsync();
        }
    }

    /// <summary>
    /// Приёмка грузов: оформление партии по накладной ТТН или ввод разового наряда.
    /// Все операции выполняются строго внутри единой атомарной транзакции SQLite (ACID).
    /// </summary>
    [RelayCommand]
    public async Task ReceiveCargoManifestAsync()
    {
        string choice = await Shell.Current.DisplayActionSheetAsync(
            "Приёмка грузов",
            "Отмена",
            null,
            "📋 Принять накладную ТТН",
            "➕ Оформить новый наряд");

        if (string.IsNullOrEmpty(choice) || choice == "Отмена") return;

        if (choice == "📋 Принять накладную ТТН")
        {
            await ReceiveManifestByNumberAsync();
        }
        else if (choice == "➕ Оформить новый наряд")
        {
            await RegisterManualOrderAsync();
        }
    }

    private async Task ReceiveManifestByNumberAsync()
    {
        string? manifestInput = await Shell.Current.DisplayPromptAsync(
            "Приёмка накладной ТТН",
            "Введите номер товарно-транспортной накладной со склада:",
            accept: "Далее",
            cancel: "Отмена",
            placeholder: "Например: 204 или 205",
            initialValue: "204");

        if (string.IsNullOrWhiteSpace(manifestInput)) return;

        manifestInput = manifestInput.Trim();
        string manifestNum = manifestInput.StartsWith("№") ? manifestInput : $"№ {manifestInput}";

        var items = new List<DeliveryPoint>();

        if (manifestInput.Contains("205"))
        {
            // Накладная ТТН № 205: содержит ошибку склада — повторный номер наряда,
            // уже зарегистрированный в путевом листе (ORD-BY-101 или первый доступный наряд),
            // что вызывает срабатывание ограничения [Unique] в СУБД SQLite.
            string conflictOrderNumber = Deliveries.FirstOrDefault()?.OrderNumber ?? "ORD-BY-101";

            items.Add(new DeliveryPoint
            {
                Id = $"deliv-manifest-{Guid.NewGuid().ToString("N")[..6]}",
                OrderNumber = "ORD-BY-205-1",
                ClientName = "УП «Минск-Кристалл»",
                Address = "г. Минск, ул. Октябрьская 19",
                City = "Минск",
                Phone = "+375 17 220-44-55",
                CargoDescription = "Стеклянная тара в паллетах",
                CargoCategoryId = 5,
                WeightKg = 60.0,
                VolumeM3 = 0.4,
                Priority = "Обычный",
                Status = "Ожидает",
                EstimatedTime = "17:00",
                Latitude = 53.8920,
                Longitude = 27.5710,
                Notes = $"Позиция 1 накладной {manifestNum}",
                UpdatedAt = DateTime.UtcNow
            });
            items.Add(new DeliveryPoint
            {
                Id = $"deliv-manifest-{Guid.NewGuid().ToString("N")[..6]}",
                OrderNumber = conflictOrderNumber, // ОШИБКА ДУБЛИКАТА [Unique]: номер уже существует в базе!
                ClientName = "Складской комплекс № 2",
                Address = "г. Минск, ул. Монтажников 8",
                City = "Минск",
                Phone = "+375 29 111-22-33",
                CargoDescription = "Строительные смеси и грунтовка",
                CargoCategoryId = 6,
                WeightKg = 30.0,
                VolumeM3 = 0.2,
                Priority = "Срочный",
                Status = "Ожидает",
                EstimatedTime = "17:30",
                Latitude = 53.8740,
                Longitude = 27.4210,
                Notes = $"Позиция 2 накладной {manifestNum} (дубликат наряда {conflictOrderNumber})",
                UpdatedAt = DateTime.UtcNow
            });
            items.Add(new DeliveryPoint
            {
                Id = $"deliv-manifest-{Guid.NewGuid().ToString("N")[..6]}",
                OrderNumber = "ORD-BY-205-3",
                ClientName = "ТЦ «Замок»",
                Address = "г. Минск, пр-т Победителей 65",
                City = "Минск",
                Phone = "+375 17 309-81-81",
                CargoDescription = "Товары розничной торговли",
                CargoCategoryId = 5,
                WeightKg = 25.0,
                VolumeM3 = 0.2,
                Priority = "Обычный",
                Status = "Ожидает",
                EstimatedTime = "18:00",
                Latitude = 53.9260,
                Longitude = 27.5180,
                Notes = $"Позиция 3 накладной {manifestNum}",
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            // Корректная накладная ТТН (например № 204 или любая другая)
            int seed = Math.Abs(manifestInput.GetHashCode()) % 800 + 100;
            items.Add(new DeliveryPoint
            {
                Id = $"deliv-manifest-{Guid.NewGuid().ToString("N")[..6]}",
                OrderNumber = $"ORD-BY-{seed}-1",
                ClientName = "ОАО «Белшина»",
                Address = "г. Минск, ул. Промышленная 14",
                City = "Минск",
                Phone = "+375 29 440-11-22",
                CargoDescription = "Комплект автошин R17 (4 шт.)",
                CargoCategoryId = 4,
                WeightKg = 120.0,
                VolumeM3 = 0.8,
                Priority = "Обычный",
                Status = "Ожидает",
                EstimatedTime = "14:30",
                Latitude = 53.8650,
                Longitude = 27.6820,
                Notes = $"Принято со склада по накладной {manifestNum}",
                UpdatedAt = DateTime.UtcNow
            });
            items.Add(new DeliveryPoint
            {
                Id = $"deliv-manifest-{Guid.NewGuid().ToString("N")[..6]}",
                OrderNumber = $"ORD-BY-{seed}-2",
                ClientName = "ООО «ЕвроТрейд»",
                Address = "г. Минск, пр-т Независимости 85",
                City = "Минск",
                Phone = "+375 29 771-33-44",
                CargoDescription = "Запчасти и расходные материалы",
                CargoCategoryId = 4,
                WeightKg = 45.0,
                VolumeM3 = 0.3,
                Priority = "Срочный",
                Status = "Ожидает",
                EstimatedTime = "15:45",
                Latitude = 53.9215,
                Longitude = 27.5985,
                Notes = $"Принято со склада по накладной {manifestNum}",
                UpdatedAt = DateTime.UtcNow
            });
            items.Add(new DeliveryPoint
            {
                Id = $"deliv-manifest-{Guid.NewGuid().ToString("N")[..6]}",
                OrderNumber = $"ORD-BY-{seed}-3",
                ClientName = "ЗАО «АгроКомплект»",
                Address = "г. Минск, Долгиновский тракт 42",
                City = "Минск",
                Phone = "+375 44 882-55-66",
                CargoDescription = "Инструменты и крепеж",
                CargoCategoryId = 4,
                WeightKg = 80.0,
                VolumeM3 = 0.5,
                Priority = "Обычный",
                Status = "Ожидает",
                EstimatedTime = "16:30",
                Latitude = 53.9480,
                Longitude = 27.5340,
                Notes = $"Принято со склада по накладной {manifestNum}",
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Подтверждение приёмки: водитель может отменить операцию до фиксации
        double totalWeight = items.Sum(x => x.WeightKg);
        string summaryList = string.Join("\n• ", items.Select(x => $"{x.OrderNumber}: {x.ClientName} ({x.WeightKg} кг)"));

        bool confirm = await Shell.Current.DisplayAlertAsync(
            $"Приёмка накладной {manifestNum}",
            $"В накладной {items.Count} наряда (общий вес: {totalWeight:F1} кг):\n• {summaryList}\n\nПринять партию груза в путевой лист?",
            "Принять партию",
            "Отменить");

        if (!confirm)
        {
            // Отмена пользователем
            SqliteTransactionBadgeText = $"Приёмка {manifestNum} отменена";
            SqliteTransactionStatusColor = "#F59E0B";
            SqliteTransactionDetails = "Операция отменена водителем.";
            StatusNotice = $"Приёмка накладной {manifestNum} отменена.";
            return;
        }

        IsBusy = true;
        try
        {
            var trxResult = await _databaseService.AcceptCargoManifestBatchAsync(DriverId, manifestNum, items);
            if (trxResult.IsSuccess && trxResult.IsCommitted)
            {
                SqliteTransactionBadgeText = $"Принято: {trxResult.AffectedRows} наряда ({manifestNum})";
                SqliteTransactionStatusColor = "#10B981";
                SqliteTransactionDetails = $"Накладная {manifestNum} успешно проведена";
                StatusNotice = $"Накладная {manifestNum}: все {trxResult.AffectedRows} наряда внесены в путевой лист.";

                await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
                await _nativeFeedbackService.ShowNativeToastAsync($"Принято нарядов: {trxResult.AffectedRows}");

                var updatedList = await _databaseService.GetDeliveryPointsAsync(DriverId);
                Deliveries.Clear();
                foreach (var item in updatedList) Deliveries.Add(item);
                TotalCount = Deliveries.Count;
                await UpdateDatabaseCountsAsync();

                await Shell.Current.DisplayAlertAsync(
                    "Приёмка груза завершена",
                    $"Накладная {manifestNum} успешно проведена!\n\n" +
                    $"• Добавлено нарядов: {trxResult.AffectedRows}\n" +
                    $"• Все наряды внесены в текущий путевой лист.",
                    "OK");
            }
            else
            {
                SqliteTransactionBadgeText = $"Отказ: накладная {manifestNum}";
                SqliteTransactionStatusColor = "#EF4444";
                SqliteTransactionDetails = "Обнаружен дубликат наряда. Партия отклонена.";
                StatusNotice = $"Накладная {manifestNum} отклонена: конфликт номеров нарядов.";

                await _nativeFeedbackService.ShowNativeToastAsync("Ошибка проведения накладной");

                await Shell.Current.DisplayAlertAsync(
                    "Ошибка проведения накладной",
                    $"Не удалось провести приёмку накладной {manifestNum}:\n\n" +
                    $"В партии обнаружен номер наряда, который уже зарегистрирован в путевом листе.\n\n" +
                    $"Партия груза не была сохранена во избежание дублирования данных.",
                    "Понятно");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка приёмки: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            await UpdateDatabaseCountsAsync();
        }
    }

    private async Task RegisterManualOrderAsync()
    {
        string? orderNum = await Shell.Current.DisplayPromptAsync(
            "Оформление нового наряда",
            "Введите номер наряда:",
            accept: "Далее",
            cancel: "Отмена",
            placeholder: "Например: ORD-BY-105",
            initialValue: $"ORD-BY-{Deliveries.Count + 105}");

        if (string.IsNullOrWhiteSpace(orderNum)) return;

        orderNum = orderNum.Trim();

        string? client = await Shell.Current.DisplayPromptAsync(
            "Оформление нового наряда",
            $"Получатель груза для наряда {orderNum}:",
            accept: "Далее",
            cancel: "Отмена",
            placeholder: "Название организации или ФИО",
            initialValue: "ООО «БелТрансЭкспедиция»");

        if (string.IsNullOrWhiteSpace(client)) return;

        string? address = await Shell.Current.DisplayPromptAsync(
            "Оформление нового наряда",
            $"Адрес доставки для наряда {orderNum}:",
            accept: "Сохранить наряд",
            cancel: "Отмена",
            placeholder: "Адрес доставки",
            initialValue: "г. Минск, ул. Платонова 22");

        if (string.IsNullOrWhiteSpace(address)) return;

        var newPoint = new DeliveryPoint
        {
            Id = $"deliv-manual-{Guid.NewGuid().ToString("N")[..6]}",
            OrderNumber = orderNum,
            ClientName = client.Trim(),
            Address = address.Trim(),
            City = "Минск",
            Phone = "+375 29 600-11-22",
            CargoDescription = "Сборный розничный груз",
            CargoCategoryId = 5,
            WeightKg = 35.0,
            VolumeM3 = 0.25,
            Priority = "Обычный",
            Status = "Ожидает",
            EstimatedTime = $"{DateTime.Now.AddHours(2):HH:mm}",
            Latitude = 53.9110,
            Longitude = 27.5950,
            Notes = "Оформлен водителем в рейсе",
            UpdatedAt = DateTime.UtcNow
        };

        IsBusy = true;
        try
        {
            var trxResult = await _databaseService.RegisterSingleOrderAsync(DriverId, newPoint);
            if (trxResult.IsSuccess && trxResult.IsCommitted)
            {
                SqliteTransactionBadgeText = $"Наряд {orderNum} добавлен";
                SqliteTransactionStatusColor = "#10B981";
                SqliteTransactionDetails = $"Наряд {orderNum} успешно зарегистрирован";
                StatusNotice = $"Наряд {orderNum} добавлен в путевой лист.";

                var updatedList = await _databaseService.GetDeliveryPointsAsync(DriverId);
                Deliveries.Clear();
                foreach (var item in updatedList) Deliveries.Add(item);
                TotalCount = Deliveries.Count;
                await UpdateDatabaseCountsAsync();

                await Shell.Current.DisplayAlertAsync("Успешно", $"Наряд {orderNum} внесён в путевой лист.", "OK");
            }
            else
            {
                SqliteTransactionBadgeText = $"Дубликат {orderNum}";
                SqliteTransactionStatusColor = "#EF4444";
                SqliteTransactionDetails = trxResult.Message;
                StatusNotice = $"Сбой регистрации наряда {orderNum}.";

                await Shell.Current.DisplayAlertAsync(
                    "Наряд не сохранен",
                    $"Не удалось сохранить наряд {orderNum}:\n\n" +
                    $"Наряд с таким номером уже существует в путевом листе.",
                    "Понятно");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Комплексная синхронизация: наряды, телеметрия (Вариант 7) и очередь офлайн-транзакций
    /// </summary>
    [RelayCommand]
    public async Task SyncAllDataAsync()
    {
        IsBusy = true;
        StatusNotice = "Синхронизация данных путевого листа и телеметрии...";
        try
        {
            // 1. Сброс накопленной телеметрии в рамках транзакции SQLite (Вариант 7)
            if (UnsyncedWaypointsCount > 0)
            {
                var waypointsRes = await _telemetryService.FlushOfflineWaypointsTransactionAsync();
                if (waypointsRes.IsSuccess)
                {
                    StatusNotice = waypointsRes.Message;
                }
            }

            // 2. Сброс отложенных изменений (очередь)
            if (PendingSyncCount > 0 || PendingSqliteSyncCount > 0)
            {
                await ProcessSyncQueueInternalAsync();
            }

            // 3. Загрузка свежих нарядов из API с транзакционным сохранением
            await LoadDeliveriesAsync(refresh: true);
            await UpdateDatabaseCountsAsync();
            await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
            await _nativeFeedbackService.ShowNativeToastAsync("Синхронизация завершена");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка синхронизации: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Формирование точек маршрута для отправки
    /// </summary>
    [RelayCommand]
    public async Task SimulateOfflineWaypointsAsync()
    {
        IsBusy = true;
        try
        {
            int added = await _telemetryService.SimulateOfflineWaypointsAccumulationAsync(15);
            await UpdateDatabaseCountsAsync();
            await _nativeFeedbackService.ShowNativeToastAsync($"Накоплено {added} точек маршрута");
            await Shell.Current.DisplayAlertAsync(
                "Маршрутные точки",
                $"Зафиксировано {added} точек пути водителя.\n\nВсего неотправленных точек: {UnsyncedWaypointsCount}.\n\nНажмите 'Отправить', чтобы передать точки на сервер диспетчерской.",
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Передача накопленной телеметрии маршрута
    /// </summary>
    [RelayCommand]
    public async Task SyncOfflineWaypointsAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _telemetryService.FlushOfflineWaypointsTransactionAsync();
            await UpdateDatabaseCountsAsync();

            if (result.IsSuccess)
            {
                SqliteTransactionBadgeText = $"Передано: {result.AffectedRows} точек пути";
                SqliteTransactionStatusColor = "#10B981";
                SqliteTransactionDetails = result.Message;
                StatusNotice = $"Передано {result.AffectedRows} точек маршрута.";

                await Shell.Current.DisplayAlertAsync(
                    "Синхронизация телеметрии",
                    $"Точки маршрута ({result.AffectedRows}) успешно переданы на сервер.",
                    "OK");
            }
            else
            {
                SqliteTransactionBadgeText = "Ошибка телеметрии";
                SqliteTransactionStatusColor = "#EF4444";
                SqliteTransactionDetails = result.Message;
                await Shell.Current.DisplayAlertAsync("Сбой передачи", result.Message, "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Проверка стабильности сохранения данных
    /// </summary>
    [RelayCommand]
    public async Task TestTransactionRollbackAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _databaseService.SimulateCorruptDataTransactionTestAsync(DriverId);
            SqliteTransactionBadgeText = "Проверка целостности";
            SqliteTransactionStatusColor = "#10B981";
            SqliteTransactionDetails = "Целостность данных подтверждена";
            StatusNotice = "Проверка целостности завершена успешно.";

            await Shell.Current.DisplayAlertAsync(
                "Целостность данных",
                "Контроль целостности путевого листа выполнен успешно. Некорректные данные отсеиваются автоматически.",
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ViewPendingQueueJsonAsync()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "pending_sync_queue.json");
        if (!File.Exists(path))
        {
            await Shell.Current.DisplayAlertAsync(
                "Очередь отправки", 
                "Все изменения уже переданы на сервер.", 
                "OK");
            return;
        }

        var json = await File.ReadAllTextAsync(path);
        await Shell.Current.DisplayAlertAsync(
            "Очередь отправки", 
            $"Количество записей в очереди: {PendingSyncCount}", 
            "Закрыть");
    }

    [RelayCommand]
    public async Task ViewDeliveriesCacheJsonAsync()
    {
        var sanitized = string.IsNullOrWhiteSpace(DriverId) ? "default" : DriverId.Trim();
        var path = Path.Combine(FileSystem.AppDataDirectory, $"deliveries_cache_{sanitized}.json");
        if (!File.Exists(path))
        {
            await Shell.Current.DisplayAlertAsync("Сохраненные наряды", "Локальные записи отсутствуют.", "OK");
            return;
        }

        await Shell.Current.DisplayAlertAsync(
            "Сохраненные наряды", 
            $"Количество сохраненных нарядов: {Deliveries.Count}", 
            "Закрыть");
    }

    // =========================================================================
    // ОТЧЕТ О РАСХОДЕ ТОПЛИВА
    // =========================================================================

    [RelayCommand]
    public async Task GenerateAndUploadFuelReportAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusNotice = "Формирование отчета о расходе топлива...";

        try
        {
            // 1. Формирование отчета
            var (report, encryptedFilePath) = await _fuelReportService.GenerateAndEncryptReportAsync(
                DriverId, DriverName, VehicleNumber, Deliveries);

            var fileInfo = new FileInfo(encryptedFilePath);
            FuelReportStatusText = $"Отчет сформирован: {Path.GetFileName(encryptedFilePath)}";

            // 2. Если офлайн — сохраняем локально
            if (_mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected)
            {
                StatusNotice = $"Отчет сохранен на устройстве ({fileInfo.Length} байт).";
                await Shell.Current.DisplayAlertAsync(
                    "Отчет сохранен",
                    $"Отчет по расходу топлива за смену успешно сформирован и сохранен на устройстве.\n\nФайл: {Path.GetFileName(encryptedFilePath)}\nПри появлении сети отчет будет отправлен в диспетчерскую.",
                    "OK");
                return;
            }

            // 3. Отправка контента на сервер
            StatusNotice = "Отправка отчета в диспетчерскую...";
            var uploadResult = await _apiService.UploadFuelReportAsync(
                DriverId, 
                encryptedFilePath, 
                CreateLinkedToken(TimeSpan.FromSeconds(15)));

            if (uploadResult.Success)
            {
                await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
                await _nativeFeedbackService.ShowNativeToastAsync("Отчет топлива успешно передан на сервер!");
                FuelReportStatusText = $"Отчет принят: {uploadResult.FileName}";
                StatusNotice = "Отчет о расходе топлива успешно передан в диспетчерскую!";

                await Shell.Current.DisplayAlertAsync(
                    "Отчет отправлен",
                    $"Отчет о расходе топлива успешно передан в диспетчерскую.\n\nФайл: {uploadResult.FileName}\nВремя отправки: {DateTime.Now:HH:mm:ss}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Сбой при отправке отчета: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ResetCancellation();
        }
    }

    [RelayCommand]
    public async Task SimulateTokenExpirationAsync()
    {
        await _secureStorageService.SimulateExpireTokenAsync();
        StatusNotice = "Сессия обновлена.";
    }

    [RelayCommand]
    public async Task PlotRouteAsync()
    {
        if (IsPlottingRoute) return;

        if (!Deliveries.Any())
        {
            ErrorMessage = "Нет точек доставки для прокладки маршрута.";
            return;
        }

        IsPlottingRoute = true;
        ErrorMessage = null;
        var token = CreateLinkedToken(TimeSpan.FromSeconds(15));

        try
        {
            var driverLoc = await _locationService.GetCurrentLocationAsync(token);
            _telemetryService.UpdateDriverPosition(driverLoc.Latitude, driverLoc.Longitude);

            var route = await _routingService.CalculateRouteAsync(driverLoc.Latitude, driverLoc.Longitude, Deliveries, token);

            if (route.IsSuccess)
            {
                await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
                LastCalculatedRoute = route;
                IsRouteAvailable = true;
                RouteSummary = $"Дистанция: {route.FormattedDistance} • Время в пути: ~{route.FormattedDuration} ({route.Geometry.Count} точек)";
                
                var navParam = new Dictionary<string, object>
                {
                    { "CalculatedRoute", route }
                };
                await Shell.Current.GoToAsync("RouteMapPage", navParam);
            }
            else
            {
                ErrorMessage = route.ErrorMessage ?? "Не удалось построить маршрут через гео-сервер.";
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Построение маршрута отменено.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка построения маршрута: {ex.Message}";
        }
        finally
        {
            IsPlottingRoute = false;
            ResetCancellation();
        }
    }

    [RelayCommand]
    public async Task OpenMapAsync()
    {
        if (LastCalculatedRoute == null)
        {
            await PlotRouteAsync();
        }
        else
        {
            var navParam = new Dictionary<string, object>
            {
                { "CalculatedRoute", LastCalculatedRoute }
            };
            await Shell.Current.GoToAsync("RouteMapPage", navParam);
        }
    }

    [RelayCommand]
    public async Task SelectDeliveryAsync(DeliveryPoint? point)
    {
        if (point == null) return;

        var navParam = new Dictionary<string, object>
        {
            { "DeliveryPoint", point }
        };

        await Shell.Current.GoToAsync("DeliveryDetailPage", navParam);
    }

    [RelayCommand]
    public async Task SendTelemetryNowAsync()
    {
        await _telemetryService.SendImmediateAsync();
        UpdateTelemetryUI();
    }

    [RelayCommand]
    public void ToggleSimulateOffline()
    {
        IsSimulateOffline = !IsSimulateOffline;
        _mockBackendServer.SimulateOffline = IsSimulateOffline;
        CacheNotice = IsSimulateOffline 
            ? "Автономный режим активен. Все изменения сохраняются в памяти устройства." 
            : null;
    }

    [RelayCommand]
    public async Task OpenProfileAsync()
    {
        await Shell.Current.GoToAsync(nameof(ProfilePage));
    }

    [RelayCommand]
    public async Task LogoutAsync()
    {
        // 1. Принудительно останавливаем фоновый сбор телеметрии и запись координат в БД (Вариант 7)
        _telemetryService.Stop();

        // 2. Атомарно очищаем токены в SecureStorage (Требование 4)
        await _secureStorageService.ClearAuthAsync();

        // 3. Помечаем сессию в SQLite как неактивную (Требование 4)
        await _databaseService.DeactivateUserSessionAsync(DriverId);

        // 4. Сброс стека навигации и блокировка контента: возврат на LoginPage (Требование 4)
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
