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
        ILocalDatabaseService databaseService)
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

        // Автоматическая миграция legacy JSON в реляционные таблицы SQLite при открытии
        await _databaseService.MigrateFromLegacyJsonAsync(DriverId);
        await _databaseService.GetOrCreateWaybillAsync(DriverId, DriverName, VehicleNumber);

        PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
        await UpdateDatabaseCountsAsync();
        UpdateTelemetryUI();

        await LoadDeliveriesAsync(refresh: true);
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

            // ШАГ 3: СИНХРОНИЗАЦИЯ ПОЛУЧЕННЫХ ИЗВНЕ СТРУКТУР С ЛОКАЛЬНОЙ БД SQLITE (Upsert)
            await _databaseService.SaveOrUpdateDeliveriesFromApiAsync(DriverId, response.Items);

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
            "🗑️ Удалить наряд (DELETE)",
            "📍 Отметить прибытие (PATCH)",
            "❌ Зафиксировать отказ от груза (PUT)");

        if (action == "📍 Отметить прибытие (PATCH)")
        {
            await MarkArrivalAsync(point);
        }
        else if (action == "❌ Зафиксировать отказ от груза (PUT)")
        {
            await RejectCargoAsync(point);
        }
        else if (action == "🗑️ Удалить наряд (DELETE)")
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
        point.Notes = $"Отметка прибытия: {DateTime.Now:HH:mm:ss} (зафиксировано в SQLite)";
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
            StatusNotice = $"ОФЛАЙН: Отметка прибытия ({point.OrderNumber}) атомарно сохранена в SQLite (IsSyncPending=true).";
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
            StatusNotice = $"PATCH: Прибытие по наряду {point.OrderNumber} подтверждено сервером и зафиксировано в SQLite!";
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
            StatusNotice = $"Сбой сети: Прибытие сохранено в SQLite со статусом IsSyncPending=true.";
        }
        finally
        {
            IsBusy = false;
            await UpdateDatabaseCountsAsync();
            ResetCancellation();
        }
    }

    /// <summary>
    /// Вариант 7: Контекстное меню -> «Зафиксировать отказ от груза» (PUT-запрос с указанием причины)
    /// Атомарно меняет состояние строки в таблице SQLite с внесением записи в trip_records.
    /// </summary>
    [RelayCommand]
    public async Task RejectCargoAsync(DeliveryPoint? point)
    {
        if (point == null) return;

        string? reason = await Shell.Current.DisplayPromptAsync(
            "Зафиксировать отказ от груза (PUT)",
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
        point.Notes = $"Акт отказа (SQLite): {reason}";
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
            StatusNotice = $"ОФЛАЙН: Отказ ({point.OrderNumber}) атомарно сохранен в SQLite (IsSyncPending=true).";
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
            StatusNotice = $"PUT: Акт отказа по заказу {point.OrderNumber} принят сервером и подтвержден в SQLite!";
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
            StatusNotice = $"Сбой сети: Отказ сохранен в SQLite со статусом IsSyncPending=true.";
        }
        finally
        {
            IsBusy = false;
            await UpdateDatabaseCountsAsync();
            ResetCancellation();
        }
    }

    /// <summary>
    /// Общее требование: Контекстное меню -> «Удалить точку» (DELETE-запрос к серверу)
    /// </summary>
    [RelayCommand]
    public async Task DeleteDeliveryAsync(DeliveryPoint? point)
    {
        if (point == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Удаление наряда (DELETE)",
            $"Вы действительно хотите удалить наряд {point.OrderNumber} ({point.ClientName}) из путевого листа?",
            "Удалить",
            "Отмена");

        if (!confirm) return;

        bool isOffline = IsOffline || _mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected;

        // 1. АТОМАРНОЕ УДАЛЕНИЕ / ПОМЕТКА НА УДАЛЕНИЕ В SQLITE
        await _databaseService.DeleteDeliveryPointAsync(point.Id, isOffline);

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
            StatusNotice = $"ОФЛАЙН: Запись {point.OrderNumber} помечена в SQLite на удаление (IsSyncPending=true).";
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
                StatusNotice = $"DELETE: Наряд {point.OrderNumber} успешно удален из SQLite и сервера!";
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
            StatusNotice = $"Сбой сети: Удаление помещено в лог синхронизации.";
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
            DatabaseStatusText = $"SQLite: {summary.DeliveriesCount} точек (путевой #{summary.WaybillCount})";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] UpdateDatabaseCounts error: {ex.Message}");
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
            $"📋 ТАБЛИЦА 1: waybills (Путевые листы водителей)\n" +
            $"• Записей в таблице: {summary.WaybillCount}\n" +
            $"• Активный путевой лист: #{waybill.Id} ({waybill.Status})\n" +
            $"• Дата смены: {waybill.WaybillDate:dd.MM.yyyy}\n" +
            $"• Водитель: {waybill.DriverName} ({waybill.VehicleNumber})\n" +
            $"• Начальный одометр: {waybill.InitialOdometerKm:F1} км\n" +
            $"• Текущий одометр: {waybill.CurrentOdometerKm:F1} км\n" +
            $"• Топливо в баке: {waybill.FuelRemainingLiters:F1} л (из {waybill.FuelStartingLiters:F1} л)\n\n" +
            $"📦 ТАБЛИЦА 2: delivery_points (Точки маршрутного листа)\n" +
            $"• Всего точек в SQLite: {summary.DeliveriesCount}\n" +
            $"• Ожидают отправки на сервер (IsSyncPending): {summary.PendingSyncCount}\n\n" +
            $"🚚 ТАБЛИЦА 3: trip_records (Рейсы и расход топлива ДУТ)\n" +
            $"• Зарегистрировано рейсов: {summary.TripRecordsCount}\n" +
            $"• Агрегированный пробег: {stats.TotalDistanceKm:F1} км\n" +
            $"• Израсходовано топлива: {stats.TotalFuelConsumedLiters:F2} л\n" +
            $"• Средний расход: {stats.AverageConsumptionPer100Km:F1} л/100 км\n" +
            $"• Средняя скорость движения: {stats.AverageSpeedKmh:F1} км/ч\n\n" +
            $"✅ МИГРАЦИЯ ДАННЫХ: Старые JSON-файлы успешно перенесены в реляционные таблицы SQLite!";

        await Shell.Current.DisplayAlertAsync("Инспектор СУБД SQLite", message, "Закрыть");
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
