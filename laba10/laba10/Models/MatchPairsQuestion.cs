using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace laba10.Models;

/// <summary>
/// Вопрос на установление соответствий (Match Pairs).
/// Содержит самодостаточные команды связывания пар и списки для отображения.
/// </summary>
public class MatchPairsQuestion : QuestionBase
{
    private string? _selectedLeftItem;
    private ObservableCollection<MatchPairDisplayItem> _matchPairDisplayItems = new();
    private ObservableCollection<string> _availableRightItems = new();

    public override QuestionType Type => QuestionType.MatchPairs;

    public List<string> LeftItems { get; set; } = new();
    public List<string> RightItems { get; set; } = new();

    public Dictionary<string, string> CorrectMatches { get; set; } = new();
    public Dictionary<string, string> UserMatches { get; set; } = new();

    public double PenaltyPerMistake { get; set; } = 5.0;

    [JsonIgnore]
    public string? SelectedLeftItem
    {
        get => _selectedLeftItem;
        set
        {
            if (SetProperty(ref _selectedLeftItem, value))
            {
                OnPropertyChanged(nameof(SelectedLeftStatusText));
                RefreshDisplayItems();
            }
        }
    }

    [JsonIgnore]
    public string SelectedLeftStatusText => string.IsNullOrEmpty(SelectedLeftItem)
        ? "1. Выберите термин слева для связывания"
        : $"Выбран: «{SelectedLeftItem}» → теперь нажмите значение справа";

    [JsonIgnore]
    public ObservableCollection<MatchPairDisplayItem> MatchPairDisplayItems
    {
        get => _matchPairDisplayItems;
        set => SetProperty(ref _matchPairDisplayItems, value);
    }

    [JsonIgnore]
    public ObservableCollection<string> AvailableRightItems
    {
        get => _availableRightItems;
        set => SetProperty(ref _availableRightItems, value);
    }

    [JsonIgnore]
    public ICommand SelectLeftCommand { get; }
    [JsonIgnore]
    public ICommand SelectRightCommand { get; }
    [JsonIgnore]
    public ICommand ClearPairCommand { get; }

    public MatchPairsQuestion()
    {
        SelectLeftCommand = new Command<string>(item =>
        {
            if (!string.IsNullOrEmpty(item))
            {
                SelectedLeftItem = (SelectedLeftItem == item) ? null : item;
            }
        });

        SelectRightCommand = new Command<string>(rightItem =>
        {
            if (!string.IsNullOrEmpty(rightItem) && !string.IsNullOrEmpty(SelectedLeftItem))
            {
                UserMatches[SelectedLeftItem] = rightItem;
                SelectedLeftItem = null;
                RefreshDisplayItems();
            }
        });

        ClearPairCommand = new Command<string>(leftItem =>
        {
            if (!string.IsNullOrEmpty(leftItem) && UserMatches.Remove(leftItem))
            {
                RefreshDisplayItems();
            }
        });
    }

    public void RefreshDisplayItems()
    {
        var list = new ObservableCollection<MatchPairDisplayItem>();
        foreach (var left in LeftItems)
        {
            string? right = UserMatches.TryGetValue(left, out var val) ? val : null;
            list.Add(new MatchPairDisplayItem
            {
                LeftText = left,
                SelectedRightText = right,
                IsSelected = (SelectedLeftItem == left)
            });
        }
        MatchPairDisplayItems = list;

        var usedRight = UserMatches.Values.ToHashSet();
        var available = RightItems.Where(r => !usedRight.Contains(r)).ToList();
        AvailableRightItems = new ObservableCollection<string>(available);
    }

    public override bool CheckIsAnswered()
    {
        return UserMatches.Count == LeftItems.Count && LeftItems.Count > 0;
    }

    public override void ResetAnswer()
    {
        UserMatches.Clear();
        SelectedLeftItem = null;
        RefreshDisplayItems();
        EarnedPoints = 0;
        Feedback = string.Empty;
    }
}
