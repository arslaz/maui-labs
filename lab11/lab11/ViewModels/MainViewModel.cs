using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using lab11.Models;
using lab11.Services;

namespace lab11.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly LocalizationManager _locManager = LocalizationManager.Instance;
    private readonly PluralizationService _pluralService = PluralizationService.Instance;
    private readonly RegionalFormatService _regionalService = RegionalFormatService.Instance;
    private readonly SecurityService _securityService = SecurityService.Instance;

    private IDispatcherTimer? _audioTimer;
    private int _selectedTabIndex; // 0 = Exhibits, 1 = Wings, 2 = Settings
    private ExhibitItem? _currentExhibit;
    private MuseumWing? _selectedWing;

    // Audio Player State
    private bool _isPlaying;
    private int _currentPlaybackSeconds;
    private int _tourRemainingMinutes = 14;
    private double _audioVolume = 0.85;

    // Security & VIP
    private bool _isVipUnlocked;
    private string _activationKeyInput = string.Empty;
    private string _securityFeedbackMessage = string.Empty;
    private bool _isSecurityFeedbackSuccess;

    // Navigation Distance Mode
    private DistanceUnitMode _currentDistanceMode = DistanceUnitMode.Auto;

    public ObservableCollection<ExhibitItem> Exhibits { get; } = new();
    public ObservableCollection<MuseumWing> Wings { get; } = new();
    public ObservableCollection<LanguageItem> AvailableLanguages { get; } = new();

    // Commands
    public ICommand ChangeLanguageCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand SelectExhibitCommand { get; }
    public ICommand SelectWingCommand { get; }
    public ICommand TogglePlayCommand { get; }
    public ICommand StopAudioCommand { get; }
    public ICommand SeekRelativeCommand { get; }
    public ICommand ChangeDistanceModeCommand { get; }
    public ICommand SimulateWalkCloserCommand { get; }
    public ICommand ResetDistanceCommand { get; }
    public ICommand ActivateKeyCommand { get; }
    public ICommand RevokeKeyCommand { get; }
    public ICommand AdjustTourMinutesCommand { get; }

    public MainViewModel()
    {
        // Load initial volume from Preferences
        _audioVolume = Preferences.Default.Get("AppAudio_Volume", 0.85);
        _currentDistanceMode = _regionalService.CurrentMode;

        // Initialize Commands
        ChangeLanguageCommand = new Command<string>(OnChangeLanguage);
        SelectTabCommand = new Command<object>(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out int tab))
            {
                OnSelectTab(tab);
            }
        });
        SelectExhibitCommand = new Command<ExhibitItem>(OnSelectExhibit);
        SelectWingCommand = new Command<MuseumWing>(OnSelectWing);
        TogglePlayCommand = new Command(OnTogglePlay);
        StopAudioCommand = new Command(OnStopAudio);
        SeekRelativeCommand = new Command<object>(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out int sec))
            {
                OnSeekRelative(sec);
            }
        });
        ChangeDistanceModeCommand = new Command<object>(param =>
        {
            if (param is DistanceUnitMode mode)
                OnChangeDistanceMode(mode);
            else if (param != null && Enum.TryParse<DistanceUnitMode>(param.ToString(), out var parsedMode))
                OnChangeDistanceMode(parsedMode);
        });
        SimulateWalkCloserCommand = new Command(OnSimulateWalkCloser);
        ResetDistanceCommand = new Command(OnResetDistance);
        ActivateKeyCommand = new Command(async () => await OnActivateKeyAsync());
        RevokeKeyCommand = new Command(async () => await OnRevokeKeyAsync());
        AdjustTourMinutesCommand = new Command<object>(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out int delta))
            {
                OnAdjustTourMinutes(delta);
            }
        });

        // Populate Languages
        AvailableLanguages.Add(new LanguageItem("ru", "Русский", "🇷🇺", false));
        AvailableLanguages.Add(new LanguageItem("en", "English", "🇬🇧", false));
        AvailableLanguages.Add(new LanguageItem("ar", "العربية", "🇸🇦", true));

        // Populate Exhibits
        InitializeExhibits();

        // Populate Wings
        InitializeWings();

        // Select defaults
        _currentExhibit = Exhibits.FirstOrDefault();
        if (_currentExhibit != null)
        {
            _currentExhibit.IsCurrentlyPlaying = false;
        }

        _selectedWing = Wings.FirstOrDefault();
        if (_selectedWing != null)
        {
            _selectedWing.IsSelected = true;
        }

        // Listen for culture changes
        _locManager.CultureChanged += OnCultureChanged;

        // Setup audio simulation timer
        SetupAudioTimer();

        // Check SecureStorage for VIP key
        _ = CheckInitialSecurityAsync();
    }

    private void SetupAudioTimer()
    {
        if (Application.Current?.Dispatcher != null)
        {
            _audioTimer = Application.Current.Dispatcher.CreateTimer();
            _audioTimer.Interval = TimeSpan.FromSeconds(1);
            _audioTimer.Tick += OnAudioTimerTick;
        }
    }

    private void OnAudioTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || CurrentExhibit == null)
            return;

        if (CurrentPlaybackSeconds < CurrentExhibit.TotalDurationSeconds)
        {
            CurrentPlaybackSeconds++;
            if (CurrentPlaybackSeconds % 60 == 0 && TourRemainingMinutes > 1)
            {
                TourRemainingMinutes--;
            }
        }
        else
        {
            // Track completed
            IsPlaying = false;
            CurrentPlaybackSeconds = 0;
            if (TourRemainingMinutes > 1)
            {
                TourRemainingMinutes--;
            }
        }
    }

    private async Task CheckInitialSecurityAsync()
    {
        IsVipUnlocked = await _securityService.IsPaidTourUnlockedAsync();
        RefreshAllLocalizations();
    }

    #region Properties

    public FlowDirection CurrentFlowDirection => _locManager.CurrentFlowDirection;

    public bool IsRtl => _locManager.IsRtl;

    public string CurrentLanguageCode => _locManager.CurrentLanguageCode;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public ExhibitItem? CurrentExhibit
    {
        get => _currentExhibit;
        set
        {
            if (SetProperty(ref _currentExhibit, value))
            {
                CurrentPlaybackSeconds = 0;
                OnPropertyChanged(nameof(CurrentPlaybackFormatted));
                OnPropertyChanged(nameof(PlaybackProgress));
            }
        }
    }

    public MuseumWing? SelectedWing
    {
        get => _selectedWing;
        set
        {
            if (_selectedWing != null) _selectedWing.IsSelected = false;
            if (SetProperty(ref _selectedWing, value))
            {
                if (_selectedWing != null) _selectedWing.IsSelected = true;
                OnPropertyChanged(nameof(SelectedWingHallsFormatted));
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                if (CurrentExhibit != null)
                {
                    CurrentExhibit.IsCurrentlyPlaying = value;
                }

                if (value)
                    _audioTimer?.Start();
                else
                    _audioTimer?.Stop();

                OnPropertyChanged(nameof(PlayPauseButtonText));
                OnPropertyChanged(nameof(PlayPauseButtonGlyph));
            }
        }
    }

    public int CurrentPlaybackSeconds
    {
        get => _currentPlaybackSeconds;
        set
        {
            if (SetProperty(ref _currentPlaybackSeconds, value))
            {
                OnPropertyChanged(nameof(CurrentPlaybackFormatted));
                OnPropertyChanged(nameof(PlaybackProgress));
            }
        }
    }

    public double PlaybackProgress
    {
        get
        {
            if (CurrentExhibit == null || CurrentExhibit.TotalDurationSeconds == 0)
                return 0;
            return (double)CurrentPlaybackSeconds / CurrentExhibit.TotalDurationSeconds;
        }
        set
        {
            if (CurrentExhibit != null && CurrentExhibit.TotalDurationSeconds > 0)
            {
                CurrentPlaybackSeconds = (int)(value * CurrentExhibit.TotalDurationSeconds);
            }
        }
    }

    public string CurrentPlaybackFormatted
    {
        get
        {
            int total = CurrentExhibit?.TotalDurationSeconds ?? 0;
            return $"{FormatTime(CurrentPlaybackSeconds)} / {FormatTime(total)}";
        }
    }

    private static string FormatTime(int seconds)
    {
        int m = seconds / 60;
        int s = seconds % 60;
        return $"{m:00}:{s:00}";
    }

    public int TourRemainingMinutes
    {
        get => _tourRemainingMinutes;
        set
        {
            if (SetProperty(ref _tourRemainingMinutes, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(RemainingMinutesFormatted));
            }
        }
    }

    public double AudioVolume
    {
        get => _audioVolume;
        set
        {
            if (SetProperty(ref _audioVolume, value))
            {
                Preferences.Default.Set("AppAudio_Volume", value);
                OnPropertyChanged(nameof(SavedVolumeDisplay));
            }
        }
    }

    public bool IsVipUnlocked
    {
        get => _isVipUnlocked;
        set
        {
            if (SetProperty(ref _isVipUnlocked, value))
            {
                OnPropertyChanged(nameof(VipStatusBadgeText));
                OnPropertyChanged(nameof(VipStatusColor));
                foreach (var ex in Exhibits)
                {
                    ex.RefreshLocalization(value);
                }
            }
        }
    }

    public string ActivationKeyInput
    {
        get => _activationKeyInput;
        set => SetProperty(ref _activationKeyInput, value);
    }

    public string SecurityFeedbackMessage
    {
        get => _securityFeedbackMessage;
        set => SetProperty(ref _securityFeedbackMessage, value);
    }

    public bool IsSecurityFeedbackSuccess
    {
        get => _isSecurityFeedbackSuccess;
        set
        {
            if (SetProperty(ref _isSecurityFeedbackSuccess, value))
            {
                OnPropertyChanged(nameof(SecurityFeedbackColor));
            }
        }
    }

    public string SecurityFeedbackColor => IsSecurityFeedbackSuccess ? "#10B981" : "#EF4444";

    public DistanceUnitMode CurrentDistanceMode
    {
        get => _currentDistanceMode;
        set
        {
            if (SetProperty(ref _currentDistanceMode, value))
            {
                _regionalService.CurrentMode = value;
                RefreshDistances();
            }
        }
    }

    // Dynamic Pluralized Properties
    public string RemainingMinutesFormatted =>
        _pluralService.GetPluralizedString("Plural_Minutes", TourRemainingMinutes, _locManager.CurrentCulture);

    public string SelectedWingHallsFormatted =>
        SelectedWing != null
            ? _pluralService.GetPluralizedString("Plural_Halls", SelectedWing.AccessibleHallsCount, _locManager.CurrentCulture)
            : string.Empty;

    // Regional Properties
    public string CurrentMuseumTimeFormatted =>
        _regionalService.FormatDateTime(DateTime.Now, _locManager.CurrentCulture);

    public string StandardTicketPriceFormatted =>
        _regionalService.FormatCurrency(12.50m, _locManager.CurrentCulture);

    public string VipTicketPriceFormatted =>
        _regionalService.FormatCurrency(25.00m, _locManager.CurrentCulture);

    public string VisitorsTodayFormatted =>
        _regionalService.FormatNumber(3842, _locManager.CurrentCulture);

    public string CurrentLocaleInfo =>
        $"{_locManager.CurrentCulture.DisplayName} ({_locManager.CurrentCulture.Name})";

    public string SavedLanguageDisplay =>
        Preferences.Default.Get("AppLanguage_Key", _locManager.CurrentCulture.TwoLetterISOLanguageName).ToUpperInvariant();

    public string SavedVolumeDisplay =>
        $"{Math.Round(AudioVolume * 100)}%";

    public string VipStatusBadgeText =>
        IsVipUnlocked ? _locManager.GetString("Security_Status_Active") : _locManager.GetString("Security_Status_Locked");

    public string VipStatusColor => IsVipUnlocked ? "#10B981" : "#EF4444";

    public string PlayPauseButtonText =>
        IsPlaying ? _locManager.GetString("Audio_Pause") : _locManager.GetString("Audio_Play");

    public string PlayPauseButtonGlyph => IsPlaying ? "⏸" : "▶";

    public string RtlStatusText =>
        IsRtl ? "RTL (Right-to-Left / من اليمين إلى اليسار)" : "LTR (Left-to-Right / Слева направо)";

    #endregion

    #region Command Handlers

    private void OnChangeLanguage(string langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode))
            return;

        _locManager.SetLanguageByCode(langCode);
    }

    private void OnSelectTab(int index)
    {
        SelectedTabIndex = index;
    }

    private void OnSelectExhibit(ExhibitItem exhibit)
    {
        if (exhibit == null) return;

        if (exhibit.IsLocked)
        {
            SecurityFeedbackMessage = _locManager.GetString("PaidRoute_LockedDesc");
            IsSecurityFeedbackSuccess = false;
            SelectedTabIndex = 2; // Jump to settings & security to unlock
            return;
        }

        CurrentExhibit = exhibit;
        IsPlaying = true;
    }

    private void OnSelectWing(MuseumWing wing)
    {
        if (wing == null) return;
        SelectedWing = wing;
    }

    private void OnTogglePlay()
    {
        if (CurrentExhibit != null && CurrentExhibit.IsLocked)
        {
            SecurityFeedbackMessage = _locManager.GetString("PaidRoute_LockedDesc");
            IsSecurityFeedbackSuccess = false;
            SelectedTabIndex = 2;
            return;
        }

        IsPlaying = !IsPlaying;
    }

    private void OnStopAudio()
    {
        IsPlaying = false;
        CurrentPlaybackSeconds = 0;
    }

    private void OnSeekRelative(int seconds)
    {
        if (CurrentExhibit == null) return;

        int newSec = Math.Clamp(CurrentPlaybackSeconds + seconds, 0, CurrentExhibit.TotalDurationSeconds);
        CurrentPlaybackSeconds = newSec;
    }

    private void OnChangeDistanceMode(DistanceUnitMode mode)
    {
        CurrentDistanceMode = mode;
    }

    private void OnSimulateWalkCloser()
    {
        foreach (var exhibit in Exhibits)
        {
            exhibit.CurrentDistanceMeters = Math.Max(2, exhibit.CurrentDistanceMeters - 15);
        }
    }

    private void OnResetDistance()
    {
        foreach (var exhibit in Exhibits)
        {
            exhibit.CurrentDistanceMeters = exhibit.BaseDistanceMeters;
        }
    }

    private async Task OnActivateKeyAsync()
    {
        var result = await _securityService.ActivateTourKeyAsync(ActivationKeyInput);
        IsSecurityFeedbackSuccess = result.Success;
        SecurityFeedbackMessage = _locManager.GetString(result.MessageKey);

        if (result.Success)
        {
            IsVipUnlocked = true;
            ActivationKeyInput = string.Empty;
        }
    }

    private async Task OnRevokeKeyAsync()
    {
        await _securityService.RevokeTourKeyAsync();
        IsVipUnlocked = false;
        SecurityFeedbackMessage = _locManager.GetString("Security_Status_Locked");
        IsSecurityFeedbackSuccess = false;
    }

    private void OnAdjustTourMinutes(int delta)
    {
        TourRemainingMinutes = Math.Clamp(TourRemainingMinutes + delta, 0, 120);
    }

    #endregion

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        RefreshAllLocalizations();
    }

    private void RefreshAllLocalizations()
    {
        OnPropertyChanged(nameof(CurrentFlowDirection));
        OnPropertyChanged(nameof(IsRtl));
        OnPropertyChanged(nameof(CurrentLanguageCode));
        OnPropertyChanged(nameof(RemainingMinutesFormatted));
        OnPropertyChanged(nameof(SelectedWingHallsFormatted));
        OnPropertyChanged(nameof(CurrentMuseumTimeFormatted));
        OnPropertyChanged(nameof(StandardTicketPriceFormatted));
        OnPropertyChanged(nameof(VipTicketPriceFormatted));
        OnPropertyChanged(nameof(VisitorsTodayFormatted));
        OnPropertyChanged(nameof(CurrentLocaleInfo));
        OnPropertyChanged(nameof(SavedLanguageDisplay));
        OnPropertyChanged(nameof(SavedVolumeDisplay));
        OnPropertyChanged(nameof(VipStatusBadgeText));
        OnPropertyChanged(nameof(PlayPauseButtonText));
        OnPropertyChanged(nameof(RtlStatusText));

        foreach (var ex in Exhibits)
        {
            ex.RefreshLocalization(IsVipUnlocked);
        }

        foreach (var wing in Wings)
        {
            wing.RefreshLocalization();
        }
    }

    private void RefreshDistances()
    {
        foreach (var ex in Exhibits)
        {
            ex.RefreshDistance();
        }
    }

    private void InitializeExhibits()
    {
        Exhibits.Clear();

        Exhibits.Add(new ExhibitItem
        {
            Id = "mona_lisa",
            TitleKey = "Exhibit_MonaLisa_Title",
            DescKey = "Exhibit_MonaLisa_Desc",
            TrackKey = "Exhibit_MonaLisa_Track",
            NarratorKey = "Exhibit_MonaLisa_Narrator",
            HallKey = "Exhibit_MonaLisa_Hall",
            BaseDistanceMeters = 45,
            CurrentDistanceMeters = 45,
            TotalDurationSeconds = 180,
            IsPaidRoute = false,
            IconGlyph = "🎨",
            ImageSource = "mona_lisa.jpg",
            AccentColor = "#3B82F6",
            WingId = "wing_b"
        });

        Exhibits.Add(new ExhibitItem
        {
            Id = "tutankhamun",
            TitleKey = "Exhibit_Tutankhamun_Title",
            DescKey = "Exhibit_Tutankhamun_Desc",
            TrackKey = "Exhibit_Tutankhamun_Track",
            NarratorKey = "Exhibit_Tutankhamun_Narrator",
            HallKey = "Exhibit_Tutankhamun_Hall",
            BaseDistanceMeters = 120,
            CurrentDistanceMeters = 120,
            TotalDurationSeconds = 240,
            IsPaidRoute = true, // VIP Route
            IconGlyph = "👑",
            ImageSource = "tutankhamun.jpg",
            AccentColor = "#F59E0B",
            WingId = "wing_a"
        });

        Exhibits.Add(new ExhibitItem
        {
            Id = "venus",
            TitleKey = "Exhibit_Venus_Title",
            DescKey = "Exhibit_Venus_Desc",
            TrackKey = "Exhibit_Venus_Track",
            NarratorKey = "Exhibit_Venus_Narrator",
            HallKey = "Exhibit_Venus_Hall",
            BaseDistanceMeters = 85,
            CurrentDistanceMeters = 85,
            TotalDurationSeconds = 150,
            IsPaidRoute = false,
            IconGlyph = "🏛️",
            ImageSource = "venus_milo.jpg",
            AccentColor = "#10B981",
            WingId = "wing_c"
        });

        Exhibits.Add(new ExhibitItem
        {
            Id = "winged_victory",
            TitleKey = "Exhibit_WingedVictory_Title",
            DescKey = "Exhibit_WingedVictory_Desc",
            TrackKey = "Exhibit_WingedVictory_Track",
            NarratorKey = "Exhibit_WingedVictory_Narrator",
            HallKey = "Exhibit_WingedVictory_Hall",
            BaseDistanceMeters = 210,
            CurrentDistanceMeters = 210,
            TotalDurationSeconds = 300,
            IsPaidRoute = true, // VIP Route
            IconGlyph = "🕊️",
            AccentColor = "#8B5CF6",
            WingId = "wing_c"
        });

        Exhibits.Add(new ExhibitItem
        {
            Id = "hammurabi",
            TitleKey = "Exhibit_Hammurabi_Title",
            DescKey = "Exhibit_Hammurabi_Desc",
            TrackKey = "Exhibit_Hammurabi_Track",
            NarratorKey = "Exhibit_Hammurabi_Narrator",
            HallKey = "Exhibit_Hammurabi_Hall",
            BaseDistanceMeters = 340,
            CurrentDistanceMeters = 340,
            TotalDurationSeconds = 210,
            IsPaidRoute = false,
            IconGlyph = "📜",
            AccentColor = "#EC4899",
            WingId = "wing_a"
        });
    }

    private void InitializeWings()
    {
        Wings.Clear();

        Wings.Add(new MuseumWing
        {
            Id = "wing_a",
            TitleKey = "Wing_East_Title",
            DescKey = "Wing_East_Desc",
            FloorKey = "Wing_East_Floor",
            TotalHalls = 12,
            AccessibleHallsCount = 5, // Tests Plural: 5 залов / 5 halls / 5 قاعات
            MapColor = "#F59E0B",
            VisualRooms = "Hall 201 (Mask) • Hall 202 (Mummies) • Hall 203 (Papyri) • Hall 305 (Hammurabi) • Hall 306 (Babylon)"
        });

        Wings.Add(new MuseumWing
        {
            Id = "wing_b",
            TitleKey = "Wing_Central_Title",
            DescKey = "Wing_Central_Desc",
            FloorKey = "Wing_Central_Floor",
            TotalHalls = 8,
            AccessibleHallsCount = 2, // Tests Plural: 2 зала / 2 halls / قاعتان
            MapColor = "#3B82F6",
            VisualRooms = "Hall 104 (Mona Lisa) • Hall 105 (Italian Frescoes)"
        });

        Wings.Add(new MuseumWing
        {
            Id = "wing_c",
            TitleKey = "Wing_West_Title",
            DescKey = "Wing_West_Desc",
            FloorKey = "Wing_West_Floor",
            TotalHalls = 6,
            AccessibleHallsCount = 1, // Tests Plural: 1 зал / 1 hall / قاعة واحدة
            MapColor = "#10B981",
            VisualRooms = "Hall 112 (Venus de Milo) • Hall 115 (Winged Victory - Under Restoration)"
        });
    }
}
