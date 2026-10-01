namespace laba10.Models;

public class MatchPairDisplayItem
{
    public string LeftText { get; set; } = string.Empty;
    public string? SelectedRightText { get; set; }
    public bool IsMatched => !string.IsNullOrEmpty(SelectedRightText);
    public bool IsSelected { get; set; }
}
