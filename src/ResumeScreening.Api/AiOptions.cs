namespace ResumeScreening.Api;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public const string ApiKeyEnvironmentVariableName = "MY_AI_API_KEY";

    public bool Enabled { get; init; } = true;

    /// <summary>
    /// AI 中转站基础地址（例如 https://your-relay.example.com/）。
    /// 必须配置为绝对 URI。
    /// </summary>
    public string BaseUrl { get; init; } = "";

    /// <summary>
    /// AI API 路径，相对于 BaseUrl（例如 v1/chat/completions）。
    /// </summary>
    public string ApiPath { get; init; } = "v1/chat/completions";

    public string ApiKey { get; init; } =
        Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariableName) ?? string.Empty;

    public string Model { get; init; } = "";

    public int RequestTimeoutSeconds { get; init; } = 180;

    public int MaxOutputTokens { get; init; } = 8_000;
}
