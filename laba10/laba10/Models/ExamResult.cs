namespace laba10.Models;

/// <summary>
/// Результат прохождения языкового экзамена.
/// Содержит детализированные баллы по каждой секции, статус апелляции
/// и защищенный хэш/подпись результата в SecureStorage.
/// </summary>
public class ExamResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TestId { get; set; } = string.Empty;
    public string TestTitle { get; set; } = string.Empty;
    public string Level { get; set; } = "B2";
    public DateTime Date { get; set; } = DateTime.Now;

    public double TotalScore { get; set; } // 0..100%
    public string CefrGrade { get; set; } = "B2"; // A1..C2

    // Метрики по секциям
    public double ListeningScore { get; set; }
    public double MatchPairsScore { get; set; }
    public double EssayScore { get; set; }
    public int MatchErrorsCount { get; set; }
    public int EssayWordCount { get; set; }

    // Апелляция
    public bool IsAppealed { get; set; }
    public string AppealStatus { get; set; } = "Не подана";
    public string DetailedReport { get; set; } = string.Empty;

    // Ссылка на зашифрованную цифровую подпись в SecureStorage
    public string SecureTokenKey { get; set; } = string.Empty;
}
