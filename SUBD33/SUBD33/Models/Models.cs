namespace SUBD33.Models;

public class Student
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int Course { get; set; }
    public string Photo { get; set; } = string.Empty;
    public ImageSource PhotoSource => Helpers.ImageHelper.GetImageSource(Photo);

    public string DisplayInfo => $"{FullName} ({GroupName}, {Course} курс)";
}

public class TriggerItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}

public class Incident
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int Course { get; set; }
    public string StudentPhoto { get; set; } = string.Empty;
    
    public DateTime IncidentDate { get; set; }
    public string DateDisplay => IncidentDate.ToString("dd.MM.yyyy HH:mm");

    public int TriggerId { get; set; }
    public string TriggerName { get; set; } = string.Empty;

    public int PanicLevel { get; set; }
    public string PanicBadge => $"{PanicLevel}/10 {GetPanicEmoji(PanicLevel)}";
    public Color PanicColor => PanicLevel switch
    {
        >= 9 => Color.FromArgb("#EF4444"), // Red
        >= 7 => Color.FromArgb("#F97316"), // Orange
        >= 4 => Color.FromArgb("#F59E0B"), // Amber
        _ => Color.FromArgb("#10B981")     // Green
    };

    public string CalmingMethod { get; set; } = string.Empty;
    public bool Helped { get; set; }
    public string HelpedDisplay => Helped ? "Да" : "Нет";
    public Color HelpedColor => Helped ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");

    private static string GetPanicEmoji(int level) => level switch
    {
        10 => "🔥",
        >= 8 => "😱",
        >= 6 => "😰",
        >= 4 => "😬",
        _ => "🙂"
    };
}

public class StudentCard
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public int Course { get; set; }
    public string Photo { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
    public double AveragePanicLevel { get; set; }
    public string MostFrequentTrigger { get; set; } = "Нет данных";

    public string PanicRatingText => $"{AveragePanicLevel:F1} / 10";
    public Color PanicRatingColor => AveragePanicLevel switch
    {
        >= 8.0 => Color.FromArgb("#EF4444"),
        >= 6.0 => Color.FromArgb("#F97316"),
        >= 4.0 => Color.FromArgb("#F59E0B"),
        _ => Color.FromArgb("#10B981")
    };
}

public class QueryResult
{
    public string Title { get; set; } = string.Empty;
    public string SqlQuery { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public int RowCount => Rows.Count;
}
