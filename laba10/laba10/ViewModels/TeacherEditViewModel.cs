using System.Windows.Input;
using laba10.Models;
using laba10.Services;

namespace laba10.ViewModels;

/// <summary>
/// ViewModel скрытого режима преподавателя для создания и редактирования тестов
/// со всеми тремя полиморфными типами вопросов (Listening, MatchPairs, Essay).
/// </summary>
public class TeacherEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ITestService _testService;

    private string? _editingTestId = null;
    private bool _isEditingExistingTest = false;
    private string _pageHeader = "РЕЖИМ ПРЕПОДАВАТЕЛЯ";
    private string _pageSubtitle = "Конструктор тестов и вопросов";
    private string _saveButtonText = "Сохранить тест";

    private string _testTitle = "Custom Mock Exam: Advanced Proficiency";
    private string _testDescription = "Пользовательский тест, составленный преподавателем в защищенном режиме.";
    private string _selectedLevel = "B2";
    private int _timeLimitMinutes = 15;

    // Вопрос 1: Listening
    private string _listeningTitle = "Аудирование: Академическая лекция";
    private string _listeningAudioUrl = "audio_ielts.mp3";
    private string _listeningSpeaker = "Ведущий профессор & Исследователь";
    private string _listeningOption1 = "Языковые модели помогают в освоении академической лексики";
    private string _listeningOption2 = "Изучение грамматики больше не требуется в университетах";
    private string _listeningOption3 = "Произношение невозможно улучшить в дистанционном формате";
    private string _listeningOption4 = "Традиционные словари полностью утратили методический смысл";

    // Вопрос 2: Match Pairs
    private string _matchTitle = "Лексические соответствия";
    private string _pair1Left = "Take with a grain of salt";
    private string _pair1Right = "Относиться скептически";
    private string _pair2Left = "Once in a blue moon";
    private string _pair2Right = "Крайне редко";
    private string _pair3Left = "See eye to eye";
    private string _pair3Right = "Полностью соглашаться";
    private string _pair4Left = "Burn the midnight oil";
    private string _pair4Right = "Усердно трудиться до поздней ночи";
    private double _penalty = 5.0;

    // Вопрос 3: Essay
    private string _essayTitle = "Академическое эссе";
    private string _essayPrompt = "Do modern digital tools improve foreign language retention compared to traditional classroom methods? Give reasons.";
    private int _essayMinWords = 35;

    public List<string> Levels { get; } = new() { "A1", "A2", "B1", "B2", "C1", "C2" };

    public bool IsEditingExistingTest
    {
        get => _isEditingExistingTest;
        set => SetProperty(ref _isEditingExistingTest, value);
    }

    public string PageHeader
    {
        get => _pageHeader;
        set => SetProperty(ref _pageHeader, value);
    }

    public string PageSubtitle
    {
        get => _pageSubtitle;
        set => SetProperty(ref _pageSubtitle, value);
    }

    public string SaveButtonText
    {
        get => _saveButtonText;
        set => SetProperty(ref _saveButtonText, value);
    }

    public string TestTitle
    {
        get => _testTitle;
        set => SetProperty(ref _testTitle, value);
    }

    public string TestDescription
    {
        get => _testDescription;
        set => SetProperty(ref _testDescription, value);
    }

    public string SelectedLevel
    {
        get => _selectedLevel;
        set => SetProperty(ref _selectedLevel, value);
    }

    public int TimeLimitMinutes
    {
        get => _timeLimitMinutes;
        set => SetProperty(ref _timeLimitMinutes, value);
    }

    public string ListeningTitle
    {
        get => _listeningTitle;
        set => SetProperty(ref _listeningTitle, value);
    }

    public string ListeningAudioUrl
    {
        get => _listeningAudioUrl;
        set => SetProperty(ref _listeningAudioUrl, value);
    }

    public string ListeningSpeaker
    {
        get => _listeningSpeaker;
        set => SetProperty(ref _listeningSpeaker, value);
    }

    public string ListeningOption1
    {
        get => _listeningOption1;
        set => SetProperty(ref _listeningOption1, value);
    }

    public string ListeningOption2
    {
        get => _listeningOption2;
        set => SetProperty(ref _listeningOption2, value);
    }

    public string ListeningOption3
    {
        get => _listeningOption3;
        set => SetProperty(ref _listeningOption3, value);
    }

    public string ListeningOption4
    {
        get => _listeningOption4;
        set => SetProperty(ref _listeningOption4, value);
    }

    // Pairs
    public string MatchTitle { get => _matchTitle; set => SetProperty(ref _matchTitle, value); }
    public string Pair1Left { get => _pair1Left; set => SetProperty(ref _pair1Left, value); }
    public string Pair1Right { get => _pair1Right; set => SetProperty(ref _pair1Right, value); }
    public string Pair2Left { get => _pair2Left; set => SetProperty(ref _pair2Left, value); }
    public string Pair2Right { get => _pair2Right; set => SetProperty(ref _pair2Right, value); }
    public string Pair3Left { get => _pair3Left; set => SetProperty(ref _pair3Left, value); }
    public string Pair3Right { get => _pair3Right; set => SetProperty(ref _pair3Right, value); }
    public string Pair4Left { get => _pair4Left; set => SetProperty(ref _pair4Left, value); }
    public string Pair4Right { get => _pair4Right; set => SetProperty(ref _pair4Right, value); }
    public double Penalty { get => _penalty; set => SetProperty(ref _penalty, value); }

    // Essay
    public string EssayTitle { get => _essayTitle; set => SetProperty(ref _essayTitle, value); }
    public string EssayPrompt { get => _essayPrompt; set => SetProperty(ref _essayPrompt, value); }
    public int EssayMinWords { get => _essayMinWords; set => SetProperty(ref _essayMinWords, value); }

    public ICommand SaveNewTestCommand { get; }
    public ICommand CancelCommand { get; }

    public TeacherEditViewModel(ITestService testService)
    {
        _testService = testService;
        Title = "Конструктор тестов преподавателя";

        SaveNewTestCommand = new Command(async () => await ExecuteSaveTestAsync());
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Test", out var testObj) && testObj is ExamTest test)
        {
            LoadTest(test);
        }
        else
        {
            ResetToNewTest();
        }
    }

    public void LoadTest(ExamTest test)
    {
        _editingTestId = test.Id;
        IsEditingExistingTest = true;
        PageHeader = "РЕДАКТИРОВАНИЕ ВОПРОСОВ (ТЕСТ)";
        PageSubtitle = $"Правка экзамена: «{test.Title}»";
        SaveButtonText = "Сохранить изменения";
        Title = $"Редактирование: {test.Title}";

        TestTitle = test.Title;
        TestDescription = test.Description;
        SelectedLevel = test.Level;
        TimeLimitMinutes = test.TimeLimitMinutes;

        // 1. Listening
        var lq = test.Questions.OfType<ListeningQuestion>().FirstOrDefault();
        if (lq != null)
        {
            ListeningTitle = lq.Title;
            ListeningAudioUrl = lq.AudioSource;
            ListeningSpeaker = lq.SpeakerRole;
            if (lq.Options.Count > 0) ListeningOption1 = lq.Options[0];
            if (lq.Options.Count > 1) ListeningOption2 = lq.Options[1];
            if (lq.Options.Count > 2) ListeningOption3 = lq.Options[2];
            if (lq.Options.Count > 3) ListeningOption4 = lq.Options[3];
        }

        // 2. Match Pairs
        var mq = test.Questions.OfType<MatchPairsQuestion>().FirstOrDefault();
        if (mq != null)
        {
            MatchTitle = mq.Title;
            Penalty = mq.PenaltyPerMistake;
            if (mq.LeftItems.Count > 0) Pair1Left = mq.LeftItems[0];
            if (mq.RightItems.Count > 0) Pair1Right = mq.RightItems[0];
            if (mq.LeftItems.Count > 1) Pair2Left = mq.LeftItems[1];
            if (mq.RightItems.Count > 1) Pair2Right = mq.RightItems[1];
            if (mq.LeftItems.Count > 2) Pair3Left = mq.LeftItems[2];
            if (mq.RightItems.Count > 2) Pair3Right = mq.RightItems[2];
            if (mq.LeftItems.Count > 3) Pair4Left = mq.LeftItems[3];
            if (mq.RightItems.Count > 3) Pair4Right = mq.RightItems[3];
        }

        // 3. Essay
        var eq = test.Questions.OfType<EssayQuestion>().FirstOrDefault();
        if (eq != null)
        {
            EssayTitle = eq.Title;
            EssayPrompt = eq.TopicPrompt;
            EssayMinWords = eq.MinWordCount;
        }
    }

    public void ResetToNewTest()
    {
        _editingTestId = null;
        IsEditingExistingTest = false;
        PageHeader = "РЕЖИМ ПРЕПОДАВАТЕЛЯ";
        PageSubtitle = "Создание нового теста и вопросов";
        SaveButtonText = "Создать новый тест";
        Title = "Конструктор тестов";
    }

    private async Task ExecuteSaveTestAsync()
    {
        if (string.IsNullOrWhiteSpace(TestTitle))
        {
            await DisplayAlertAsync("Внимание", "Укажите название теста.");
            return;
        }

        // 1. Listening
        var lq = new ListeningQuestion
        {
            Title = ListeningTitle,
            Instructions = "Прослушайте аудиозапись и ответьте на контрольный вопрос.",
            AudioTitle = ListeningTitle,
            AudioSource = string.IsNullOrWhiteSpace(ListeningAudioUrl) ? "audio_ielts.mp3" : ListeningAudioUrl.Trim(),
            SpeakerRole = ListeningSpeaker,
            AudioDurationSeconds = 15,
            Options = new List<string> { ListeningOption1, ListeningOption2, ListeningOption3, ListeningOption4 },
            CorrectOptionIndex = 0,
            MaxPoints = 30.0
        };

        // 2. Match Pairs
        var mq = new MatchPairsQuestion
        {
            Title = MatchTitle,
            Instructions = $"Сопоставьте пары. Внимание: штраф за каждую ошибку составляет {Penalty:F1} баллов!",
            MaxPoints = 35.0,
            PenaltyPerMistake = Penalty,
            LeftItems = new List<string> { Pair1Left, Pair2Left, Pair3Left, Pair4Left },
            RightItems = new List<string> { Pair1Right, Pair2Right, Pair3Right, Pair4Right },
            CorrectMatches = new Dictionary<string, string>
            {
                [Pair1Left] = Pair1Right,
                [Pair2Left] = Pair2Right,
                [Pair3Left] = Pair3Right,
                [Pair4Left] = Pair4Right,
            }
        };

        // 3. Essay
        var eq = new EssayQuestion
        {
            Title = EssayTitle,
            Instructions = $"Напишите эссе. Жесткое ограничение: не менее {EssayMinWords} слов!",
            TopicPrompt = EssayPrompt,
            MinWordCount = EssayMinWords,
            MaxPoints = 35.0,
            RequiredKeywords = new List<string> { "however", "furthermore", "methods", "conclusion" }
        };

        if (IsEditingExistingTest && !string.IsNullOrEmpty(_editingTestId))
        {
            // Обновляем существующий тест в каталоге
            var existing = await _testService.GetTestByIdAsync(_editingTestId);
            if (existing != null)
            {
                existing.Title = TestTitle.Trim();
                existing.Description = TestDescription.Trim();
                existing.Level = SelectedLevel;
                existing.TimeLimitMinutes = Math.Max(5, TimeLimitMinutes);
                existing.Questions.Clear();
                existing.Questions.Add(lq);
                existing.Questions.Add(mq);
                existing.Questions.Add(eq);

                await _testService.AddOrUpdateTestAsync(existing);
                await DisplayAlertAsync("Успешно", $"Вопросы теста «{existing.Title}» успешно отредактированы и сохранены!");
                await Shell.Current.GoToAsync("..");
                return;
            }
        }

        // Создаем новый тест
        var newTest = new ExamTest
        {
            Id = $"teacher_test_{DateTime.Now.Ticks}",
            Title = TestTitle.Trim(),
            Description = TestDescription.Trim(),
            Level = SelectedLevel,
            TimeLimitMinutes = Math.Max(5, TimeLimitMinutes),
            BannerImage = "ic_exam_hero.png"
        };

        newTest.Questions.Add(lq);
        newTest.Questions.Add(mq);
        newTest.Questions.Add(eq);

        await _testService.AddOrUpdateTestAsync(newTest);
        await DisplayAlertAsync("Успешно", $"Новый тест «{newTest.Title}» сохранен в изолированное хранилище!");
        await Shell.Current.GoToAsync("..");
    }
}
