using System.Text.Json;
using ResumeScreening.Contracts;

namespace ResumeScreening_Desktop.Services;

internal static class ScreeningWeightPreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ResumeScreening",
        "screening-weights.json");

    public static ScreeningWeights Load()
    {
        try
        {
            if (!File.Exists(PreferencePath))
            {
                return ScreeningWeights.Default;
            }

            var weights = JsonSerializer.Deserialize<ScreeningWeights>(
                File.ReadAllText(PreferencePath),
                JsonOptions);
            return weights?.IsValid == true ? weights : ScreeningWeights.Default;
        }
        catch (Exception exception) when (
            exception is JsonException
                or IOException
                or UnauthorizedAccessException)
        {
            return ScreeningWeights.Default;
        }
    }

    public static void Save(ScreeningWeights weights)
    {
        if (!weights.IsValid)
        {
            throw new ArgumentException("筛选权重必须在 0 到 100 之间，且合计为 100%。", nameof(weights));
        }

        var directory = Path.GetDirectoryName(PreferencePath)
            ?? throw new InvalidOperationException("无法确定筛选权重配置目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{PreferencePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(weights, JsonOptions));
            File.Move(temporaryPath, PreferencePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
