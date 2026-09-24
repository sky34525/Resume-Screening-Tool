using System.Net.Http.Json;
using System.Text.Json;
using ResumeScreening.Contracts;

namespace ResumeScreening_Desktop.Services;

public interface IAiSettingsApiClient
{
    Task<AiSettingsStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<AiSettingsStatus> SaveApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}

public sealed class AiSettingsApiClient : IAiSettingsApiClient
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("http://localhost:49851/"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<AiSettingsStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.GetAsync("api/ai-settings", cancellationToken);
        return await ReadResponseAsync(response, cancellationToken);
    }

    public async Task<AiSettingsStatus> SaveApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.PostAsJsonAsync(
            "api/ai-settings",
            new UpdateAiSettingsRequest(apiKey),
            cancellationToken);
        return await ReadResponseAsync(response, cancellationToken);
    }

    private static async Task<AiSettingsStatus> ReadResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AiSettingsStatus>(cancellationToken)
                ?? throw new AiSettingsApiException("设置接口返回了空结果。");
        }

        var message = await TryReadProblemDetailAsync(response, cancellationToken)
            ?? $"设置接口返回 HTTP {(int)response.StatusCode}。";
        throw new AiSettingsApiException(message);
    }

    private static async Task<string?> TryReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }

            if (root.TryGetProperty("errors", out var errors))
            {
                foreach (var error in errors.EnumerateObject())
                {
                    if (error.Value.ValueKind == JsonValueKind.Array
                        && error.Value.GetArrayLength() > 0)
                    {
                        return error.Value[0].GetString();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Fall back to the HTTP status message below.
        }

        return null;
    }
}

public sealed class AiSettingsApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);
