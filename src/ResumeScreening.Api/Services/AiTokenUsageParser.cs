using System.Text.Json;

namespace ResumeScreening.Api.Services;

internal static class AiTokenUsageParser
{
    public static AiTokenUsage Parse(JsonElement responseRoot)
    {
        if (!responseRoot.TryGetProperty("usage", out var usage)
            || usage.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        var inputTokens = ReadNonNegativeInt64(usage, "input_tokens", "prompt_tokens");
        var outputTokens = ReadNonNegativeInt64(usage, "output_tokens", "completion_tokens");
        var totalTokens = ReadNonNegativeInt64(usage, "total_tokens");
        if (totalTokens == 0)
        {
            totalTokens = inputTokens + outputTokens;
        }

        return new AiTokenUsage(inputTokens, outputTokens, totalTokens);
    }

    private static long ReadNonNegativeInt64(JsonElement parent, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (parent.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt64(out var number))
            {
                return Math.Max(0, number);
            }
        }

        return 0;
    }
}
