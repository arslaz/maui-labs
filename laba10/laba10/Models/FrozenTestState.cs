namespace laba10.Models;

/// <summary>
/// Состояние замороженного теста для сохранения в изолированное хранилище AppDataDirectory.
/// Позволяет восстановить тест с того же вопроса и с оставшимся таймером.
/// </summary>
public class FrozenTestState
{
    public string TestId { get; set; } = string.Empty;
    public string TestTitle { get; set; } = string.Empty;
    public string Level { get; set; } = "B2";
    public int CurrentQuestionIndex { get; set; } = 0;
    public int RemainingSeconds { get; set; } = 0;
    public DateTime SavedAt { get; set; } = DateTime.Now;

    public List<QuestionBase> Questions { get; set; } = new();
}
