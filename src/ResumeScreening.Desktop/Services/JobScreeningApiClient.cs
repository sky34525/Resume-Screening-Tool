using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResumeScreening.Contracts;

namespace ResumeScreening_Desktop.Services;

public interface IJobScreeningApiClient
{
    Task<JobScreeningDraft> ScreenAsync(
        JobScreeningRequest request,
        CancellationToken cancellationToken);
}

public sealed class JobScreeningApiClient : IJobScreeningApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:52081/"),
        Timeout = TimeSpan.FromSeconds(210)
    };

    public async Task<JobScreeningDraft> ScreenAsync(
        JobScreeningRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/job-screening", request, JsonOptions, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var detail = TryReadProblemDetail(responseBody);
                var message = (int)response.StatusCode == 404
                    ? "后台 API 不包含单次 AI 初筛接口。请停止并重新启动 ResumeScreening.Api 后再试。"
                    : detail ?? $"职位初筛失败：HTTP {(int)response.StatusCode}。";
                throw new JobScreeningApiException(message);
            }

            JobScreeningDraft? draft;
            try
            {
                draft = JsonSerializer.Deserialize<JobScreeningDraft>(responseBody, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new JobScreeningApiException(
                    "后台返回的数据与当前桌面端不兼容。请同时重启 ResumeScreening.Api 和桌面端。",
                    exception);
            }

            if (draft?.ResumeAnalysis is null || string.IsNullOrWhiteSpace(draft.SchemaVersion))
            {
                throw new JobScreeningApiException(
                    "后台仍在使用旧版初筛结果格式。请停止并重新启动 ResumeScreening.Api 后再试。");
            }

            return draft;
        }
        catch (JobScreeningApiException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new JobScreeningApiException("职位初筛等待超时，请稍后重试。");
        }
        catch (HttpRequestException exception)
        {
            throw new JobScreeningApiException("无法连接后台 API，请先启动 ResumeScreening.Api。", exception);
        }
    }

    private static string? TryReadProblemDetail(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("detail", out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class JobScreeningApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);
