using laba10.Models;

namespace laba10.Services;

public interface IResultService
{
    Task<List<ExamResult>> GetResultsAsync();
    Task SaveResultAsync(ExamResult result);
    Task<string> ProcessAppealAsync(string resultId);
    Task<string> ExportResultCertificateAsync(string resultId);
    Task<bool> DeleteResultAsync(string resultId);
    Task ClearAllResultsAsync();
}
