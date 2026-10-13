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

    // Свойства отложенной синхронизации (Offline JSON Queue)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingSyncTransactions))]
    [NotifyPropertyChangedFor(nameof(PendingSyncBadgeText))]
    private int pendingSyncCount;

    public bool HasPendingSyncTransactions => PendingSyncCount > 0;
    public string PendingSyncBadgeText => $"Очередь офлайн-транзакций: {PendingSyncCount}";

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
        IFuelReportService fuelReportService)
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

        PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
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
    /// СТРАТЕГИЯ CACHE-ASIDE:
    /// Сначала мгновенно считывает информацию из локального файла-кэша для быстрого рендера UI,
    /// и лишь затем инициирует асинхронный фоновый запрос к REST API для актуализации данных.
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

        // ШАГ 1: МГНОВЕННОЕ ЧТЕНИЕ ИЗ ЛОКАЛЬНОГО JSON-КЭША (Стратегия Cache-Aside)
        var cached = await _cacheService.GetCachedDeliveriesAsync(DriverId);
        var lastCacheTime = await _cacheService.GetLastCacheTimeAsync(DriverId);

        if (cached != null && cached.Any())
        {
            Deliveries.Clear();
            foreach (var item in cached)
            {
                Deliveries.Add(item);
            }
            TotalCount = Deliveries.Count;
            LastUpdatedText = $"Локальный кэш: {lastCacheTime:dd.MM.yyyy HH:mm:ss}";
        }

        // Если физическая сеть отсутствует или включена симуляция офлайна
        if (!_networkStatusService.IsConnected || _mockBackendServer.SimulateOffline)
        {
            IsOffline = true;
            CacheNotice = $"ОФЛАЙН: Телефон работает автономно. Отображен кэш ({lastCacheTime:HH:mm:ss}) из AppDataDirectory.";
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

            Deliveries.Clear();
            foreach (var item in response.Items)
            {
                Deliveries.Add(item);
            }

            TotalCount = response.TotalCount;
            HasMoreItems = response.HasMore;
            IsOffline = false;
            CacheNotice = null;
            LastUpdatedText = $"REST API (Bearer Token): {DateTime.Now:HH:mm:ss}";

            // ШАГ 3: ОБНОВЛЕНИЕ ЛОКАЛЬНОГО КЭША В AppDataDirectory
            await _cacheService.SaveDeliveriesAsync(DriverId, Deliveries.ToList());
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Загрузка данных отменена пользователем.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] Сетевой запрос завершился ошибкой: {ex.Message}. Кэш уже отображен.");
            IsOffline = true;
            CacheNotice = $"ОФЛАЙН: Сервер недоступен ({ex.Message}). Показаны данные из локального кэша.";
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
    /// </summary>
    [RelayCommand]
    public async Task MarkArrivalAsync(DeliveryPoint? point)
    {
        if (point == null) return;

        var arrivalTime = DateTime.UtcNow;

        // Мгновенное оптимистичное обновление UI и локального кэша
        point.Status = "Прибыл";
        point.Notes = $"Прибытие зафиксировано: {DateTime.Now:HH:mm:ss}";
        point.UpdatedAt = arrivalTime;
        await _cacheService.UpdateCachedPointAsync(DriverId, point);

        // Проверка: офлайн или сбой сети
        if (IsOffline || _mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected)
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
            StatusNotice = $"ОФЛАЙН: Отметка прибытия ({point.OrderNumber}) сохранена в JSON-лог отложенной синхронизации.";
            return;
        }

        // Онлайн: отправка PATCH-запроса на сервер
        IsBusy = true;
        try
        {
            var token = CreateLinkedToken(TimeSpan.FromSeconds(10));
            var updated = await _apiService.MarkArrivalAsync(point.Id, arrivalTime, token);
            if (updated != null)
            {
                point.Status = updated.Status;
                point.Notes = updated.Notes;
                await _cacheService.UpdateCachedPointAsync(DriverId, point);
            }
            StatusNotice = $"PATCH: Прибытие по наряду {point.OrderNumber} успешно подтверждено сервером!";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] PATCH error: {ex.Message}. Сохранение в JSON-лог.");
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
            StatusNotice = $"Сбой сети: Отметка прибытия помещена в JSON-лог синхронизации.";
        }
        finally
        {
            IsBusy = false;
            ResetCancellation();
        }
    }

    /// <summary>
    /// Вариант 7: Контекстное меню -> «Зафиксировать отказ от груза» (PUT-запрос с указанием причины)
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

        // Мгновенное оптимистичное обновление UI и локального кэша
        point.Status = "Отказ";
        point.Notes = $"Причина отказа: {reason}";
        point.UpdatedAt = rejectTime;
        await _cacheService.UpdateCachedPointAsync(DriverId, point);

        // Проверка: офлайн
        if (IsOffline || _mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected)
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
            StatusNotice = $"ОФЛАЙН: Отказ от груза ({point.OrderNumber}) сохранен в JSON-лог отложенной синхронизации.";
            return;
        }

        // Онлайн: отправка PUT-запроса на сервер
        IsBusy = true;
        try
        {
            var token = CreateLinkedToken(TimeSpan.FromSeconds(10));
            var updated = await _apiService.RejectCargoAsync(point.Id, reason, token);
            if (updated != null)
            {
                point.Status = updated.Status;
                point.Notes = updated.Notes;
                await _cacheService.UpdateCachedPointAsync(DriverId, point);
            }
            StatusNotice = $"PUT: Акт отказа по заказу {point.OrderNumber} успешно принят сервером!";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] PUT error: {ex.Message}. Сохранение в JSON-лог.");
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
            StatusNotice = $"Сбой сети: Отказ от груза помещен в JSON-лог синхронизации.";
        }
        finally
        {
            IsBusy = false;
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

        // Удаление из UI и обновление локального кэша
        Deliveries.Remove(point);
        TotalCount = Deliveries.Count;
        await _cacheService.SaveDeliveriesAsync(DriverId, Deliveries.ToList());

        // Проверка: офлайн
        if (IsOffline || _mockBackendServer.SimulateOffline || !_networkStatusService.IsConnected)
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
            StatusNotice = $"ОФЛАЙН: Запрос на удаление ({point.OrderNumber}) сохранен в JSON-лог синхронизации.";
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
                StatusNotice = $"DELETE: Наряд {point.OrderNumber} успешно удален на сервере!";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeliveriesViewModel] DELETE error: {ex.Message}. Сохранение в JSON-лог.");
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
            StatusNotice = $"Сбой сети: Удаление помещено в JSON-лог синхронизации.";
        }
        finally
        {
            IsBusy = false;
            ResetCancellation();
        }
    }

    // =========================================================================
    // ОТЛОЖЕННАЯ СИНХРОНИЗАЦИЯ (ОБРАБОТКА ОФЛАЙН JSON-ЛОГА)
    // =========================================================================

    [RelayCommand]
    public async Task ProcessSyncQueueAsync()
    {
        if (PendingSyncCount == 0)
        {
            await Shell.Current.DisplayAlertAsync("Синхронизация", "Локальный JSON-лог синхронизации пуст.", "OK");
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
        StatusNotice = "Синхронизация отложенных транзакций с сервером...";
        try
        {
            var result = await _syncQueueService.ProcessQueueAsync(_apiService);
            PendingSyncCount = await _syncQueueService.GetPendingCountAsync();
            StatusNotice = result.Message;
            await Shell.Current.DisplayAlertAsync("Отложенная синхронизация", result.Message, "OK");
            await LoadDeliveriesAsync(refresh: true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Сбой при синхронизации JSON-лога: {ex.Message}";
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
