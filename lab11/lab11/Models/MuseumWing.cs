using lab11.Services;
using lab11.ViewModels;

namespace lab11.Models;

public class MuseumWing : BaseViewModel
{
    private bool _isSelected;

    public string Id { get; set; } = string.Empty;
    public string TitleKey { get; set; } = string.Empty;
    public string DescKey { get; set; } = string.Empty;
    public string FloorKey { get; set; } = string.Empty;
    public int TotalHalls { get; set; }
    public int AccessibleHallsCount { get; set; }
    public string MapColor { get; set; } = "#F59E0B";
    public string VisualRooms { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string LocalizedTitle => LocalizationManager.Instance.GetString(TitleKey);
    public string LocalizedDescription => LocalizationManager.Instance.GetString(DescKey);
    public string LocalizedFloor => LocalizationManager.Instance.GetString(FloorKey);

    public string FormattedHallsCount =>
        PluralizationService.Instance.GetPluralizedString("Plural_Halls", AccessibleHallsCount, LocalizationManager.Instance.CurrentCulture);

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(LocalizedTitle));
        OnPropertyChanged(nameof(LocalizedDescription));
        OnPropertyChanged(nameof(LocalizedFloor));
        OnPropertyChanged(nameof(FormattedHallsCount));
    }
}
