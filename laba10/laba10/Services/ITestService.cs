using laba10.Models;

namespace laba10.Services;

public interface ITestService
{
    Task<List<ExamTest>> GetTestsAsync();
    Task<List<ExamTest>> GetFilteredTestsAsync(string level);
    Task<ExamTest?> GetTestByIdAsync(string id);
    Task<ExamTest> DuplicateTestAsync(string id);
    Task<bool> DeleteTestAsync(string id);
    Task<bool> ResetTestProgressAsync(string id);
    Task<string> ExportTestToJsonAsync(string id);
    Task AddOrUpdateTestAsync(ExamTest test);
    Task UpdateTestResultAsync(string testId, double score);

    // Заморозка и возобновление теста
    Task SaveFrozenStateAsync(FrozenTestState state);
    Task<FrozenTestState?> GetFrozenStateAsync();
    Task ClearFrozenStateAsync();
}
