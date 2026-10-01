using System.Text.Json.Serialization;

namespace laba10.Models;

/// <summary>
/// Вопрос на написание эссе (Essay / Writing).
/// Содержит самодостаточный живой подсчет слов и статус соблюдения лимита.
/// </summary>
public class EssayQuestion : QuestionBase
{
    private string _userEssayText = string.Empty;
    private int _minWordCount = 40;

    public override QuestionType Type => QuestionType.Essay;

    public string TopicPrompt { get; set; } = string.Empty;

    public int MinWordCount
    {
        get => _minWordCount;
        set
        {
            if (SetProperty(ref _minWordCount, value))
            {
                OnPropertyChanged(nameof(IsWordCountMet));
                OnPropertyChanged(nameof(WordCountStatusText));
            }
        }
    }

    public List<string> RequiredKeywords { get; set; } = new();

    public string UserEssayText
    {
        get => _userEssayText;
        set
        {
            if (SetProperty(ref _userEssayText, value))
            {
                OnPropertyChanged(nameof(CurrentWordCount));
                OnPropertyChanged(nameof(IsWordCountMet));
                OnPropertyChanged(nameof(WordCountStatusText));
            }
        }
    }

    [JsonIgnore]
    public int CurrentWordCount => GetCurrentWordCount();

    [JsonIgnore]
    public bool IsWordCountMet => CurrentWordCount >= MinWordCount;

    [JsonIgnore]
    public string WordCountStatusText => IsWordCountMet
        ? $"Слов: {CurrentWordCount} из {MinWordCount} (Требование выполнено ✓)"
        : $"Слов: {CurrentWordCount} из {MinWordCount} (Необходимо еще {MinWordCount - CurrentWordCount} слов)";

    public int GetCurrentWordCount()
    {
        if (string.IsNullOrWhiteSpace(UserEssayText))
            return 0;

        return UserEssayText
            .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    public override bool CheckIsAnswered()
    {
        return CurrentWordCount >= MinWordCount;
    }

    public override void ResetAnswer()
    {
        UserEssayText = string.Empty;
        EarnedPoints = 0;
        Feedback = string.Empty;
    }
}
