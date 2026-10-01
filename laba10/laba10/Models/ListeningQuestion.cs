using System.Text.Json.Serialization;
using System.Windows.Input;

namespace laba10.Models;

/// <summary>
/// Вопрос на аудирование (Listening comprehension).
/// Поддерживает воспроизведение по URL или локальному пути, симуляцию воспроизведения
/// со встроенным синтезом речи TextToSpeech и отображением скрипта диалога.
/// </summary>
public class ListeningQuestion : QuestionBase
{
    private IDispatcherTimer? _audioTimer;
    private Plugin.Maui.Audio.IAudioPlayer? _audioPlayer;

    private int? _selectedOptionIndex;
    private bool _isAudioPlaying;
    private double _audioProgress;
    private int _currentPlaybackSeconds = 0;
    private string _audioStatusText = "Нажмите «Воспроизвести» для прослушивания диалога";
    private bool _isTranscriptVisible = false;

    public override QuestionType Type => QuestionType.Listening;

    public string AudioTitle { get; set; } = "Hotel Reception Inquiry";
    public string AudioSource { get; set; } = "audio_ielts.mp3";
    public string SpeakerRole { get; set; } = "Receptionist & Hotel Guest";
    public int AudioDurationSeconds { get; set; } = 15;
    public string AudioTranscript { get; set; } = string.Empty;

    public List<string> Options { get; set; } = new();
    public int CorrectOptionIndex { get; set; } = 0;

    public int? SelectedOptionIndex
    {
        get => _selectedOptionIndex;
        set
        {
            if (SetProperty(ref _selectedOptionIndex, value))
            {
                OnPropertyChanged(nameof(IsOption0Selected));
                OnPropertyChanged(nameof(IsOption1Selected));
                OnPropertyChanged(nameof(IsOption2Selected));
                OnPropertyChanged(nameof(IsOption3Selected));
            }
        }
    }

    // Свойства для визуального выделения выбранного варианта
    [JsonIgnore]
    public bool IsOption0Selected => SelectedOptionIndex == 0;
    [JsonIgnore]
    public bool IsOption1Selected => SelectedOptionIndex == 1;
    [JsonIgnore]
    public bool IsOption2Selected => SelectedOptionIndex == 2;
    [JsonIgnore]
    public bool IsOption3Selected => SelectedOptionIndex == 3;

    // Свойства аудиоплеера
    [JsonIgnore]
    public bool IsAudioPlaying
    {
        get => _isAudioPlaying;
        set => SetProperty(ref _isAudioPlaying, value);
    }

    [JsonIgnore]
    public double AudioProgress
    {
        get => _audioProgress;
        set => SetProperty(ref _audioProgress, value);
    }

    [JsonIgnore]
    public int CurrentPlaybackSeconds
    {
        get => _currentPlaybackSeconds;
        set
        {
            if (SetProperty(ref _currentPlaybackSeconds, value))
            {
                OnPropertyChanged(nameof(FormattedPlaybackTime));
            }
        }
    }

    [JsonIgnore]
    public string FormattedPlaybackTime =>
        $"{CurrentPlaybackSeconds / 60:D2}:{CurrentPlaybackSeconds % 60:D2} / {AudioDurationSeconds / 60:D2}:{AudioDurationSeconds % 60:D2}";

    [JsonIgnore]
    public string AudioStatusText
    {
        get => _audioStatusText;
        set => SetProperty(ref _audioStatusText, value);
    }

    [JsonIgnore]
    public bool IsTranscriptVisible
    {
        get => _isTranscriptVisible;
        set => SetProperty(ref _isTranscriptVisible, value);
    }

    [JsonIgnore]
    public string SourceTypeBadge => AudioSource.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        ? "🌐 Удаленный URL"
        : "📁 Аудиофайл (MP3)";

    // Команды
    [JsonIgnore]
    public ICommand SelectOptionCommand { get; }
    [JsonIgnore]
    public ICommand PlayAudioCommand { get; }
    [JsonIgnore]
    public ICommand PauseAudioCommand { get; }
    [JsonIgnore]
    public ICommand ReplayAudioCommand { get; }
    [JsonIgnore]
    public ICommand ToggleTranscriptCommand { get; }

    public ListeningQuestion()
    {
        SelectOptionCommand = new Command<object>(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out int idx))
            {
                SelectedOptionIndex = idx;
            }
        });

        PlayAudioCommand = new Command(StartAudioPlayback);
        PauseAudioCommand = new Command(StopAudioPlayback);
        ReplayAudioCommand = new Command(ReplayAudio);
        ToggleTranscriptCommand = new Command(() => IsTranscriptVisible = !IsTranscriptVisible);
    }

    private async void StartAudioPlayback()
    {
        if (IsAudioPlaying) return;

        try
        {
            // Если плеер уже инициализирован и стоит на паузе - возобновляем
            if (_audioPlayer != null && !_audioPlayer.IsPlaying)
            {
                _audioPlayer.Play();
                IsAudioPlaying = true;
                AudioStatusText = $"Воспроизведение: {AudioTitle}";
                StartTimer();
                return;
            }

            AudioStatusText = "Загрузка реального аудиофайла...";

            Stream? audioStream = null;

            // 1. Попытка открыть как упакованный ресурс (Resources/Raw)
            string fileName = AudioSource;
            if (fileName.Contains('/')) fileName = Path.GetFileName(fileName);
            if (fileName.Contains('\\')) fileName = Path.GetFileName(fileName);

            try
            {
                audioStream = await FileSystem.OpenAppPackageFileAsync(fileName);
            }
            catch { }

            // 2. Попытка открыть как локальный файл (FileSystem.AppDataDirectory)
            if (audioStream == null && File.Exists(AudioSource))
            {
                try
                {
                    audioStream = File.OpenRead(AudioSource);
                }
                catch { }
            }

            // 3. Если это URL (http/https), скачиваем в кэш
            if (audioStream == null && AudioSource.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var cacheDir = Path.Combine(FileSystem.AppDataDirectory, "audio");
                    Directory.CreateDirectory(cacheDir);
                    var cachedPath = Path.Combine(cacheDir, fileName);
                    if (!File.Exists(cachedPath) || new FileInfo(cachedPath).Length == 0)
                    {
                        using var client = new HttpClient();
                        var data = await client.GetByteArrayAsync(AudioSource);
                        await File.WriteAllBytesAsync(cachedPath, data);
                    }
                    audioStream = File.OpenRead(cachedPath);
                }
                catch { }
            }

            // 4. Гарантированный fallback на встроенный аудиофайл
            if (audioStream == null)
            {
                try
                {
                    audioStream = await FileSystem.OpenAppPackageFileAsync("sample_listening.mp3");
                }
                catch { }
            }

            if (audioStream != null)
            {
                _audioPlayer?.Dispose();
                _audioPlayer = Plugin.Maui.Audio.AudioManager.Current.CreatePlayer(audioStream);

                if (_audioPlayer.Duration > 0)
                {
                    AudioDurationSeconds = (int)Math.Ceiling(_audioPlayer.Duration);
                }

                _audioPlayer.PlaybackEnded += (s, e) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StopAudioPlayback();
                        CurrentPlaybackSeconds = AudioDurationSeconds;
                        AudioProgress = 1.0;
                        AudioStatusText = "Прослушивание завершено. Выберите правильный вариант ответа.";
                    });
                };

                _audioPlayer.Play();
                IsAudioPlaying = true;
                AudioStatusText = $"Воспроизведение: {AudioTitle}";
                StartTimer();
            }
            else
            {
                AudioStatusText = "Аудиозапись временно недоступна";
            }
        }
        catch (Exception ex)
        {
            AudioStatusText = $"Ошибка аудиоплеера: {ex.Message}";
            IsAudioPlaying = false;
        }
    }

    private void StartTimer()
    {
        _audioTimer?.Stop();
        _audioTimer = Application.Current?.Dispatcher.CreateTimer();
        if (_audioTimer != null)
        {
            _audioTimer.Interval = TimeSpan.FromMilliseconds(500);
            _audioTimer.Tick += (s, e) =>
            {
                if (_audioPlayer != null && _audioPlayer.IsPlaying)
                {
                    CurrentPlaybackSeconds = (int)_audioPlayer.CurrentPosition;
                    if (_audioPlayer.Duration > 0)
                    {
                        AudioProgress = Math.Clamp(_audioPlayer.CurrentPosition / _audioPlayer.Duration, 0.0, 1.0);
                    }
                    AudioStatusText = $"Воспроизведение: {FormattedPlaybackTime}";
                }
            };
            _audioTimer.Start();
        }
    }

    private void StopAudioPlayback()
    {
        IsAudioPlaying = false;
        _audioTimer?.Stop();
        try
        {
            if (_audioPlayer != null && _audioPlayer.IsPlaying)
            {
                _audioPlayer.Pause();
            }
        }
        catch { }

        if (CurrentPlaybackSeconds > 0 && CurrentPlaybackSeconds < AudioDurationSeconds)
        {
            AudioStatusText = $"Пауза на {FormattedPlaybackTime}";
        }
    }

    private void ReplayAudio()
    {
        StopAudioPlayback();
        CurrentPlaybackSeconds = 0;
        AudioProgress = 0;
        try
        {
            _audioPlayer?.Seek(0);
        }
        catch { }
        StartAudioPlayback();
    }

    public override bool CheckIsAnswered()
    {
        return SelectedOptionIndex.HasValue;
    }

    public override void ResetAnswer()
    {
        StopAudioPlayback();
        SelectedOptionIndex = null;
        CurrentPlaybackSeconds = 0;
        AudioProgress = 0;
        AudioStatusText = "Нажмите «Воспроизвести» для прослушивания диалога";
        IsTranscriptVisible = false;
        EarnedPoints = 0;
        Feedback = string.Empty;
    }
}
