using laba10.Models;

namespace laba10.Selectors;

/// <summary>
/// DataTemplateSelector для динамического выбора XAML-шаблона вопроса
/// на основе его полиморфного типа (Listening, MatchPairs, Essay).
/// Исключает процедурный код в code-behind страницы.
/// </summary>
public class QuestionTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ListeningTemplate { get; set; }
    public DataTemplate? MatchPairsTemplate { get; set; }
    public DataTemplate? EssayTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is QuestionBase question)
        {
            return question.Type switch
            {
                QuestionType.Listening => ListeningTemplate ?? throw new InvalidOperationException("ListeningTemplate not set"),
                QuestionType.MatchPairs => MatchPairsTemplate ?? throw new InvalidOperationException("MatchPairsTemplate not set"),
                QuestionType.Essay => EssayTemplate ?? throw new InvalidOperationException("EssayTemplate not set"),
                _ => throw new ArgumentOutOfRangeException(nameof(question.Type), $"Неизвестный тип вопроса: {question.Type}")
            };
        }

        throw new ArgumentException("Item must be of type QuestionBase", nameof(item));
    }
}
