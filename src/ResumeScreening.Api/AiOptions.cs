namespace ResumeScreening.Api;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public const string ApiKeyEnvironmentVariableName = "MY_DEEPSEEK_API_KEY";

    public bool Enabled { get; init; } = true;

    public string BaseUrl { get; init; } = "https://api.deepseek.com/";

    public string ApiKey { get; init; } =
        Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariableName) ?? string.Empty;

    public string Model { get; init; } = "deepseek-v4-pro";

    public int RequestTimeoutSeconds { get; init; } = 180;

    public int MaxOutputTokens { get; init; } = 8_000;
}
