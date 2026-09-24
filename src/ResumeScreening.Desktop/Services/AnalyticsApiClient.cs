using System.Net.Http.Json;
using ResumeScreening.Contracts;

namespace ResumeScreening_Desktop.Services;

public interface IAnalyticsApiClient
{
    Task<AiAnalyticsReport> GetReportAsync(CancellationToken cancellationToken = default);
}

public sealed class AnalyticsApiClient : IAnalyticsApiClient
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("http://localhost:49851/"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<AiAnalyticsReport> GetReportAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await HttpClient.GetFromJsonAsync<AiAnalyticsReport>(
                "api/analytics",
                cancellationToken)
                ?? throw new AnalyticsApiException("分析报表接口返回了空结果。");
        }
        catch (AnalyticsApiException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AnalyticsApiException("读取分析报表超时，请稍后重试。");
        }
        catch (HttpRequestException exception)
        {
            throw new AnalyticsApiException("无法连接后台 API，请先启动 ResumeScreening.Api。", exception);
        }
    }
}

public sealed class AnalyticsApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);
