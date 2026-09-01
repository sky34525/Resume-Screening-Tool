namespace ResumeScreening.Api;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = "https://api.openai.com/v1/";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "gpt-4o-mini";

    public int RequestTimeoutSeconds { get; init; } = 45;

    public int MaxOutputTokens { get; init; } = 8_000;
}
