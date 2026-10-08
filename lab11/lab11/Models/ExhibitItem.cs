using System.Globalization;
using lab11.Services;
using lab11.ViewModels;

namespace lab11.Models;

public class ExhibitItem : BaseViewModel
{
    private int _currentDistanceMeters;
    private bool _isCurrentlyPlaying;
    private bool _isLocked;

    public string Id { get; set; } = string.Empty;
    public string TitleKey { get; set; } = string.Empty;
    public string DescKey { get; set; } = string.Empty;
    public string TrackKey { get; set; } = string.Empty;
    public string NarratorKey { get; set; } = string.Empty;
    public string HallKey { get; set; } = string.Empty;
    public int BaseDistanceMeters { get; set; }
    public int TotalDurationSeconds { get; set; }
    public bool IsPaidRoute { get; set; }
    public string IconGlyph { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#3B82F6";
    public string WingId { get; set; } = string.Empty;
    public string ImageSource { get; set; } = string.Empty;
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageSource);

    public int CurrentDistanceMeters
    {
        get => _currentDistanceMeters;
        set
        {
            if (SetProperty(ref _currentDistanceMeters, Math.Max(2, value)))
            {
                OnPropertyChanged(nameof(FormattedDistance));
            }
        }
    }

    public bool IsCurrentlyPlaying
    {
        get => _isCurrentlyPlaying;
        set
        {
            if (SetProperty(ref _isCurrentlyPlaying, value))
            {
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusBackgroundColor));
            }
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetProperty(ref _isLocked, value))
            {
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusBackgroundColor));
            }
        }
    }

    public string StatusIcon => IsLocked ? "🔒" : (IsCurrentlyPlaying ? "⏸" : "▶");
    public string StatusBackgroundColor => IsLocked ? "#475569" : (IsCurrentlyPlaying ? "#F59E0B" : "#2563EB");

    public string LocalizedTitle => LocalizationManager.Instance.GetString(TitleKey);
    public string LocalizedDescription => LocalizationManager.Instance.GetString(DescKey);
    public string LocalizedTrack => LocalizationManager.Instance.GetString(TrackKey);
    public string LocalizedNarrator => LocalizationManager.Instance.GetString(NarratorKey);
    public string LocalizedHall => LocalizationManager.Instance.GetString(HallKey);

    public string FormattedDistance =>
        RegionalFormatService.Instance.FormatDistance(CurrentDistanceMeters, LocalizationManager.Instance.CurrentCulture);

    public void RefreshDistance()
    {
        OnPropertyChanged(nameof(FormattedDistance));
    }

    public void RefreshLocalization(bool isVipUnlocked)
    {
        IsLocked = IsPaidRoute && !isVipUnlocked;

        OnPropertyChanged(nameof(LocalizedTitle));
        OnPropertyChanged(nameof(LocalizedDescription));
        OnPropertyChanged(nameof(LocalizedTrack));
        OnPropertyChanged(nameof(LocalizedNarrator));
        OnPropertyChanged(nameof(LocalizedHall));
        OnPropertyChanged(nameof(FormattedDistance));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusBackgroundColor));
    }
}
