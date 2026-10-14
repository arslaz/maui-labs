# Лабораторная работа: Логистический Навигатор Грузоперевозок (Вариант 7)
## Архитектура .NET MAUI + MVVM • Сквозная JWT-авторизация • Отложенная синхронизация • Cache-Aside

Проект модернизирован в полном соответствии с требованиями лабораторной работы по интеграции мобильных приложений с защищенными REST API сервисами в рамках шаблона **MVVM**.

---

### Реализованные требования и архитектурные компоненты

1. **Экран аутентификации и блокировка доступа**:
   - `LoginPage.xaml` / `LoginViewModel.cs`: Ввод табельного номера водителя (ID) и пароля.
   - `AppShell.xaml`: Навигация заблокирована (`Shell.TabBarIsVisible="False"`, `FlyoutBehavior="Disabled"`, `NavBarIsVisible="False"`). Доступ к основному содержимому (`DeliveriesPage`) невозможен без получения успешного ответа сервера с токенами сессии.
   - Сервер `POST /api/auth/login` генерирует пару токенов: кратковременный **Access Token** и долгоживущий **Refresh Token**.

2. **Сквозная авторизация и DelegatingHandler**:
   - `Services/AuthHeaderHandler.cs`: Специализированный перехватчик `DelegatingHandler`, внедренный в конвейер `HttpClient` («LogisticsClient») через `IHttpClientFactory` (`MauiProgram.cs`).
   - Автоматически считывает токен из `SecureStorage` и подписывает все запросы заголовком:
     ```http
     Authorization: Bearer <jwt-access-token>
     ```
   - При получении ответа `401 Unauthorized` перехватчик приостанавливает выполнение, отправляет фоновый запрос `POST /api/auth/refresh`, получает новую пару ключей, перезаписывает их в `SecureStorage` и прозрачно повторяет исходный запрос пользователя.
   - В UI добавлена кнопка **«🔑 Тест 401 Refresh»** для наглядной демонстрации авто-обновления сессии преподавателю.

3. **Безопасное хранение токенов и ключей (SecureStorage)**:
   - `Services/SecureStorageService.cs`: Реализация `ISecureStorageService` через платформенный класс `Microsoft.Maui.Storage.SecureStorage` (с автоматическим fallback для тестовых сред).
   - Хранит: Access Token (`auth_token`), Refresh Token (`refresh_token`), ID водителя (`driver_id`), а также симметричный 256-битный ключ шифрования отчетов (`fuel_report_aes_key`).

4. **Модификация ресурсов (PUT, PATCH, DELETE) исключительно через Контекстное Меню**:
   - `Views/DeliveriesPage.xaml` / `ViewModels/DeliveriesViewModel.cs`:
     - **«Отметить прибытие»** (Вариант 7): **PATCH-запрос** со штампом времени `arrivedAt` на `api/deliveries/{id}/arrival`. Статус меняется на «Прибыл» (бирюзовый цвет).
     - **«Зафиксировать отказ от груза»** (Вариант 7): **PUT-запрос** с указанием причины отказа `reason` на `api/deliveries/{id}/reject`. Статус меняется на «Отказ» (красный цвет).
     - **«Удалить наряд»** (Общее требование): **DELETE-запрос** на `api/deliveries/{id}`. Заявка удаляется из путевого листа.
   - Три способа вызова контекстного меню:
     1. Свайп по карточке влево (`SwipeView` -> `SwipeItems`);
     2. Нажатие на кнопку действий **«⋮»** на карточке (кроссплатформенный `DisplayActionSheetAsync`);
     3. Правый клик / долгое нажатие (`FlyoutBase.ContextFlyout` -> `MenuFlyout`).

5. **Стратегия кэширования Cache-Aside (Stale-While-Revalidate)**:
   - `Services/FileCacheService.cs` / `DeliveriesViewModel.LoadDeliveriesAsync`:
     - **Шаг 1**: Модель представления мгновенно считывает сериализованный JSON-кэш из локального хранилища `FileSystem.AppDataDirectory` (`deliveries_cache_{driverId}.json`) и мгновенно отображает список в UI без сетевой задержки.
     - **Шаг 2**: Асинхронно инициируется фоновый HTTP-запрос к API для актуализации данных.
     - **Шаг 3**: При получении свежего ответа список в UI обновляется, и новый JSON записывается в файл-кэш.

6. **Транзитный слой данных и отложенная синхронизация (Offline JSON-лог)**:
   - `Models/SyncTransaction.cs` и `Services/SyncQueueService.cs`:
     - Если пользователь выполняет модификацию в офлайн-режиме (или при обрыве связи), изменение оптимистично отражается в UI и кэше, а транзакция сериализуется в локальный JSON-лог:
       `FileSystem.AppDataDirectory/pending_sync_queue.json`.
     - В UI отображается информационная плашка: **«Очередь офлайн-транзакций: N»** с кнопкой синхронизации.
     - При восстановлении сети (`NetworkStatusService.ConnectivityChanged`) сервис автоматически отправляет накопленные транзакции на сервер и очищает JSON-лог.

7. **Зашифрованный отчет о расходе топлива и отправка бинарных данных (Вариант 7)**:
   - `Services/FuelReportService.cs`:
     - Формирует подробный отчет о расходе топлива, километраже, показаниях ДУТ и путевых точках.
     - Выполняет симметричное шифрование текста алгоритмом **AES-256-CBC** с ключом из `SecureStorage`.
     - Записывает зашифрованный файл `.enc` в `FileSystem.AppDataDirectory` и дублирует запись в локальный кэш-файл `fuel_reports_cache.json`.
   - `Services/LogisticsApiService.UploadFuelReportAsync`:
     - Отправляет бинарный файл через `MultipartFormDataContent` со `StreamContent` (`application/octet-stream`) и метаданными `POST /api/reports/fuel`, в точности как описано в теоретической части задания.

8. **Встроенный тестировочный сервер (MockBackendServer)**:
   - `Services/MockBackendServer.cs`: Полнофункциональный локальный сервер на `System.Net.HttpListener` (`http://127.0.0.1:5055`), обрабатывающий реальные HTTP-запросы (Bearer Token, Refresh, PATCH arrival, PUT reject, DELETE, Multipart POST fuel reports).
   - Включает кнопку симуляции офлайна (код 503) для тестирования кэша и очереди транзакций.

---

### Структура ключевых файлов проекта

```
laba12/
├── Models/
│   ├── AuthModels.cs            # LoginRequest, AuthResponse (JWT + Refresh)
│   ├── DeliveryPoint.cs         # Точка доставки (ObservableObject, Status, Notes)
│   ├── DeliveryListResponse.cs   # Ответ с пагинацией
│   ├── SyncTransaction.cs       # Модель отложенной транзакции офлайн-лога
│   ├── FuelReportModels.cs      # Модель отчета о расходе топлива и результат загрузки
│   └── TelemetryData.cs         # Модель GPS/ЭБУ телеметрии
├── Services/
│   ├── AuthHeaderHandler.cs     # DelegatingHandler: инжекция Bearer Token и Refresh на 401
│   ├── SyncQueueService.cs      # Управление очередью отложенных транзакций (JSON-лог)
│   ├── FuelReportService.cs     # Генерация отчета, шифрование AES-256, дублирование в кэш
│   ├── LogisticsApiService.cs   # REST API клиент (PATCH, PUT, DELETE, Multipart POST)
│   ├── SecureStorageService.cs  # Хранилище SecureStorage (токены, AES-ключ)
│   ├── FileCacheService.cs      # Cache-Aside сериализация в AppDataDirectory
│   ├── MockBackendServer.cs     # Тестовый HTTP-сервер для демонстрации
│   └── NetworkStatusService.cs  # Мониторинг сетевого подключения Connectivity
├── ViewModels/
│   ├── LoginViewModel.cs        # Блокирующий экран входа
│   └── DeliveriesViewModel.cs   # Список, контекстное меню, Cache-Aside, офлайн-лог
└── Views/
    ├── LoginPage.xaml           # Вход водителя в систему
    └── DeliveriesPage.xaml      # Главный экран (SwipeView, контекстное меню, плашки)
```

---

### Как продемонстрировать работу преподавателю (Чек-лист защиты)

#### 1. Блокирующий экран входа и токены в SecureStorage
1. Запустите приложение. Отображается экран **«LogiRoute Pro: Авторизация водителя»**. Перейти к доставкам без успешного ответа сервера невозможно (все вкладки и навигация заблокированы).
2. **Проверка отклонения неверных данных (401 Unauthorized)**:
   - Введите любой неверный пароль (например, логин `DRV-104` и пароль `0000`) или несуществующий ID `DRV-999`.
   - Нажмите «Войти в систему» — сервер возвращает `401 Unauthorized`, на экране появляется красная плашка:
     *«Неверный табельный номер (ID) или пароль доступа. Сервер отклонил аутентификацию устройства (JWT 401).»*
     Приложение **не пускает** пользователя внутрь.
3. **Успешный вход под зарегистрированными водителями**:
   - `DRV-104` (пароль `1234`) — входит Алексей Соколов (ГАЗель NEXT);
   - `DRV-105` (пароль `4321`) — входит Дмитрий Ковалев (МАЗ-Купава).
4. Покажите в коде `Services/LogisticsApiService.cs` и `Services/SecureStorageService.cs`: сервер возвращает JWT Access Token и Refresh Token, которые сохраняются в `SecureStorage`.

#### 2. Автоматическая подпись Bearer Token и DelegatingHandler (401 Refresh)
1. Откройте `Services/AuthHeaderHandler.cs` и покажите реализацию `DelegatingHandler`:
   - Запросы подписываются заголовком `Authorization: Bearer <token>`;
   - При ответе 401 перехватчик блокирует поток, отправляет скрытый запрос `/api/auth/refresh`, сохраняет новые ключи в `SecureStorage` и повторяет исходный запрос.
2. Для живой демонстрации нажмите фиолетовую кнопку **«🔑 Тест 401 Refresh»** в шапке экрана. Токен в SecureStorage помечается как просроченный.
3. Потяните список вниз (Pull-to-Refresh) или нажмите «⚡ Отправить пинг». В консоли логов отобразится:
   `[AuthHeaderHandler] Истек срок действия токена. Отправка фонового запроса /api/auth/refresh... Токен успешно обновлен! Повтор исходного запроса.` Запрос успешно выполняется без сбоя для пользователя!

#### 3. Контекстное меню (PATCH, PUT, DELETE)
1. В списке на любой доставке нажмите кнопку **«⋮»** или свайпните влево:
   - **«📍 Отметить прибытие (PATCH)»**: отправляет PATCH со штампом времени. Статус заказа меняется на **«Прибыл»** (бирюзовый), в примечании фиксируется время прибытия.
   - **«❌ Зафиксировать отказ от груза (PUT)»**: открывает окно ввода причины (например, *«Брак упаковки»*) и отправляет PUT на сервер. Статус меняется на **«Отказ»** (красный).
   - **«🗑️ Удалить наряд (DELETE)»**: запрашивает подтверждение и отправляет DELETE на сервер, удаляя наряд из путевого листа.

#### 4. Стратегия Cache-Aside
1. Покажите метод `LoadDeliveriesAsync` в `ViewModels/DeliveriesViewModel.cs`:
   - Сначала мгновенно читается локальный JSON из `FileSystem.AppDataDirectory` (`deliveries_cache_{driverId}.json`), и список отображается моментально.
   - Затем выполняется фоновый сетевой запрос `GetDeliveriesAsync`.
   - При получении ответа UI обновляется, и новый снимок сохраняется в локальный кэш-файл.

#### 5. Офлайн-режим и отложенная синхронизация (JSON-лог)
1. В шапке нажмите кнопку **«Офлайн-тест»** (загорается красный бейдж «Офлайн: ВКЛ»).
2. Выполните команду контекстного меню (например, «Отметить прибытие» или «Отказ от груза»).
3. Приложение не падает: изменение применяется в UI, а транзакция сохраняется в локальный JSON-файл `pending_sync_queue.json`.
4. Вверху появляется оранжевая плашка: **«Очередь офлайн-транзакций: 1»**.
5. Нажмите кнопку **«Офлайн: ВКЛ»** (выключаем симуляцию офлайна) и нажмите **«Синхронизировать»** (или дождитесь автоматической отправки).
6. Транзакции из JSON-лога отправляются на сервер, лог очищается, а данные актуализируются!

#### 6. Зашифрованный отчет о расходе топлива (AES-256 + Multipart POST)
1. В шапке нажмите зеленую кнопку **«📊 Отчет топлива (AES)»**.
2. Приложение:
   - Агрегирует данные о поездках и расходе топлива из локальной таблицы SQLite `trip_records`;
   - Формирует структурированный текстовый отчет о километраже и расходе топлива;
   - Шифрует его алгоритмом **AES-256-CBC** с ключом из `SecureStorage`;
   - Сохраняет файл `.enc` в `FileSystem.AppDataDirectory` и дублирует запись в `fuel_reports_cache.json`;
   - Отправляет файл на сервер через `MultipartFormDataContent` со `StreamContent` (`application/octet-stream`) методом `POST /api/reports/fuel`.
3. На экране появляется подтверждение сервера с именем файла, размером в байтах и типом шифрования.

---

## 🗄️ Лабораторная работа № 14: Локальные базы данных. Базовые операции (SQLite-net-pcl)
### Вариант 7: Логистический Навигатор Грузоперевозок

### 1. Архитектура слоя локального хранения данных
- **Библиотека**: `sqlite-net-pcl` (v1.11.285) — легковесная асинхронная ORM-библиотека для .NET MAUI.
- **Сервис БД**: `ILocalDatabaseService` / `LocalDatabaseService`:
  - Зарегистрирован как **Singleton** в `MauiProgram.cs`:
    ```csharp
    builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();
    ```
  - Внедряется во все ViewModels (`DeliveriesViewModel`, `LoginViewModel`, `DeliveryDetailViewModel`, `FuelReportService`) через конструктор по паттерну Dependency Injection.
  - Потокобезопасность обеспечивается встроенным пулом асинхронных операций `SQLiteAsyncConnection` и семафором `SemaphoreSlim`.
- **Файл базы данных**:
  `Path.Combine(FileSystem.AppDataDirectory, "app_local_storage.db3")`
  - **macOS**: `~/Library/Containers/com.companyname.laba12/Data/Library/app_local_storage.db3`
  - **Android (Samsung)**: `/data/user/0/com.companyname.laba12/files/app_local_storage.db3`

### 2. Спроектированные таблицы базы данных (Декларативные ORM-атрибуты)
В файле `Models/DatabaseEntities.cs` определены 3 взаимосвязанные таблицы:

1. **Таблица `waybills` (`WaybillEntity`)** — путевые листы водителей и профиль смены на текущий день:
   - `[PrimaryKey, AutoIncrement] public int Id { get; set; }`
   - `[Indexed, MaxLength(50)] public string DriverId { get; set; }`
   - `[MaxLength(100)] public string DriverName { get; set; }`
   - `[MaxLength(50)] public string VehicleNumber { get; set; }`
   - `[Indexed] public DateTime WaybillDate { get; set; }`
   - `InitialOdometerKm`, `CurrentOdometerKm` — показатели спидометра (пробег);
   - `FuelStartingLiters`, `FuelConsumedLiters`, `FuelRemainingLiters` — показатели датчиков уровня топлива (ДУТ);
   - `[MaxLength(50)] public string Status { get; set; }` — статус путевого листа («Активен», «Завершен»).

2. **Таблица `delivery_points` (`DeliveryPointEntity`)** — основная таблица точек доставки текущего дня:
   - `[PrimaryKey, MaxLength(50)] public string Id { get; set; }`
   - `[Indexed, MaxLength(50)] public string DriverId { get; set; }` (внешняя связь с водителем и путевым листом);
   - `[Indexed, MaxLength(50)] public string OrderNumber { get; set; }`
   - `[MaxLength(150)] public string ClientName { get; set; }`
   - `[MaxLength(250)] public string Address { get; set; }`
   - `[Indexed, MaxLength(50)] public string Status { get; set; }` («Ожидает», «В пути», «Прибыл», «Отказ», «Доставлен»);
   - `public DateTime? ArrivedAt { get; set; }` — штамп времени отметки прибытия (PATCH);
   - `[MaxLength(300)] public string? RejectionReason { get; set; }` — причина отказа от груза (PUT);
   - **Специальные поля отложенной синхронизации**:
     - `[Indexed] public bool IsSyncPending { get; set; }` — флаг неотправленной на сервер модификации;
     - `[MaxLength(50)] public string? PendingOperation { get; set; }` — HTTP-метод отложенного запроса (`PATCH_ARRIVAL`, `PUT_REJECT`, `DELETE`).

3. **Таблица `trip_records` (`TripRecordEntity`)** — журнал рейсов и датчиков расхода топлива:
   - `[PrimaryKey, AutoIncrement] public int Id { get; set; }`
   - `[Indexed, MaxLength(50)] public string DriverId { get; set; }`
   - `public DateTime Timestamp { get; set; }`
   - `public double DistanceKm { get; set; }`
   - `public double FuelLiters { get; set; }`
   - `public double SpeedKmh { get; set; }`
   - `public string EventDescription { get; set; }`

### 3. Автоматическая миграция из старых JSON-файлов кэша
Метод `MigrateFromLegacyJsonAsync(string driverId)` в `LocalDatabaseService.cs`:
- При первом запуске приложения или после успешной авторизации автоматически находит файлы:
  - `deliveries_cache_{driverId}.json` (старый файловый кэш) -> считывает и вставляет все точки в таблицу `delivery_points`;
  - `pending_sync_queue.json` (старая очередь) -> помечает соответствующие строки флагом `IsSyncPending = true` и сохраняет тип операции;
  - Создает стартовый путевой лист в таблице `waybills` и инициализирует записи рейсов в `trip_records`.
- Выставляет флаг миграции в `Preferences.Default`, исключая повторную миграцию.

### 4. Мгновенная загрузка данных и синхронизация с API
- В `DeliveriesViewModel.LoadDeliveriesAsync`:
  - **Шаг 1**: Экран **мгновенно** выполняет выборку из таблицы SQLite:
    ```csharp
    var localPoints = await _databaseService.GetDeliveryPointsAsync(DriverId);
    ```
    Интерфейс отрисовывается без малейшей сетевой задержки.
  - **Шаг 2**: Фоновый запрос к REST API переводит приложение в состояние загрузки.
  - **Шаг 3**: Полученные структуры синхронизируются с базой данных через Upsert (`SaveOrUpdateDeliveriesFromApiAsync`), при этом локальные несохраненные статусы (`IsSyncPending == true`) не затираются.

### 5. Атомарные операции контекстного меню (Вариант 7)
- **Отметка прибытия (PATCH)**:
  - Вызывает `_databaseService.MarkArrivalAsync(point.Id, arrivalTime, isOffline)`:
    - Атомарно обновляет строку в `delivery_points` (`Status = "Прибыл"`, `ArrivedAt = arrivalTime`);
    - Добавляет запись рейса в `trip_records` с пробегом и расходом топлива;
    - Выставляет `IsSyncPending = isOffline`.
- **Фиксация отказа от груза (PUT)**:
  - Вызывает `_databaseService.MarkRejectAsync(point.Id, reason, isOffline)`:
    - Атомарно обновляет строку в `delivery_points` (`Status = "Отказ"`, `RejectionReason = reason`);
    - Добавляет запись в `trip_records`;
    - Выставляет `IsSyncPending = isOffline`.
- **Удаление наряда (DELETE)**:
  - Вызывает `_databaseService.DeleteDeliveryPointAsync(point.Id, isOffline)`:
    - В офлайне помечает строку флагом `PendingOperation = "DELETE"`;
    - В онлайне физически удаляет строку из таблицы SQLite.

### 6. Зашифрованный отчет о топливе на основе агрегации SQLite (Вариант 7)
- Метод `_databaseService.GetAggregatedTripStatisticsAsync(driverId)` выполняет агрегацию по записям таблицы `trip_records`:
  - `TotalTrips` — количество зарегистрированных поездок;
  - `TotalDistanceKm = trips.Sum(x => x.DistanceKm)`;
  - `TotalFuelConsumedLiters = trips.Sum(x => x.FuelLiters)`;
  - `AverageConsumptionPer100Km = (TotalFuel / TotalDistance) * 100.0`;
  - `AverageSpeedKmh = trips.Average(x => x.SpeedKmh)`.
- `FuelReportService` связывает отчет с активным путевым листом из `waybills`, вносит точные агрегированные показатели датчиков, шифрует текст алгоритмом **AES-256** и передает в API.

---

### Чек-лист демонстрации Лабораторной работы № 14 преподавателю

1. **Архитектура и DI-контейнер (Singleton)**:
   - Откройте `MauiProgram.cs` и покажите строку 42:
     `builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();`
   - Покажите конструкторы `DeliveriesViewModel.cs`, `LoginViewModel.cs`, `DeliveryDetailViewModel.cs`, `FuelReportService.cs`, куда внедряется `ILocalDatabaseService`.

2. **Декларативные атрибуты ORM и связанные таблицы**:
   - Откройте `Models/DatabaseEntities.cs` и покажите:
     - `[Table("waybills")]`, `[PrimaryKey, AutoIncrement]`, `[Indexed]`, `[MaxLength(50)]`;
     - `[Table("delivery_points")]`, поле `[Indexed] public bool IsSyncPending { get; set; }`;
     - `[Table("trip_records")]` для агрегации рейсов и расхода топлива.

3. **Инспектор базы данных в приложении**:
   - В запущенном приложении на главном экране нажмите синюю кнопку **«🗄️ База SQLite»**.
   - Откроется окно инспектора, где выводятся:
     - Физический путь к файлу `app_local_storage.db3` на устройстве;
     - Количество записей в таблице `waybills` и параметры активного путевого листа;
     - Количество точек в таблице `delivery_points` и счетчик `IsSyncPending`;
     - Количество записей в таблице `trip_records`, суммарный пробег и расход топлива;
     - Подтверждение успешной миграции из старых JSON-файлов.

4. **Мгновенная загрузка при открытии**:
   - Покажите `DeliveriesViewModel.LoadDeliveriesAsync`: Шаг 1 считывает данные из `_databaseService.GetDeliveryPointsAsync(DriverId)` моментально, а сетевой запрос запускается асинхронно для фоновой синхронизации.

5. **Офлайн-модификация строк и флаг IsSyncPending**:
   - Нажмите кнопку **«Офлайн-тест»** (включается автономный режим).
   - В контекстном меню любой точки доставки (например, `ORD-BY-102`) выберите **«📍 Отметить прибытие (PATCH)»** или **«❌ Зафиксировать отказ от груза (PUT)»**.
   - На карточке мгновенно появляется коричневый бейдж: **`💾 SQLite: офлайн`**.
   - Нажмите **«🗄️ База SQLite»** — счетчик `Ожидают отправки (IsSyncPending)` увеличился!
   - Выключите **«Офлайн-тест»** и нажмите **«Синхронизировать»**.
   - Приложение находит строки с `IsSyncPending == true`, отправляет запросы к серверу, очищает флаг `IsSyncPending` в SQLite и убирает бейдж!

6. **Агрегированный отчет о расходе топлива (Вариант 7)**:
   - Нажмите **«📊 Отчет топлива (AES)»**.
   - Покажите в коде `FuelReportService.cs`, что отчет строится на вызове `_databaseService.GetAggregatedTripStatisticsAsync(driverId)`, агрегирующем реальные данные из таблицы `trip_records`, после чего зашифрованный AES-256 файл передается на сервер.
