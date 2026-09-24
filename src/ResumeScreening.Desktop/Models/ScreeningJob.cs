using ResumeScreening.Contracts;

namespace ResumeScreening_Desktop.Models;

public sealed class ScreeningJob(
    string title,
    string requirements,
    Guid? id = null,
    DateTimeOffset? createdAt = null)
{
    public Guid Id { get; } = id ?? Guid.NewGuid();

    public string Title { get; } = title;

    public string Requirements { get; } = requirements;

    public DateTimeOffset CreatedAt { get; } = createdAt ?? DateTimeOffset.Now;

    public List<ScreenedResume> Resumes { get; } = [];
}

public sealed record ScreenedResume(ResumeItem Resume, JobScreeningDraft Draft);
