using System.Text.Json;
using System.Text.Json.Serialization;
using ResumeScreening.Contracts;
using ResumeScreening_Desktop.Models;

namespace ResumeScreening_Desktop.Services;

internal static class WorkspaceCacheStore
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ResumeScreening",
        "workspace-cache.json");

    public static async Task<CachedWorkspace> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return CachedWorkspace.Empty;
            }

            await using var stream = new FileStream(
                CachePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: true);
            var workspace = await JsonSerializer.DeserializeAsync<CachedWorkspace>(
                stream,
                JsonOptions,
                cancellationToken);

            if (workspace is not { Version: CurrentVersion })
            {
                return CachedWorkspace.Empty;
            }

            return workspace with
            {
                Resumes = workspace.Resumes ?? [],
                Jobs = workspace.Jobs ?? []
            };
        }
        catch (Exception exception) when (
            exception is JsonException
                or IOException
                or UnauthorizedAccessException)
        {
            return CachedWorkspace.Empty;
        }
    }

    public static async Task SaveAsync(
        IEnumerable<ResumeItem> resumes,
        IEnumerable<ScreeningJob> jobs,
        Func<ResumeItem, ResumeAnalysisDraft?> getAnalysisDraft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resumes);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(getAnalysisDraft);

        var cachedResumes = resumes
            .Select(resume => new CachedResume(
                resume.Id,
                resume.Profile,
                resume.FileName,
                resume.FilePath,
                resume.TypeLabel,
                resume.DocumentInfo,
                resume.Pages,
                resume.ImportedAt,
                getAnalysisDraft(resume)))
            .ToArray();

        var cachedJobs = jobs
            .Select(job => new CachedScreeningJob(
                job.Id,
                job.Title,
                job.Requirements,
                job.CreatedAt,
                job.Resumes
                    .Select(item => new CachedScreenedResume(item.Resume.Id, item.Draft))
                    .ToArray()))
            .ToArray();

        var workspace = new CachedWorkspace(CurrentVersion, cachedResumes, cachedJobs);
        var directory = Path.GetDirectoryName(CachePath)
            ?? throw new InvalidOperationException("无法确定工作区缓存目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{CachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, workspace, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, CachePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static void Clear()
    {
        if (File.Exists(CachePath))
        {
            File.Delete(CachePath);
        }

        var directory = Path.GetDirectoryName(CachePath);
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        foreach (var temporaryPath in Directory.EnumerateFiles(
                     directory,
                     $"{Path.GetFileName(CachePath)}.*.tmp",
                     SearchOption.TopDirectoryOnly))
        {
            File.Delete(temporaryPath);
        }
    }
}

internal sealed record CachedWorkspace(
    int Version,
    IReadOnlyList<CachedResume> Resumes,
    IReadOnlyList<CachedScreeningJob> Jobs)
{
    public static CachedWorkspace Empty { get; } = new(1, [], []);
}

internal sealed record CachedResume(
    Guid Id,
    ResumeProfile Profile,
    string FileName,
    string FilePath,
    string TypeLabel,
    string DocumentInfo,
    IReadOnlyList<ResumePage> Pages,
    DateTimeOffset ImportedAt,
    ResumeAnalysisDraft? AnalysisDraft);

internal sealed record CachedScreeningJob(
    Guid Id,
    string Title,
    string Requirements,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CachedScreenedResume> Resumes);

internal sealed record CachedScreenedResume(
    Guid ResumeId,
    JobScreeningDraft Draft);
