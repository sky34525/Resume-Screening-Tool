using ResumeScreening.Contracts;

namespace ResumeScreening.Api.Services;

public interface IJobScreeningService
{
    Task<JobScreeningDraft> ScreenAsync(
        JobScreeningRequest request,
        CancellationToken cancellationToken);
}
