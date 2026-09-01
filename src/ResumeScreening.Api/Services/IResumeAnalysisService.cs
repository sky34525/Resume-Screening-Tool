using ResumeScreening.Contracts;

namespace ResumeScreening.Api.Services;

public interface IResumeAnalysisService
{
    Task<ResumeAnalysisDraft> AnalyzeAsync(
        IReadOnlyList<ResumePage> pages,
        CancellationToken cancellationToken);
}

public sealed class AiAnalysisUnavailableException(string message) : Exception(message);

public sealed class OpenAiAnalysisException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class OpenAiRefusalException(string message) : Exception(message);
