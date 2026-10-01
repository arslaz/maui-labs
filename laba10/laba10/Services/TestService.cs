using System.Text.Json;
using laba10.Models;

namespace laba10.Services;

/// <summary>
/// Сервис управления тестами и вопросами в изолированном хранилище FileSystem.AppDataDirectory.
/// Реализует полиморфную сериализацию через System.Text.Json и поддержку состояния заморозки теста.
/// </summary>
public class TestService : ITestService
{
    private readonly string _catalogFilePath;
    private readonly string _frozenStateFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public TestService()
    {
        _catalogFilePath = Path.Combine(FileSystem.AppDataDirectory, "exams_catalog.json");
        _frozenStateFilePath = Path.Combine(FileSystem.AppDataDirectory, "frozen_exam_state.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<ExamTest>> GetTestsAsync()
    {
        try
        {
            if (File.Exists(_catalogFilePath))
            {
                var json = await File.ReadAllTextAsync(_catalogFilePath);
                var tests = JsonSerializer.Deserialize<List<ExamTest>>(json, _jsonOptions);
                if (tests != null && tests.Count > 0)
                {
                    // Гарантируем, что вопросы ссылаются на реальные MP3 файлы
                    foreach (var test in tests)
                    {
                        foreach (var q in test.Questions.OfType<ListeningQuestion>())
                        {
                            if (q.AudioSource.Contains("campus_accommodation") || q.AudioSource.Contains("ielts"))
                                q.AudioSource = "audio_ielts.mp3";
                            else if (q.AudioSource.Contains("corporate_meeting") || q.AudioSource.Contains("cambridge"))
                                q.AudioSource = "audio_cambridge.mp3";
                            else if (q.AudioSource.Contains("hotel_reception") || q.AudioSource.Contains("toefl"))
                                q.AudioSource = "audio_toefl.mp3";
                        }
                    }
                    return tests;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка чтения тестов: {ex.Message}");
        }

        // Если файла нет или он пуст, создаем богатый начальный каталог
        var defaultTests = CreateDefaultTests();
        await SaveTestsAsync(defaultTests);
        return defaultTests;
    }

    public async Task<List<ExamTest>> GetFilteredTestsAsync(string level)
    {
        var all = await GetTestsAsync();
        if (string.IsNullOrWhiteSpace(level) || level == "Все" || level == "All")
        {
            return all;
        }

        return all.Where(t => string.Equals(t.Level, level, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<ExamTest?> GetTestByIdAsync(string id)
    {
        var all = await GetTestsAsync();
        return all.FirstOrDefault(t => t.Id == id);
    }

    public async Task SaveTestsAsync(List<ExamTest> tests)
    {
        var json = JsonSerializer.Serialize(tests, _jsonOptions);
        await File.WriteAllTextAsync(_catalogFilePath, json);
    }

    public async Task<ExamTest> DuplicateTestAsync(string id)
    {
        var all = await GetTestsAsync();
        var test = all.FirstOrDefault(t => t.Id == id);
        if (test == null)
            throw new InvalidOperationException("Тест не найден.");

        var copy = test.Clone();
        all.Add(copy);
        await SaveTestsAsync(all);
        return copy;
    }

    public async Task<bool> DeleteTestAsync(string id)
    {
        var all = await GetTestsAsync();
        var item = all.FirstOrDefault(t => t.Id == id);
        if (item != null)
        {
            all.Remove(item);
            await SaveTestsAsync(all);
            return true;
        }
        return false;
    }

    public async Task<bool> ResetTestProgressAsync(string id)
    {
        var all = await GetTestsAsync();
        var item = all.FirstOrDefault(t => t.Id == id);
        if (item != null)
        {
            item.IsCompleted = false;
            item.LastScore = null;
            item.LastCompletedAt = null;
            foreach (var q in item.Questions)
            {
                q.ResetAnswer();
            }
            await SaveTestsAsync(all);
            return true;
        }
        return false;
    }

    public async Task<string> ExportTestToJsonAsync(string id)
    {
        var test = await GetTestByIdAsync(id);
        if (test == null) return string.Empty;

        var exportDir = Path.Combine(FileSystem.AppDataDirectory, "exports");
        Directory.CreateDirectory(exportDir);
        var exportPath = Path.Combine(exportDir, $"test_{test.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        var json = JsonSerializer.Serialize(test, _jsonOptions);
        await File.WriteAllTextAsync(exportPath, json);
        return exportPath;
    }

    public async Task AddOrUpdateTestAsync(ExamTest test)
    {
        var all = await GetTestsAsync();
        var existingIndex = all.FindIndex(t => t.Id == test.Id);
        if (existingIndex >= 0)
        {
            all[existingIndex] = test;
        }
        else
        {
            all.Add(test);
        }
        await SaveTestsAsync(all);
    }

    public async Task UpdateTestResultAsync(string testId, double score)
    {
        var all = await GetTestsAsync();
        var item = all.FirstOrDefault(t => t.Id == testId);
        if (item != null)
        {
            item.IsCompleted = true;
            item.LastScore = score;
            item.LastCompletedAt = DateTime.Now;
            await SaveTestsAsync(all);
        }
    }

    // Сохранение и восстановление замороженного теста
    public async Task SaveFrozenStateAsync(FrozenTestState state)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(_frozenStateFilePath, json);
    }

    public async Task<FrozenTestState?> GetFrozenStateAsync()
    {
        if (!File.Exists(_frozenStateFilePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_frozenStateFilePath);
            return JsonSerializer.Deserialize<FrozenTestState>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public Task ClearFrozenStateAsync()
    {
        if (File.Exists(_frozenStateFilePath))
        {
            File.Delete(_frozenStateFilePath);
        }
        return Task.CompletedTask;
    }

    private List<ExamTest> CreateDefaultTests()
    {
        var tests = new List<ExamTest>();

        // ТЕСТ 1: IELTS Academic Master (B2-C1)
        var ielts = new ExamTest
        {
            Id = "test_ielts_01",
            Title = "IELTS Academic: Advanced Mastery",
            Description = "Комплексный международный экзамен: академическое аудирование, идиоматические соответствия и написание эссе.",
            Level = "C1",
            TimeLimitMinutes = 15,
            BannerImage = "ic_exam_hero.png"
        };

        // 1. Вопрос на аудирование (реальный MP3 файл)
        var listening1 = new ListeningQuestion
        {
            Title = "Секция 1: Академическое аудирование",
            Instructions = "Прослушайте аудиозапись лекции и определите ключевое условие.",
            MaxPoints = 30.0,
            AudioTitle = "Cognitive Linguistics & Language Acquisition",
            AudioSource = "audio_ielts.mp3",
            SpeakerRole = "Research Professor & Student",
            AudioDurationSeconds = 12,
            AudioTranscript = "This is the listening section of the IELTS Academic examination. In this recording, two university researchers discuss cognitive aspects of second language acquisition and semantic memory.",
            Options = new List<string>
            {
                "Полная оплата залогового депозита до 15 октября",
                "Предоставление академической рекомендации от деканата",
                "Средний балл диплома бакалавра не ниже 3.8 GPA",
                "Прохождение обязательной волонтерской практики в библиотеке"
            },
            CorrectOptionIndex = 0
        };

        // 2. Вопрос на соответствия со штрафами
        var matchPairs1 = new MatchPairsQuestion
        {
            Title = "Секция 2: Установление соответствий (Академические идиомы)",
            Instructions = "Сопоставьте академические идиомы с их точными значениями. Внимание: за каждую неверную комбинацию налагается штраф 5 баллов!",
            MaxPoints = 35.0,
            PenaltyPerMistake = 5.0,
            LeftItems = new List<string>
            {
                "Break new ground",
                "Bite the bullet",
                "Cut corners",
                "Draw the line"
            },
            RightItems = new List<string>
            {
                "Совершить инновационное открытие",
                "Мужественно принять трудное неизбежное решение",
                "Делать что-либо небрежно ради экономии ресурсов",
                "Установить четкую непреодолимую границу"
            },
            CorrectMatches = new Dictionary<string, string>
            {
                ["Break new ground"] = "Совершить инновационное открытие",
                ["Bite the bullet"] = "Мужественно принять трудное неизбежное решение",
                ["Cut corners"] = "Делать что-либо небрежно ради экономии ресурсов",
                ["Draw the line"] = "Установить четкую непреодолимую границу"
            }
        };

        // 3. Вопрос на эссе с ограничением слов
        var essay1 = new EssayQuestion
        {
            Title = "Секция 3: Академическое эссе (Writing Task 2)",
            Instructions = "Напишите развернутый ответ на заданную тему. Действует жесткое ограничение: минимум 40 слов! Без достижения минимума ответ не оценивается.",
            MaxPoints = 35.0,
            MinWordCount = 40,
            TopicPrompt = "Should artificial intelligence systems be widely integrated into academic grading in universities? Give your opinion and provide relevant supporting arguments.",
            RequiredKeywords = new List<string> { "intelligence", "education", "furthermore", "opinion" }
        };

        ielts.Questions.Add(listening1);
        ielts.Questions.Add(matchPairs1);
        ielts.Questions.Add(essay1);
        tests.Add(ielts);

        // ТЕСТ 2: Cambridge B2 First (B2)
        var cambridge = new ExamTest
        {
            Id = "test_cambridge_b2",
            Title = "Cambridge English: First Certificate",
            Description = "Тестирование уверенного владения английским языком для работы, путешествий и международной коммуникации.",
            Level = "B2",
            TimeLimitMinutes = 12,
            BannerImage = "ic_exam_hero.png"
        };

        var listening2 = new ListeningQuestion
        {
            Title = "Секция 1: Аудирование в деловой среде",
            Instructions = "Прослушайте запись обсуждения корпоративного проекта и выберите цель совещания.",
            MaxPoints = 30.0,
            AudioTitle = "Nuances of Idiomatic Expressions in Journalism",
            AudioSource = "audio_cambridge.mp3",
            SpeakerRole = "Lead Product Manager & Journalist",
            AudioDurationSeconds = 10,
            AudioTranscript = "Cambridge Certificate of Proficiency in English. You will hear an interview with an author discussing the nuances of idiomatic expressions in international journalism.",
            Options = new List<string>
            {
                "Перенос даты релиза мобильного приложения на две недели",
                "Утверждение бюджета на маркетинговую кампанию в соцсетях",
                "Найм пяти дополнительных инженеров в команду тестирования",
                "Закрытие проекта из-за смены стратегического фокуса"
            },
            CorrectOptionIndex = 0
        };

        var matchPairs2 = new MatchPairsQuestion
        {
            Title = "Секция 2: Деловые фразовые глаголы",
            Instructions = "Установите правильные пары между фразовыми глаголами и их синонимами. За каждую ошибку вычитается штраф 6 баллов!",
            MaxPoints = 35.0,
            PenaltyPerMistake = 6.0,
            LeftItems = new List<string>
            {
                "Call off",
                "Put off",
                "Bring up",
                "Look into"
            },
            RightItems = new List<string>
            {
                "Отменить мероприятие",
                "Отложить на более поздний срок",
                "Поднять вопрос для обсуждения",
                "Расследовать или тщательно изучить"
            },
            CorrectMatches = new Dictionary<string, string>
            {
                ["Call off"] = "Отменить мероприятие",
                ["Put off"] = "Отложить на более поздний срок",
                ["Bring up"] = "Поднять вопрос для обсуждения",
                ["Look into"] = "Расследовать или тщательно изучить"
            }
        };

        var essay2 = new EssayQuestion
        {
            Title = "Секция 3: Аргументированное письмо",
            Instructions = "Напишите короткое аналитическое эссе. Требование: не менее 35 слов.",
            MaxPoints = 35.0,
            MinWordCount = 35,
            TopicPrompt = "Discuss the impact of remote work on employee productivity and team collaboration. Provide recommendations for modern companies.",
            RequiredKeywords = new List<string> { "remote", "productivity", "collaboration", "however" }
        };

        cambridge.Questions.Add(listening2);
        cambridge.Questions.Add(matchPairs2);
        cambridge.Questions.Add(essay2);
        tests.Add(cambridge);

        // ТЕСТ 3: TOEFL iBT Express (B1)
        var toefl = new ExamTest
        {
            Id = "test_toefl_b1",
            Title = "TOEFL iBT Express: Intermediate Fluency",
            Description = "Практический тест среднего уровня: понимание диалогов, лексические ассоциации и формулирование мыслей.",
            Level = "B1",
            TimeLimitMinutes = 10,
            BannerImage = "ic_exam_hero.png"
        };

        var listening3 = new ListeningQuestion
        {
            Title = "Секция 1: Аудирование в путешествии",
            Instructions = "Прослушайте разговор у стойки регистрации отеля и определите проблему гостя.",
            MaxPoints = 30.0,
            AudioTitle = "Hotel Reception Inquiry",
            AudioSource = "audio_toefl.mp3",
            SpeakerRole = "Receptionist & Hotel Guest",
            AudioDurationSeconds = 11,
            AudioTranscript = "Front Desk Agent: Good evening, sir. Welcome to the Grandview Hotel. How may I assist you? Guest: Good evening. I have a problem with my room key card. It seems to be demagnetized, and it simply won't unlock room 304. Front Desk Agent: I apologize for the inconvenience. Let me issue a replacement key card right away.",
            Options = new List<string>
            {
                "Ключ-карта размагнитилась и не открывает номер",
                "Багаж был доставлен не в тот терминал аэропорта",
                "Кондиционер в номере работает слишком шумно",
                "Гость хочет заказать ранний завтрак в номер"
            },
            CorrectOptionIndex = 0
        };

        var matchPairs3 = new MatchPairsQuestion
        {
            Title = "Секция 2: Синонимы повседневного английского",
            Instructions = "Сопоставьте слова с их точными синонимами. Неверные ответы штрафуются!",
            MaxPoints = 35.0,
            PenaltyPerMistake = 5.0,
            LeftItems = new List<string>
            {
                "Enormous",
                "Reluctant",
                "Accurate",
                "Courteous"
            },
            RightItems = new List<string>
            {
                "Огромный, колоссальный",
                "Неохотный, сомневающийся",
                "Точный, безошибочный",
                "Вежливый, обходительный"
            },
            CorrectMatches = new Dictionary<string, string>
            {
                ["Enormous"] = "Огромный, колоссальный",
                ["Reluctant"] = "Неохотный, сомневающийся",
                ["Accurate"] = "Точный, безошибочный",
                ["Courteous"] = "Вежливый, обходительный"
            }
        };

        var essay3 = new EssayQuestion
        {
            Title = "Секция 3: Краткое эссе о путешествиях",
            Instructions = "Опишите ваш идеальный город для путешествий. Строго не менее 30 слов.",
            MaxPoints = 35.0,
            MinWordCount = 30,
            TopicPrompt = "Describe an ideal destination for international travelers and explain why experiencing new cultures is beneficial.",
            RequiredKeywords = new List<string> { "travel", "culture", "because", "experience" }
        };

        toefl.Questions.Add(listening3);
        toefl.Questions.Add(matchPairs3);
        toefl.Questions.Add(essay3);
        tests.Add(toefl);

        return tests;
    }
}
