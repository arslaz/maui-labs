namespace laba10.Models;

/// <summary>
/// Модель экзаменационного теста, включающего полиморфный список вопросов.
/// </summary>
public class ExamTest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = "B2"; // A1, A2, B1, B2, C1, C2
    public int TimeLimitMinutes { get; set; } = 15;
    public string BannerImage { get; set; } = "ic_exam_hero.svg";

    public List<QuestionBase> Questions { get; set; } = new();

    public double? LastScore { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? LastCompletedAt { get; set; }

    public double GetMaxTotalPoints()
    {
        double sum = 0;
        foreach (var q in Questions)
        {
            sum += q.MaxPoints;
        }
        return sum > 0 ? sum : 100.0;
    }

    public ExamTest Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        var clone = System.Text.Json.JsonSerializer.Deserialize<ExamTest>(json)!;
        clone.Id = Guid.NewGuid().ToString("N");
        clone.Title = $"{Title} (Копия)";
        clone.IsCompleted = false;
        clone.LastScore = null;
        clone.LastCompletedAt = null;
        foreach (var q in clone.Questions)
        {
            q.ResetAnswer();
        }
        return clone;
    }
}
