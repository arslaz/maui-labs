namespace lab11.Models;

public class LanguageItem
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public bool IsRtl { get; set; }

    public LanguageItem(string code, string displayName, string flag, bool isRtl = false)
    {
        Code = code;
        DisplayName = displayName;
        Flag = flag;
        IsRtl = isRtl;
    }
}
