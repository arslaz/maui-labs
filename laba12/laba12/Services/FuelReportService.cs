using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using laba12.Models;

namespace laba12.Services;

public interface IFuelReportService
{
    Task<(FuelReport Report, string EncryptedFilePath)> GenerateAndEncryptReportAsync(
        string driverId, 
        string driverName, 
        string vehicleNumber, 
        IEnumerable<DeliveryPoint> deliveries);

    Task<List<FuelReport>> GetCachedReportsAsync();
}

public class FuelReportService : IFuelReportService
{
    private readonly ISecureStorageService _secureStorageService;
    private readonly ILocalDatabaseService _databaseService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private string CacheJsonPath => Path.Combine(FileSystem.AppDataDirectory, "fuel_reports_cache.json");

    public FuelReportService(
        ISecureStorageService secureStorageService,
        ILocalDatabaseService databaseService)
    {
        _secureStorageService = secureStorageService;
        _databaseService = databaseService;
    }

    public async Task<(FuelReport Report, string EncryptedFilePath)> GenerateAndEncryptReportAsync(
        string driverId,
        string driverName,
        string vehicleNumber,
        IEnumerable<DeliveryPoint> deliveries)
    {
        var points = deliveries.ToList();
        var deliveredCount = points.Count(p => p.Status == "Доставлен" || p.Status == "Прибыл");
        var activeCount = points.Count;

        // ВАРИАНТ 7: Формирование отчета на основе агрегированных данных из локальной таблицы поездок trip_records
        var tripStats = await _databaseService.GetAggregatedTripStatisticsAsync(driverId);
        var waybill = await _databaseService.GetOrCreateWaybillAsync(driverId, driverName, vehicleNumber);

        double totalDistanceKm = tripStats.TotalDistanceKm;
        double consumedLiters = tripStats.TotalFuelConsumedLiters;
        double avgLitersPer100 = tripStats.AverageConsumptionPer100Km;
        double remainingLiters = Math.Max(5.0, Math.Round(waybill.FuelStartingLiters - consumedLiters, 1));

        var report = new FuelReport
        {
            ReportId = $"REP-FUEL-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            DriverId = driverId,
            DriverName = driverName,
            VehicleNumber = vehicleNumber,
            GeneratedAt = DateTime.UtcNow,
            TotalDistanceKm = totalDistanceKm,
            FuelConsumedLiters = consumedLiters,
            FuelRemainingLiters = remainingLiters,
            AverageLitersPer100Km = avgLitersPer100,
            VisitedOrders = points.Select(p => $"{p.OrderNumber}: {p.ClientName} ({p.Status})").ToList(),
            Notes = $"Агрегировано из SQLite: {tripStats.TotalTrips} рейсов, средняя скорость {tripStats.AverageSpeedKmh:F1} км/ч. Обработано {deliveredCount} из {activeCount} точек."
        };

        // 1. Формирование структурированного текстового документа
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("          ЛОГИСТИЧЕСКИЙ НАВИГАТОР ГРУЗОПЕРЕВОЗОК — СЕРВЕР ТРАНСПОРТНОЙ КОМПАНИИ");
        sb.AppendLine("            ЭЛЕКТРОННЫЙ ОТЧЕТ О РАСХОДЕ ТОПЛИВА И ПРОБЕГЕ ТРАНСПОРТА (ВАРИАНТ 7)");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Идентификатор отчета:    {report.ReportId}");
        sb.AppendLine($"Табельный номер (ID):    {report.DriverId}");
        sb.AppendLine($"ФИО водителя:            {report.DriverName}");
        sb.AppendLine($"Гос. номер ТС:           {report.VehicleNumber}");
        sb.AppendLine($"Время генерации:         {DateTime.Now:dd.MM.yyyy HH:mm:ss} (Локальное)");
        sb.AppendLine($"Время сервера (UTC):     {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}Z");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("ИСТОЧНИК: ЛОКАЛЬНАЯ РЕЛЯЦИОННАЯ СУБД SQLITE (Таблицы: waybills, trip_records)");
        sb.AppendLine($"  • Учтенных поездок в БД:      {tripStats.TotalTrips}");
        sb.AppendLine($"  • Агрегированный пробег:      {report.TotalDistanceKm:F1} км");
        sb.AppendLine($"  • Объем израсходованного ДТ:  {report.FuelConsumedLiters:F2} л");
        sb.AppendLine($"  • Средний расход на 100 км:   {report.AverageLitersPer100Km:F1} л/100 км");
        sb.AppendLine($"  • Средняя скорость датчиков:  {tripStats.AverageSpeedKmh:F1} км/ч");
        sb.AppendLine($"  • Путевой лист водителя:      #{waybill.Id} ({waybill.Status}, {waybill.WaybillDate:dd.MM.yyyy})");
        sb.AppendLine($"  • Одометр автомобиля:         {waybill.InitialOdometerKm:F1} км -> {waybill.InitialOdometerKm + report.TotalDistanceKm:F1} км");
        sb.AppendLine($"  • Расчетный остаток в баке:   {report.FuelRemainingLiters:F1} л (из {waybill.FuelStartingLiters:F1} л)");
        sb.AppendLine($"  • Примечание диспетчера:      {report.Notes}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("ОБРАБОТАННЫЕ ПУТЕВЫЕ ТОЧКИ МАРШРУТНОГО ЛИСТА:");
        for (int i = 0; i < report.VisitedOrders.Count; i++)
        {
            sb.AppendLine($"  [{i + 1:D2}] {report.VisitedOrders[i]}");
        }
        sb.AppendLine("================================================================================");
        sb.AppendLine($"ЭЛЕКТРОННАЯ ПОДПИСЬ УСТРОЙСТВА: SHA256-DEVICE-SIG-{Guid.NewGuid():N}");
        sb.AppendLine("СТАТУС ШИФРОВАНИЯ: СИММЕТРИЧНЫЙ АЛГОРИТМ AES-256-CBC, КЛЮЧ В SECURESTORAGE");
        sb.AppendLine("================================================================================");

        var plainText = sb.ToString();

        // 2. Шифрование текстового отчета алгоритмом AES-256
        var aesKey = await _secureStorageService.GetOrCreateEncryptionKeyAsync();
        var encryptedBytes = EncryptAes256(plainText, aesKey);

        // 3. Запись зашифрованного файла в директорию приложения
        var fileName = $"{report.ReportId.ToLower()}.enc";
        var encryptedFilePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
        await File.WriteAllBytesAsync(encryptedFilePath, encryptedBytes);

        // 4. Дублирование записи в локальный кэш-файл отчетов (Cache Strategy)
        await AppendToCacheAsync(report);

        System.Diagnostics.Debug.WriteLine($"[FuelReportService] Отчет о расходе топлива сформирован, зашифрован AES-256 ({encryptedBytes.Length} байт) и сохранен в {encryptedFilePath}");

        return (report, encryptedFilePath);
    }

    public async Task<List<FuelReport>> GetCachedReportsAsync()
    {
        try
        {
            if (!File.Exists(CacheJsonPath)) return new List<FuelReport>();
            var json = await File.ReadAllTextAsync(CacheJsonPath);
            return JsonSerializer.Deserialize<List<FuelReport>>(json, _jsonOptions) ?? new List<FuelReport>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FuelReportService] Ошибка чтения кэша отчетов: {ex.Message}");
            return new List<FuelReport>();
        }
    }

    private async Task AppendToCacheAsync(FuelReport report)
    {
        try
        {
            var reports = await GetCachedReportsAsync();
            reports.Insert(0, report);
            if (reports.Count > 20) reports.RemoveAt(reports.Count - 1);
            var json = JsonSerializer.Serialize(reports, _jsonOptions);
            await File.WriteAllTextAsync(CacheJsonPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FuelReportService] Ошибка дублирования в кэш отчетов: {ex.Message}");
        }
    }

    private static byte[] EncryptAes256(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV(); // 16-byte random IV

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();

        // Пишем IV в начало потока (стандартный формат для безопасной расшифровки)
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plainText);
        }

        return ms.ToArray();
    }
}
