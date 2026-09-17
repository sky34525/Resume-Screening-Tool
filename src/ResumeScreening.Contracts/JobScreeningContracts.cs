using System.Text.Json.Serialization;

namespace ResumeScreening.Contracts;

public sealed record JobScreeningRequest(
    string JobTitle,
    string Requirements,
    IReadOnlyList<ResumePage> Pages);

public sealed record JobScreeningResult(
    ScreeningGrade Grade,
    string Summary,
    IReadOnlyList<ResumeEvidence> Evidence);

public sealed record JobScreeningDraft(
    JobScreeningResult Result,
    ResumeAnalysisResult ResumeAnalysis,
    string SchemaVersion,
    string Model,
    DateTimeOffset GeneratedAt,
    HumanReviewStatus ReviewStatus,
    IReadOnlyList<string> ValidationWarnings);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScreeningGrade
{
    A,
    B,
    C
}
