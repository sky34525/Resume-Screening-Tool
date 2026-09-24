using System.Text.Json.Serialization;

namespace ResumeScreening.Contracts;

public sealed record JobScreeningRequest(
    string JobTitle,
    string Requirements,
    IReadOnlyList<ResumePage> Pages,
    ScreeningWeights Weights);

public sealed record ScreeningWeights(
    int Education,
    int Gender,
    int Age,
    int WorkExperience,
    int MajorMatch)
{
    public static ScreeningWeights Default { get; } = new(
        Education: 20,
        Gender: 0,
        Age: 0,
        WorkExperience: 40,
        MajorMatch: 40);

    [JsonIgnore]
    public int Total => Education + Gender + Age + WorkExperience + MajorMatch;

    [JsonIgnore]
    public bool IsValid =>
        Education is >= 0 and <= 100
        && Gender is >= 0 and <= 100
        && Age is >= 0 and <= 100
        && WorkExperience is >= 0 and <= 100
        && MajorMatch is >= 0 and <= 100
        && Total == 100;
}

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
