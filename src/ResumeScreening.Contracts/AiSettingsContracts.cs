namespace ResumeScreening.Contracts;

public sealed record UpdateAiSettingsRequest(string ApiKey);

public sealed record AiSettingsStatus(bool Configured);
