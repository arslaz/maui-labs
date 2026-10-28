using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using laba12.Models;
using laba12.Services;

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
    private string databaseStatusText = "SQLite: app_local_storage.db3";

    // Общие требования ЛР 16: Визуальная индикация успешности транзакций SQLite (ACID)
    [ObservableProperty]
    private string sqliteTransactionBadgeText = "База данных: Синхронизировано";

    [ObservableProperty]
    private string sqliteTransactionStatusColor = "#10B981";

    [ObservableProperty]
    private string sqliteTransactionDetails = "Локальная база данных SQLite синхронизирована";

    // Вариант 7: Накопленные путевые точки телеметрии в SQLite
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
    public string UnsyncedWaypointsBadgeText => $"Точки пути в SQLite: {UnsyncedWaypointsCount}";

    public bool HasPendingSyncTransactions => PendingSyncCount > 0 || PendingSqliteSyncCount > 0;
    public string PendingSyncBadgeText => $"Очередь синхронизации: {Math.Max(PendingSyncCount, PendingSqliteSyncCount)}";

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
                CacheNotice = "ОФЛАЙН: Сеть недоступна. Изменения сохраняются в локальный JSON-лог синхронизации.";
                ErrorMessage = "Отсутствует подключение к интернету! Включен автономный режим.";
            }
            else
            {
                IsOffline = false;
                CacheNotice = null;
                StatusNotice = "Сеть восстановлена! Автоматическая отправка отложенных транзакций...";
                
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
                LastUpdatedText = $"БД SQLite (app_local_storage.db3): {DateTime.Now:HH:mm:ss}";
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
                    LastUpdatedText = $"Локальный JSON кэш: {DateTime.Now:HH:mm:ss}";
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
            CacheNotice = $"ОФЛАЙН: Автономный режим. Данные мгновенно отображены из локальной базы SQLite ({Deliveries.Count} нарядов).";
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
                SqliteTransactionDetails = trxResult.Message;
            }
            else
            {
                SqliteTransactionBadgeText = "Сбой синхронизации путевого листа";
                SqliteTransactionStatusColor = "#EF4444";
                SqliteTransactionDetails = trxResult.Message;
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
            LastUpdatedText = $"Синхронизировано API ➔ SQLite: {DateTime.Now:HH:mm:ss}";

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
            CacheNotice = $"ОФЛАЙН: Сервер недоступен ({ex.Message}). Показаны данные из локальной базы SQLite.";
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
            await Shell.Current.DisplayAlertAsync("Синхронизация", "Локальная база данных SQLite и лог синхронизации полностью синхронизированы.", "OK");
            return;
        }

        if (_mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected)
        {
            await Shell.Current.DisplayAlertAsync("Офлайн-режим", "Невозможно выполнить синхронизацию при выключенной сети.", "OK");
            return;
        }

        await ProcessSyncQueueInternalAsync();
    }

    private async Task ProcessSyncQueueInternalAsync()
    {
        IsBusy = true;
        StatusNotice = "Синхронизация отложенных изменений SQLite с сервером...";
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

            string resultMsg = sqliteSyncedCount > 0
                ? $"Успешно синхронизировано строк SQLite: {sqliteSyncedCount}. {result.Message}"
                : result.Message;

            await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
            await _nativeFeedbackService.ShowNativeToastAsync("Синхронизация с сервером завершена!");
            StatusNotice = resultMsg;
            await Shell.Current.DisplayAlertAsync("Отложенная синхронизация", resultMsg, "OK");
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
            DatabaseStatusText = $"SQLite: {summary.DeliveriesCount} нарядов | {summary.CategoriesCount} категорий грузов | {summary.WaypointsCount} точек пути";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] UpdateDatabaseCounts error: {ex.Message}");
        }
    }

    /// <summary>
    /// ОБЯЗАТЕЛЬНОЕ ТРЕБОВАНИЕ: Отображение агрегированных данных, извлеченных из СУБД с помощью
    /// сложного связывающего SQL-запроса (JOIN), объединяющего справочник cargo_categories,
    /// наряды delivery_points и транзакции trip_records. Исключает вложенные циклы в C#.
    /// </summary>
    [RelayCommand]
    public async Task ShowCargoRegulationsSummaryAsync()
    {
        if (IsRegulationsSummaryVisible)
        {
            IsRegulationsSummaryVisible = false;
            RegulationsSummaryButtonText = "📊 Сводка требований и режимов грузов (SQL JOIN)";
            return;
        }

        IsBusy = true;
        try
        {
            var reports = await _databaseService.GetCargoAggregatedReportAsync(DriverId);
            CargoCategoryReports.Clear();
            foreach (var r in reports)
            {
                CargoCategoryReports.Add(r);
            }

            if (!CargoCategoryReports.Any())
            {
                await Shell.Current.DisplayAlertAsync("Сводка грузов", "В путевом листе отсутствуют наряды для формирования сводки.", "OK");
                return;
            }

            IsRegulationsSummaryVisible = true;
            RegulationsSummaryButtonText = "🔼 Скрыть сводку требований и режимов";
            await _nativeFeedbackService.TriggerClickFeedbackAsync();
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

    [RelayCommand]
    public void HideRegulationsSummary()
    {
        IsRegulationsSummaryVisible = false;
        RegulationsSummaryButtonText = "📊 Сводка требований и режимов грузов (SQL JOIN)";
    }

    private async Task ShowCargoRegulationsAlertSummaryAsync()
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
            sb.AppendLine("📊 Агрегированная сводка грузов и требований");
            sb.AppendLine("SQL JOIN: cargo_categories ⨝ delivery_points ⨝ trip_records");
            sb.AppendLine("─────────────────────────────────────");

            foreach (var r in reports)
            {
                sb.AppendLine($"{r.IconGlyph} {r.CategoryName} [{r.CategoryCode}]");
                sb.AppendLine($"  • Темп. режим: {r.TemperatureMode}");
                sb.AppendLine($"  • Требования: {r.HandlingRequirements}");
                sb.AppendLine($"  • Нарядов: {r.TotalOrdersCount} (Готово: {r.CompletedOrdersCount}, В пути: {r.InTransitOrdersCount}, Ожидает: {r.PendingOrdersCount})");
                sb.AppendLine($"  • Масса: {r.TotalWeightKg:F1} кг | Объём: {r.TotalVolumeM3:F2} м³");
                if (r.TotalDistanceKm > 0 || r.TotalFuelLiters > 0)
                {
                    sb.AppendLine($"  • Рейсы: {r.TotalDistanceKm:F1} км | Топливо: {r.TotalFuelLiters:F2} л");
                }
                sb.AppendLine("─────────────────────────────────────");
            }

            await Shell.Current.DisplayAlertAsync("Сводка по категориям грузов (JOIN)", sb.ToString(), "Закрыть");
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
    /// ОБЯЗАТЕЛЬНОЕ ТРЕБОВАНИЕ: Асинхронный алгоритм проверки наличия рабочей БД и копирования
    /// файла справочника из ресурсов пакета Resources/Raw.
    /// Метод демонстрации сброса рабочей БД и повторного копирования из дистрибутива.
    /// </summary>
    [RelayCommand]
    public async Task ResetDatabaseFromPackageAsync()
    {
        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Сброс базы данных",
            "Перезаписать рабочую базу данных app_local_storage.db3 оригинальным файлом со статическим справочником из ресурсов пакета (Resources/Raw)?",
            "Сбросить и восстановить",
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
                await Shell.Current.DisplayAlertAsync("Успешно", "Рабочая база данных переинициализирована из пакета Resources/Raw со стартовым справочником категорий!", "OK");
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
    /// Инспектор базы данных SQLite для демонстрации преподавателю
    /// </summary>
    [RelayCommand]
    public async Task ViewDatabaseInfoAsync()
    {
        var summary = await _databaseService.GetDatabaseSummaryAsync(DriverId);
        var stats = await _databaseService.GetAggregatedTripStatisticsAsync(DriverId);
        var waybill = await _databaseService.GetOrCreateWaybillAsync(DriverId, DriverName, VehicleNumber);

        string message =
            $"📂 ФАЙЛ БАЗЫ ДАННЫХ SQLITE:\n{summary.DbPath}\n\n" +
            $"📑 ТАБЛИЦА 1: cargo_categories (Вариант 7 - Справочник правил перевозок)\n" +
            $"• Категорий грузов: {summary.CategoriesCount} (Рефрижератор, Фармацевтика, Хрупкое, Оборудование...)\n" +
            $"• Жесткая реляционная связь: FOREIGN KEY (CargoCategoryId) REFERENCES cargo_categories(CategoryId)\n" +
            $"• Источник: Дистрибутив пакета (Resources/Raw/app_local_storage.db3)\n\n" +
            $"📋 ТАБЛИЦА 2: waybills (Путевые листы водителей)\n" +
            $"• Записей: {summary.WaybillCount} | Активный путевой лист: #{waybill.Id} ({waybill.Status})\n" +
            $"• Дата смены: {waybill.WaybillDate:dd.MM.yyyy} | Водитель: {waybill.DriverName}\n\n" +
            $"📦 ТАБЛИЦА 3: delivery_points (Точки маршрутного листа)\n" +
            $"• Всего точек в SQLite: {summary.DeliveriesCount}\n" +
            $"• Ожидают синхронизации (IsSyncPending): {summary.PendingSyncCount}\n" +
            $"• Внешний ключ: [Indexed] CargoCategoryId -> cargo_categories(CategoryId)\n\n" +
            $"🚚 ТАБЛИЦА 4: trip_records (Рейсы и расход топлива ДУТ)\n" +
            $"• Зарегистрировано рейсов: {summary.TripRecordsCount}\n" +
            $"• Агрегированный пробег: {stats.TotalDistanceKm:F1} км | Израсходовано: {stats.TotalFuelConsumedLiters:F2} л\n\n" +
            $"📡 ТАБЛИЦА 5: driver_waypoints (Путевые точки телеметрии)\n" +
            $"• Всего точек в таблице: {summary.WaypointsCount} (несинхр: {summary.UnsyncedWaypointsCount})\n\n" +
            $"🛡️ АТОМАРНОСТЬ: Все операции выполняются через единые транзакции RunInTransactionAsync (ACID).";

        string action = await Shell.Current.DisplayActionSheetAsync(
            "Путевой лист и СУБД SQLite",
            "Закрыть",
            null,
            "📊 Показать сводку грузов (SQL JOIN)",
            "ℹ️ Подробная структура БД",
            "🔄 Восстановить БД из Resources/Raw");

        if (action == "📊 Показать сводку грузов (SQL JOIN)")
        {
            await ShowCargoRegulationsAlertSummaryAsync();
        }
        else if (action == "ℹ️ Подробная структура БД")
        {
            await Shell.Current.DisplayAlertAsync("Схема и таблицы SQLite", message, "Закрыть");
        }
        else if (action == "🔄 Восстановить БД из Resources/Raw")
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
            $"В накладной {items.Count} наряда (общий вес: {totalWeight:F1} кг):\n• {summaryList}\n\nЗафиксировать приёмку партии в базе данных SQLite?",
            "Принять партию",
            "Отменить");

        if (!confirm)
        {
            // Отмена пользователем: транзакция отменяется, ни один наряд не записан
            SqliteTransactionBadgeText = $"Приёмка {manifestNum} отменена";
            SqliteTransactionStatusColor = "#F59E0B";
            SqliteTransactionDetails = "Операция отменена водителем. Ни одна запись не внесена в базу данных.";
            StatusNotice = $"Приёмка накладной {manifestNum} отменена. Путевой лист без изменений.";
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
                SqliteTransactionDetails = $"Партия успешно зафиксирована в SQLite за {trxResult.DurationMs} мс. Обновлен журнал рейса.";
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
                    $"• Время транзакции SQLite: {trxResult.DurationMs} мс\n" +
                    $"• Атомарность (ACID): Все наряды и этапы рейса сохранены в базе единым блоком.",
                    "OK");
            }
            else
            {
                // Транзакция полностью откатана СУБД SQLite (ROLLBACK)
                SqliteTransactionBadgeText = $"Отказ: накладная {manifestNum} отклонена";
                SqliteTransactionStatusColor = "#EF4444";
                SqliteTransactionDetails = "Конфликт уникальности наряда. Транзакция откатана: 0 записей внесено.";
                StatusNotice = $"Накладная {manifestNum} отклонена СУБД. Предыдущее состояние сохранено.";

                await _nativeFeedbackService.ShowNativeToastAsync("Ошибка накладной: откат транзакции");

                await Shell.Current.DisplayAlertAsync(
                    "Накладная отклонена базой данных",
                    $"СУБД SQLite отклонила приёмку накладной {manifestNum}:\n\n" +
                    $"{trxResult.ErrorDetails}\n\n" +
                    $"• Принцип атомарности ACID: Так как в накладной обнаружен дубликат номера наряда, транзакция была полностью откатана.\n" +
                    $"• Ни один наряд из накладной {manifestNum} не записан в базу данных.\n" +
                    $"• Путевой лист водителя остался в исходном стабильном состоянии.",
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
                SqliteTransactionDetails = $"Наряд {orderNum} успешно зарегистрирован в базе данных за {trxResult.DurationMs} мс.";
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
                SqliteTransactionBadgeText = $"Конфликт: дубликат {orderNum}";
                SqliteTransactionStatusColor = "#EF4444";
                SqliteTransactionDetails = trxResult.Message;
                StatusNotice = $"Сбой регистрации наряда {orderNum}: откат транзакции.";

                await Shell.Current.DisplayAlertAsync(
                    "Наряд отклонен СУБД",
                    $"Не удалось сохранить наряд {orderNum}:\n\n{trxResult.ErrorDetails}\n\n" +
                    $"В локальной СУБД SQLite сработало ограничение уникальности [Unique]. Наряд не был сохранен.",
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
    /// Вариант 7: Имитация накопления путевых точек пути водителя в SQLite в офлайне
    /// </summary>
    [RelayCommand]
    public async Task SimulateOfflineWaypointsAsync()
    {
        IsBusy = true;
        try
        {
            int added = await _telemetryService.SimulateOfflineWaypointsAccumulationAsync(15);
            await UpdateDatabaseCountsAsync();
            await _nativeFeedbackService.ShowNativeToastAsync($"Накоплено {added} точек в SQLite (Вар. 7)");
            await Shell.Current.DisplayAlertAsync(
                "Вариант 7: Накопление телеметрии",
                $"В локальную таблицу SQLite 'driver_waypoints' успешно сохранено {added} путевых точек водителя с индексами по координатам [Indexed] и временным штампам.\n\n" +
                $"Всего неотправленных точек в базе: {UnsyncedWaypointsCount}.\n\n" +
                $"Теперь нажмите 'Синхронизировать телеметрию (Вар. 7)', чтобы передать пакет на сервер и массово отметить синхронизацию единой транзакцией SQLite!",
                "Понятно");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Вариант 7: Синхронизация накопленной за время отсутствия сети телеметрии
    /// считывается из SQLite, отправляется в API и массово помечается как синхронизированная внутри одной транзакции
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
                SqliteTransactionBadgeText = $"SQLite: COMMIT ({result.AffectedRows} точек)";
                SqliteTransactionStatusColor = "#10B981";
                SqliteTransactionDetails = result.Message;
                StatusNotice = result.Message;

                await Shell.Current.DisplayAlertAsync(
                    "Вариант 7: Успех транзакции SQLite",
                    $"Результат атомарной операции:\n\n{result.Message}\n\n" +
                    $"• Время выполнения транзакции: {result.DurationMs} мс\n" +
                    $"• Обновлено записей: {result.AffectedRows}\n" +
                    $"• Принцип ACID: Массовая пометка строк выполнена внутри RunInTransactionAsync.",
                    "Отлично");
            }
            else
            {
                SqliteTransactionBadgeText = "SQLite: Ошибка телеметрии";
                SqliteTransactionStatusColor = "#EF4444";
                SqliteTransactionDetails = result.Message;
                await Shell.Current.DisplayAlertAsync("Сбой транзакции", result.Message, "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Общие требования: Проверка атомарности транзакции SQLite (ACID Rollback) для демонстрации преподавателю
    /// </summary>
    [RelayCommand]
    public async Task TestTransactionRollbackAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _databaseService.SimulateCorruptDataTransactionTestAsync(DriverId);
            SqliteTransactionBadgeText = "SQLite: ROLLBACK (Тест)";
            SqliteTransactionStatusColor = "#EF4444";
            SqliteTransactionDetails = result.Message;
            StatusNotice = result.Message;

            await Shell.Current.DisplayAlertAsync(
                "Демонстрация ACID Rollback (ЛР 16)",
                $"Была запущена транзакция с 1 валидным элементом и 1 поврежденным элементом (дубликатом OrderNumber с ограничением [Unique]).\n\n" +
                $"Результат работы ядра SQLite:\n{result.Message}\n\n" +
                $"Вывод для учителя: Ни одна строка из этой пачки не осталась в базе данных, так как механизм RunInTransactionAsync автоматически выполнил полный откат при ошибке ядра SQLite (свойство атомарности ACID)!",
                "Понятно");
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
                "Локальный JSON-лог транзакций", 
                $"Файл транзакций пока пуст.\n\nПуть на телефоне:\n{path}\n\nВключите 'Офлайн-тест' и нажмите 'Отметить прибытие' или 'Отказ', чтобы создать транзакцию.", 
                "OK");
            return;
        }

        var json = await File.ReadAllTextAsync(path);
        await Shell.Current.DisplayAlertAsync(
            "pending_sync_queue.json", 
            $"Путь на устройстве:\n{path}\n\nСодержимое отложенных транзакций:\n{json}", 
            "Закрыть");
    }

    [RelayCommand]
    public async Task ViewDeliveriesCacheJsonAsync()
    {
        var sanitized = string.IsNullOrWhiteSpace(DriverId) ? "default" : DriverId.Trim();
        var path = Path.Combine(FileSystem.AppDataDirectory, $"deliveries_cache_{sanitized}.json");
        if (!File.Exists(path))
        {
            await Shell.Current.DisplayAlertAsync("Кэш доставок (Cache-Aside)", $"Файл кэша отсутствует.\n\nПуть:\n{path}", "OK");
            return;
        }

        var json = await File.ReadAllTextAsync(path);
        var preview = json.Length > 700 ? json[..700] + "\n... (продолжение следует)" : json;
        await Shell.Current.DisplayAlertAsync(
            "deliveries_cache.json (Cache-Aside)", 
            $"Путь на устройстве:\n{path}\n\nРазмер: {json.Length} байт\n\nСнимок данных:\n{preview}", 
            "Закрыть");
    }

    // =========================================================================
    // ВАРИАНТ 7: ЗАШИФРОВАННЫЙ ОТЧЕТ О ТОПЛИВЕ (AES-256 + MULTIPART HTTP POST)
    // =========================================================================

    [RelayCommand]
    public async Task GenerateAndUploadFuelReportAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusNotice = "Формирование и шифрование отчета о расходе топлива (AES-256)...";

        try
        {
            // 1. Формирование отчета, шифрование ключом из SecureStorage, запись в локальный кэш
            var (report, encryptedFilePath) = await _fuelReportService.GenerateAndEncryptReportAsync(
                DriverId, DriverName, VehicleNumber, Deliveries);

            var fileInfo = new FileInfo(encryptedFilePath);
            FuelReportStatusText = $"Зашифрован AES-256 ({fileInfo.Length} байт): {Path.GetFileName(encryptedFilePath)}";

            // 2. Если офлайн — сохраняем локально в кэш-файле
            if (_mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected)
            {
                StatusNotice = $"ОФЛАЙН: Зашифрованный файл ({fileInfo.Length} байт) сохранен в кэш AppDataDirectory. Запись продублирована в fuel_reports_cache.json";
                await Shell.Current.DisplayAlertAsync(
                    "Отчет сохранен в локальный кэш",
                    $"Шифрование: AES-256-CBC (ключ из SecureStorage)\nФайл: {Path.GetFileName(encryptedFilePath)}\nРазмер: {fileInfo.Length} байт\n\nПриложение продублировало запись в локальный кэш fuel_reports_cache.json (стратегия Cache-Aside).",
                    "OK");
                return;
            }

            // 3. Отправка бинарного контента на сервер через MultipartFormDataContent со StreamContent
            StatusNotice = "Отправка зашифрованного файла на сервер транспортной компании...";
            var uploadResult = await _apiService.UploadFuelReportAsync(
                DriverId, 
                encryptedFilePath, 
                CreateLinkedToken(TimeSpan.FromSeconds(15)));

            if (uploadResult.Success)
            {
                await _nativeFeedbackService.TriggerSuccessFeedbackAsync();
                await _nativeFeedbackService.ShowNativeToastAsync("Отчет топлива успешно передан на сервер!");
                FuelReportStatusText = $"Сервер принял отчет: {uploadResult.FileName} ({uploadResult.FileSizeBytes} байт)";
                StatusNotice = "Отчет о расходе топлива успешно зашифрован и передан на сервер!";

                await Shell.Current.DisplayAlertAsync(
                    "Отчет о расходе топлива отправлен",
                    $"{uploadResult.ServerMessage}\n\nФайл: {uploadResult.FileName}\nРазмер: {uploadResult.FileSizeBytes} байт\nШифрование: {uploadResult.EncryptionAlgorithm}\nЛокальный кэш: обновлен",
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

    // =========================================================================
    // ДЕМОНСТРАЦИЯ DELEGATINGHANDLER ДЛЯ ПРЕПОДАВАТЕЛЯ (401 REFRESH TOKEN)
    // =========================================================================

    [RelayCommand]
    public async Task SimulateTokenExpirationAsync()
    {
        await _secureStorageService.SimulateExpireTokenAsync();
        StatusNotice = "⚠️ Токен в SecureStorage заменен на устаревший. Следующий запрос вызовет 401, который автоматически перехватит DelegatingHandler!";
        
        await Shell.Current.DisplayAlertAsync(
            "Тест DelegatingHandler (401 Refresh)",
            "В SecureStorage установлен устаревший токен.\n\nТеперь нажмите «Обновить» или любую команду контекстного меню. DelegatingHandler автоматически:\n1. Перехватит ответ 401 Unauthorized\n2. Отправит скрытый запрос /api/auth/refresh\n3. Перезапишет новые токены в SecureStorage\n4. Повторит исходный запрос без ошибки для пользователя!",
            "Проверить работу");
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
            ErrorMessage = $"Ошибка OSRM гео-сервера: {ex.Message}";
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
            ? "ВНИМАНИЕ: Включена симуляция обрыва связи! Модификации сохраняются в локальный JSON-лог отложенной синхронизации." 
            : null;
    }

    [RelayCommand]
    public async Task LogoutAsync()
    {
        _telemetryService.Stop();
        await _secureStorageService.ClearAuthAsync();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
