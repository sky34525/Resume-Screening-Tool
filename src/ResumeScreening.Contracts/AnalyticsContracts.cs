namespace ResumeScreening.Contracts;

public sealed record AiAnalyticsReport(
    long ParsedResumes,
    long AiCalls,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    IReadOnlyList<AiAnalyticsDailyPoint> LastSevenDays);

public sealed record AiAnalyticsDailyPoint(
    DateOnly Date,
    long ParsedResumes,
    long AiCalls,
    long InputTokens,
    long OutputTokens,
    long TotalTokens);
