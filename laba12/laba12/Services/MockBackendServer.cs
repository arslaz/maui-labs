using System.Net;
using System.Text;
using System.Text.Json;
using laba12.Models;

namespace laba12.Services;

public class MockBackendServer : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly List<DeliveryPoint> _deliveryPoints = new();
    private readonly List<TelemetryData> _telemetryLogs = new();
    private readonly List<string> _uploadedFuelReports = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string BaseUrl { get; private set; } = "http://127.0.0.1:5055";
    public bool IsRunning => _listener?.IsListening ?? false;
    public bool SimulateOffline { get; set; } = false;
    public int SimulatedDelayMs { get; set; } = 400;

    public MockBackendServer()
    {
        SeedData();
    }

    private void SeedData()
    {
        _deliveryPoints.Clear();
        var baseDate = DateTime.Now;

        var points = new (string Num, string Client, string Addr, string Phone, string Cargo, double W, double V, string Prio, string Status, double Lat, double Lon, string Time)[]
        {
            ("ORD-BY-101", "ООО «Евроопт Логистик»", "ул. Монтажников, 2", "+375 (29) 111-22-33", "Охлажденные продукты", 420.0, 2.8, "Срочно", "В пути", 53.8752, 27.4241, "10:30"),
            ("ORD-BY-102", "ТК «Автолайт Экспресс»", "ул. Бабушкина, 27 (Колядичи)", "+375 (29) 222-33-44", "Сборный экспресс-груз", 310.0, 3.1, "Высокий", "Ожидает", 53.8154, 27.5852, "11:45"),
            ("ORD-BY-103", "ООО «21vek.by Склад»", "ул. Тростенецкая, 17", "+375 (29) 333-44-55", "Бытовая техника и электроника", 280.0, 3.5, "Срочно", "Ожидает", 53.8821, 27.5942, "12:30"),
            ("ORD-BY-104", "РУП «Белфармация»", "пр-т Дзержинского, 104", "+375 (29) 444-55-66", "Медикаменты (термобокс)", 65.0, 0.5, "Срочно", "Ожидает", 53.8612, 27.4821, "13:40"),
            ("ORD-BY-105", "Сеть «Гиппо»", "пр-т Рокоссовского, 2", "+375 (29) 555-66-77", "Бакалея и напитки", 650.0, 4.2, "Обычный", "Ожидает", 53.8732, 27.5985, "14:50"),
            ("ORD-BY-106", "Гипермаркет «Корона»", "ул. Кальварийская, 24", "+375 (29) 666-77-88", "Премиальные продукты", 190.0, 1.4, "Высокий", "Ожидает", 53.9064, 27.5251, "15:30"),
            ("ORD-BY-107", "Гипермаркет «ОМА»", "ул. Ванеева, 38", "+375 (29) 777-88-99", "Строительные материалы", 920.0, 5.0, "Обычный", "Ожидает", 53.8720, 27.6080, "16:15"),
            ("ORD-BY-108", "ТРЦ «Dana Mall»", "ул. Петра Мстиславца, 11", "+375 (29) 888-99-00", "Одежда и электроника", 210.0, 1.8, "Высокий", "Ожидает", 53.9332, 27.6521, "17:00"),
            ("ORD-BY-109", "ОАО «МТЗ (Тракторный завод)»", "ул. Долгобродская, 29", "+375 (29) 999-00-11", "Промышленные комплектующие", 840.0, 4.0, "Обычный", "Ожидает", 53.8962, 27.6195, "18:00"),
            ("ORD-BY-110", "ОАО «Керамин»", "ул. Серова, 22", "+375 (29) 123-45-67", "Керамическая плитка", 1100.0, 5.5, "Обычный", "Ожидает", 53.8421, 27.5450, "19:00"),
            ("ORD-BY-111", "ТЛЦ «Прилесье»", "Минский р-н, трасса М4", "+375 (29) 234-56-78", "Таможенные сборные паллеты", 780.0, 4.8, "Срочно", "Ожидает", 53.8340, 27.7650, "20:00"),
            ("ORD-BY-112", "Парк «Великий Камень»", "пр-т Пекинский, 18", "+375 (29) 345-67-89", "Высокоточное оборудование", 350.0, 2.2, "Обычный", "Ожидает", 53.9050, 28.0200, "21:00")
        };

        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            _deliveryPoints.Add(new DeliveryPoint
            {
                Id = $"deliv-by-{i + 1}",
                OrderNumber = p.Num,
                ClientName = p.Client,
                Address = p.Addr,
                City = "Минск (РБ)",
                Phone = p.Phone,
                CargoDescription = p.Cargo,
                WeightKg = p.W,
                VolumeM3 = p.V,
                Priority = p.Prio,
                Status = p.Status,
                Latitude = p.Lat,
                Longitude = p.Lon,
                EstimatedTime = p.Time,
                Notes = i == 0 ? "Въезд через шлагбаум со двора, пропуск на КПП." : "Служебный вход для разгрузки.",
                UpdatedAt = baseDate.AddMinutes(-i * 15)
            });
        }
    }

    public bool Start(int port = 5055)
    {
        if (IsRunning) return true;

        for (int tryPort = port; tryPort < port + 10; tryPort++)
        {
            try
            {
                var prefix = $"http://127.0.0.1:{tryPort}/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                _listener.Start();

                BaseUrl = $"http://127.0.0.1:{tryPort}";
                _cts = new CancellationTokenSource();

                Task.Run(() => ListenLoopAsync(_cts.Token));
                System.Diagnostics.Debug.WriteLine($"[MockBackendServer] Успешно запущен на {BaseUrl}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MockBackendServer] Порт {tryPort} занят: {ex.Message}");
                _listener?.Close();
                _listener = null;
            }
        }

        return false;
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
        }
        catch { }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MockBackendServer] Ошибка сокета: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS headers
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        if (SimulatedDelayMs > 0)
        {
            await Task.Delay(SimulatedDelayMs);
        }

        if (SimulateOffline)
        {
            response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            var errBytes = Encoding.UTF8.GetBytes("{\"error\": \"Сетевой сервер временно недоступен (симуляция разрыва связи)\"}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = errBytes.Length;
            await response.OutputStream.WriteAsync(errBytes);
            response.Close();
            return;
        }

        var path = request.Url?.AbsolutePath ?? "/";
        var method = request.HttpMethod.ToUpperInvariant();

        try
        {
            // 1. АУТЕНТИФИКАЦИЯ: POST /api/auth/login
            if (path == "/api/auth/login" && method == "POST")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                var loginReq = JsonSerializer.Deserialize<LoginRequest>(body, _jsonOptions);

                var reqDriverId = loginReq?.DriverId?.Trim() ?? string.Empty;
                var reqPassword = loginReq?.Password?.Trim() ?? string.Empty;

                // База зарегистрированных водителей транспортной компании
                var registeredDrivers = new Dictionary<string, (string Password, string Name, string Vehicle)>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DRV-104"] = ("1234", "Алексей Соколов (Водитель 1 класса)", "АВ 7412-7 (ГАЗель NEXT РБ)"),
                    ["DRV-105"] = ("4321", "Дмитрий Ковалев (Водитель 2 класса)", "ВК 3829-7 (МАЗ-Купава РБ)"),
                    ["DRV-106"] = ("5678", "Максим Васильев (Экспедитор)", "ЕН 1102-7 (ГАЗон Next РБ)")
                };

                if (registeredDrivers.TryGetValue(reqDriverId, out var driverInfo) && driverInfo.Password == reqPassword)
                {
                    var authRes = new AuthResponse
                    {
                        Success = true,
                        Token = $"jwt-access-{Guid.NewGuid():N}",
                        RefreshToken = $"refresh-token-{Guid.NewGuid():N}",
                        DriverId = reqDriverId.ToUpperInvariant(),
                        DriverName = driverInfo.Name,
                        VehicleNumber = driverInfo.Vehicle,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(15)
                    };

                    System.Diagnostics.Debug.WriteLine($"[MockBackendServer] Успешный вход: {driverInfo.Name} ({reqDriverId})");
                    await SendJsonResponseAsync(response, HttpStatusCode.OK, authRes);
                    return;
                }

                // Неверный логин или пароль -> блокировка и возврат 401 Unauthorized
                System.Diagnostics.Debug.WriteLine($"[MockBackendServer] Ошибка авторизации: неверный логин '{reqDriverId}' или пароль '{reqPassword}' -> 401 Unauthorized");
                await SendJsonResponseAsync(response, HttpStatusCode.Unauthorized, new AuthResponse
                {
                    Success = false,
                    ErrorMessage = "Неверный табельный номер (ID) или пароль доступа. Сервер отклонил аутентификацию устройства (JWT 401)."
                });
                return;
            }

            // 2. ОБНОВЛЕНИЕ ТОКЕНА (REFRESH SESSION): POST /api/auth/refresh
            if (path == "/api/auth/refresh" && method == "POST")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();

                var authRes = new AuthResponse
                {
                    Success = true,
                    Token = $"jwt-access-renewed-{Guid.NewGuid():N}",
                    RefreshToken = $"refresh-token-renewed-{Guid.NewGuid():N}",
                    DriverId = "DRV-104",
                    DriverName = "Алексей Соколов (Водитель 1 класса)",
                    VehicleNumber = "АВ 7412-7 (ГАЗель NEXT РБ)",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15)
                };

                System.Diagnostics.Debug.WriteLine("[MockBackendServer] Сессия обновлена через Refresh Token. Выдан новый JWT Access Token.");
                await SendJsonResponseAsync(response, HttpStatusCode.OK, authRes);
                return;
            }

            // Для всех бизнес-эндпоинтов проверяем Bearer-токен
            if (!ValidateAuthHeader(request, out var authError))
            {
                await SendJsonResponseAsync(response, HttpStatusCode.Unauthorized, new { error = authError });
                return;
            }

            // 3. ПОЛУЧЕНИЕ ТОЧЕК ДОСТАВКИ: GET /api/drivers/{id}/deliveries
            if (path.StartsWith("/api/drivers/") && path.Contains("/deliveries") && method == "GET")
            {
                var query = request.Url?.Query;
                int page = 1;
                int pageSize = 5;

                if (!string.IsNullOrEmpty(query))
                {
                    var parsed = System.Web.HttpUtility.ParseQueryString(query);
                    if (int.TryParse(parsed["page"], out var p)) page = Math.Max(1, p);
                    if (int.TryParse(parsed["pageSize"], out var s)) pageSize = Math.Max(1, s);
                }

                var total = _deliveryPoints.Count;
                var skip = (page - 1) * pageSize;
                var items = _deliveryPoints.Skip(skip).Take(pageSize).ToList();
                var hasMore = skip + items.Count < total;

                var result = new DeliveryListResponse
                {
                    Items = items,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize,
                    HasMore = hasMore
                };

                await SendJsonResponseAsync(response, HttpStatusCode.OK, result);
                return;
            }

            // 4. ВАРИАНТ 7: КОМАНДА «ОТМЕТИТЬ ПРИБЫТИЕ» (PATCH /api/deliveries/{id}/arrival)
            if (path.StartsWith("/api/deliveries/") && path.EndsWith("/arrival") && method == "PATCH")
            {
                var id = path.Substring("/api/deliveries/".Length).Replace("/arrival", "").Trim();
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();

                DateTime arrivalTime = DateTime.Now;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("arrivedAt", out var prop) && prop.TryGetDateTime(out var dt))
                    {
                        arrivalTime = dt.ToLocalTime();
                    }
                }
                catch { }

                var idx = _deliveryPoints.FindIndex(x => x.Id == id);
                if (idx >= 0)
                {
                    _deliveryPoints[idx].Status = "Прибыл";
                    _deliveryPoints[idx].Notes = $"Отметка прибытия: {arrivalTime:HH:mm:ss} (зафиксировано в системе)";
                    _deliveryPoints[idx].UpdatedAt = DateTime.UtcNow;

                    System.Diagnostics.Debug.WriteLine($"[MockBackendServer] PATCH /arrival для точки {id} выполнен: статус 'Прибыл'");
                    await SendJsonResponseAsync(response, HttpStatusCode.OK, _deliveryPoints[idx]);
                    return;
                }

                await SendJsonResponseAsync(response, HttpStatusCode.NotFound, new { error = "Точка доставки не найдена" });
                return;
            }

            // 5. ВАРИАНТ 7: КОМАНДА «ЗАФИКСИРОВАТЬ ОТКАЗ ОТ ГРУЗА» (PUT /api/deliveries/{id}/reject)
            if (path.StartsWith("/api/deliveries/") && path.EndsWith("/reject") && method == "PUT")
            {
                var id = path.Substring("/api/deliveries/".Length).Replace("/reject", "").Trim();
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();

                string reason = "Отказ грузополучателя";
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("reason", out var rProp))
                    {
                        reason = rProp.GetString() ?? reason;
                    }
                }
                catch { }

                var idx = _deliveryPoints.FindIndex(x => x.Id == id);
                if (idx >= 0)
                {
                    _deliveryPoints[idx].Status = "Отказ";
                    _deliveryPoints[idx].Notes = $"Акт отказа составлен. Причина: {reason}";
                    _deliveryPoints[idx].UpdatedAt = DateTime.UtcNow;

                    System.Diagnostics.Debug.WriteLine($"[MockBackendServer] PUT /reject для точки {id} выполнен: причина '{reason}'");
                    await SendJsonResponseAsync(response, HttpStatusCode.OK, _deliveryPoints[idx]);
                    return;
                }

                await SendJsonResponseAsync(response, HttpStatusCode.NotFound, new { error = "Точка доставки не найдена" });
                return;
            }

            // 6. ОБЩЕЕ ТРЕБОВАНИЕ: УДАЛЕНИЕ ТОЧКИ (DELETE /api/deliveries/{id})
            if (path.StartsWith("/api/deliveries/") && method == "DELETE")
            {
                var id = path.Substring("/api/deliveries/".Length).Trim();
                var idx = _deliveryPoints.FindIndex(x => x.Id == id);
                if (idx >= 0)
                {
                    var removed = _deliveryPoints[idx];
                    _deliveryPoints.RemoveAt(idx);
                    System.Diagnostics.Debug.WriteLine($"[MockBackendServer] DELETE /api/deliveries/{id} выполнен: наряд {removed.OrderNumber} удален");
                    await SendJsonResponseAsync(response, HttpStatusCode.OK, new { success = true, deletedId = id, orderNumber = removed.OrderNumber });
                    return;
                }

                await SendJsonResponseAsync(response, HttpStatusCode.NotFound, new { error = "Точка доставки не найдена" });
                return;
            }

            // Обычное обновление (PUT /api/deliveries/{id})
            if (path.StartsWith("/api/deliveries/") && method == "PUT")
            {
                var id = path.Substring("/api/deliveries/".Length).Trim();
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                var updated = JsonSerializer.Deserialize<DeliveryPoint>(body, _jsonOptions);

                if (updated != null)
                {
                    var idx = _deliveryPoints.FindIndex(x => x.Id == id || x.Id == updated.Id);
                    if (idx >= 0)
                    {
                        _deliveryPoints[idx].Status = updated.Status;
                        _deliveryPoints[idx].Notes = updated.Notes;
                        _deliveryPoints[idx].UpdatedAt = DateTime.UtcNow;
                        await SendJsonResponseAsync(response, HttpStatusCode.OK, _deliveryPoints[idx]);
                        return;
                    }
                }

                await SendJsonResponseAsync(response, HttpStatusCode.NotFound, new { error = "Точка доставки не найдена" });
                return;
            }

            // 7. ВАРИАНТ 7 + ТЕОРИЯ: ПРИЕМ ЗАШИФРОВАННОГО ФАЙЛА ОТЧЕТА О ТОПЛИВЕ
            // (POST /api/reports/fuel - MultipartFormDataContent со StreamContent и бинарными данными)
            if (path == "/api/reports/fuel" && method == "POST")
            {
                using var ms = new MemoryStream();
                await request.InputStream.CopyToAsync(ms);
                var totalBytes = ms.Length;

                var reportName = $"fuel_report_server_received_{DateTime.Now:yyyyMMdd_HHmmss}.enc";
                _uploadedFuelReports.Add(reportName);

                System.Diagnostics.Debug.WriteLine($"[MockBackendServer] POST /api/reports/fuel: Получен зашифрованный отчет ({totalBytes} байт)");

                var res = new FuelReportUploadResult
                {
                    Success = true,
                    FileName = reportName,
                    FileSizeBytes = totalBytes,
                    EncryptionAlgorithm = "AES-256-CBC",
                    ServerMessage = "Зашифрованный файл отчета о расходе топлива успешно проверен и сохранен на сервере транспортной компании."
                };

                await SendJsonResponseAsync(response, HttpStatusCode.OK, res);
                return;
            }

            // 8. ТЕЛЕМЕТРИЯ: POST /api/telemetry
            if (path == "/api/telemetry" && method == "POST")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                var telemetry = JsonSerializer.Deserialize<TelemetryData>(body, _jsonOptions);

                if (telemetry != null)
                {
                    _telemetryLogs.Add(telemetry);
                    if (_telemetryLogs.Count > 100) _telemetryLogs.RemoveAt(0);

                    await SendJsonResponseAsync(response, HttpStatusCode.OK, new
                    {
                        success = true,
                        receivedAt = DateTime.UtcNow,
                        speed = telemetry.SpeedKmh,
                        lat = telemetry.Latitude,
                        lon = telemetry.Longitude
                    });
                    return;
                }

                await SendJsonResponseAsync(response, HttpStatusCode.BadRequest, new { error = "Неверный формат телеметрии" });
                return;
            }

            // 9. HEALTH CHECK: GET /api/health
            if (path == "/api/health" && method == "GET")
            {
                await SendJsonResponseAsync(response, HttpStatusCode.OK, new { status = "healthy", uptime = DateTime.UtcNow });
                return;
            }

            // Маршрут не найден
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MockBackendServer] Ошибка обработки запроса: {ex.Message}");
            try
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Close();
            }
            catch { }
        }
    }

    private bool ValidateAuthHeader(HttpListenerRequest request, out string errorMessage)
    {
        var authHeader = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Отсутствует заголовок авторизации Bearer Token.";
            return false;
        }

        var tokenValue = authHeader.Substring("Bearer ".Length).Trim();

        // Симуляция истечения токена для демонстрации перехватчика DelegatingHandler
        if (tokenValue.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Срок действия JWT-токена истек (401 Unauthorized).";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private async Task SendJsonResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, object data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public void Dispose()
    {
        Stop();
    }
}
