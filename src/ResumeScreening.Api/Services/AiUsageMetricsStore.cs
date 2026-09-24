using System.Text.Json;
using ResumeScreening.Contracts;

namespace ResumeScreening.Api.Services;

public interface IAiUsageMetricsStore
{
    void RecordAiCall();

    void RecordTokenUsage(AiTokenUsage usage);

    void RecordResumeParsed();

    AiAnalyticsReport GetReport();
}

public readonly record struct AiTokenUsage(long InputTokens, long OutputTokens, long TotalTokens);

public sealed class AiUsageMetricsStore : IAiUsageMetricsStore
{
    private const int RetentionDays = 3650;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly ILogger<AiUsageMetricsStore> _logger;
    private readonly string _metricsPath;
    private readonly Dictionary<DateOnly, MutableDailyMetrics> _dailyMetrics;

    public AiUsageMetricsStore(ILogger<AiUsageMetricsStore> logger)
    {
        _logger = logger;
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ResumeScreening");
        Directory.CreateDirectory(dataDirectory);
        _metricsPath = Path.Combine(dataDirectory, "ai-analytics.json");
        _dailyMetrics = Load();
    }

    public void RecordAiCall() => Mutate(day => day.AiCalls++);

    public void RecordTokenUsage(AiTokenUsage usage)
    {
        if (usage.InputTokens <= 0 && usage.OutputTokens <= 0 && usage.TotalTokens <= 0)
        {
            return;
        }

        Mutate(day =>
        {
            day.InputTokens += Math.Max(0, usage.InputTokens);
            day.OutputTokens += Math.Max(0, usage.OutputTokens);
            day.TotalTokens += Math.Max(0, usage.TotalTokens);
        });
    }

    public void RecordResumeParsed() => Mutate(day => day.ParsedResumes++);

    public AiAnalyticsReport GetReport()
    {
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var lastSevenDays = Enumerable.Range(0, 7)
                .Select(offset => today.AddDays(offset - 6))
                .Select(date => ToContract(date, GetOrEmpty(date)))
                .ToArray();

            return new AiAnalyticsReport(
                _dailyMetrics.Values.Sum(item => item.ParsedResumes),
                _dailyMetrics.Values.Sum(item => item.AiCalls),
                _dailyMetrics.Values.Sum(item => item.InputTokens),
                _dailyMetrics.Values.Sum(item => item.OutputTokens),
                _dailyMetrics.Values.Sum(item => item.TotalTokens),
                lastSevenDays);
        }
    }

    private void Mutate(Action<MutableDailyMetrics> update)
    {
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (!_dailyMetrics.TryGetValue(today, out var daily))
            {
                daily = new MutableDailyMetrics { Date = today };
                _dailyMetrics.Add(today, daily);
            }

            update(daily);
            var cutoff = today.AddDays(-RetentionDays);
            foreach (var expiredDate in _dailyMetrics.Keys.Where(date => date < cutoff).ToArray())
            {
                _dailyMetrics.Remove(expiredDate);
            }

            TryPersist();
        }
    }

    private Dictionary<DateOnly, MutableDailyMetrics> Load()
    {
        if (!File.Exists(_metricsPath))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<MutableDailyMetrics>>(
                File.ReadAllText(_metricsPath),
                JsonOptions) ?? [];
            return entries
                .GroupBy(item => item.Date)
                .ToDictionary(group => group.Key, group => group.Last());
        }
        catch (Exception exception) when (
            exception is JsonException
                or IOException
                or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "AI analytics data could not be loaded; starting with empty metrics.");
            return [];
        }
    }

    private void TryPersist()
    {
        var temporaryPath = $"{_metricsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var entries = _dailyMetrics.Values.OrderBy(item => item.Date).ToArray();
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries, JsonOptions));
            File.Move(temporaryPath, _metricsPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "AI analytics data could not be persisted.");
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Temporary AI analytics file could not be removed.");
            }
        }
    }

    private MutableDailyMetrics GetOrEmpty(DateOnly date) =>
        _dailyMetrics.TryGetValue(date, out var metrics)
            ? metrics
            : new MutableDailyMetrics { Date = date };

    private static AiAnalyticsDailyPoint ToContract(DateOnly date, MutableDailyMetrics metrics) =>
        new(
            date,
            metrics.ParsedResumes,
            metrics.AiCalls,
            metrics.InputTokens,
            metrics.OutputTokens,
            metrics.TotalTokens);

    private sealed class MutableDailyMetrics
    {
        public DateOnly Date { get; set; }

        public long ParsedResumes { get; set; }

        public long AiCalls { get; set; }

        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }

        public long TotalTokens { get; set; }
    }
}
