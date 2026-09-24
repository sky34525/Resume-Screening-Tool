using System.Net;
using ResumeScreening.Api.Services;
using ResumeScreening.Contracts;

namespace ResumeScreening.Api;

public static class AiSettingsEndpoints
{
    private const int MaximumApiKeyLength = 512;

    public static IEndpointRouteBuilder MapAiSettings(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/ai-settings",
            (HttpContext context, IAiCredentialStore credentialStore) =>
            {
                if (!IsLoopbackRequest(context))
                {
                    return Results.Forbid();
                }

                return Results.Ok(new AiSettingsStatus(credentialStore.IsConfigured));
            });

        endpoints.MapPost(
            "/api/ai-settings",
            (HttpContext context, UpdateAiSettingsRequest request, IAiCredentialStore credentialStore) =>
            {
                if (!IsLoopbackRequest(context))
                {
                    return Results.Forbid();
                }

                var apiKey = request.ApiKey?.Trim();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["apiKey"] = ["DeepSeek API Key 不能为空。"]
                    });
                }

                if (apiKey.Length > MaximumApiKeyLength)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["apiKey"] = [$"DeepSeek API Key 不能超过 {MaximumApiKeyLength} 个字符。"]
                    });
                }

                credentialStore.SetApiKey(apiKey);
                return Results.Ok(new AiSettingsStatus(Configured: true));
            });

        return endpoints;
    }

    private static bool IsLoopbackRequest(HttpContext context)
    {
        var remoteIpAddress = context.Connection.RemoteIpAddress;
        return remoteIpAddress is not null && IPAddress.IsLoopback(remoteIpAddress);
    }
}
