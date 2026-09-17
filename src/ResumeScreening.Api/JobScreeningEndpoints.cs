using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ResumeScreening.Api.Services;
using ResumeScreening.Contracts;

namespace ResumeScreening.Api;

public static class JobScreeningEndpoints
{
    public static IServiceCollection AddJobScreening(this IServiceCollection services)
    {
        services.AddHttpClient<IJobScreeningService, OpenAiJobScreeningService>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
                var baseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
                    ? options.BaseUrl
                    : $"{options.BaseUrl}/";
                client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            });
        return services;
    }

    public static IEndpointRouteBuilder MapJobScreening(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/job-screening",
            async ([FromBody] JobScreeningRequest request, IJobScreeningService service, CancellationToken cancellationToken) =>
            {
                var errors = Validate(request);
                if (errors.Count > 0)
                {
                    return Results.ValidationProblem(errors);
                }

                try
                {
                    return Results.Ok(await service.ScreenAsync(request, cancellationToken));
                }
                catch (AiAnalysisUnavailableException exception)
                {
                    return Results.Problem(statusCode: 503, title: "AI 初筛不可用", detail: exception.Message);
                }
                catch (OpenAiAnalysisException exception)
                {
                    return Results.Problem(statusCode: 502, title: "AI 初筛失败", detail: exception.Message);
                }
            });
        return endpoints;
    }

    private static Dictionary<string, string[]> Validate(JobScreeningRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.JobTitle) || request.JobTitle.Length > 100)
        {
            errors["jobTitle"] = ["职位名称不能为空且不能超过 100 个字符。"];
        }
        if (string.IsNullOrWhiteSpace(request.Requirements) || request.Requirements.Length > 10_000)
        {
            errors["requirements"] = ["招聘要求不能为空且不能超过 10000 个字符。"];
        }
        if (request.Pages is null || request.Pages.Count == 0)
        {
            errors["pages"] = ["至少需要一页简历内容。"];
            return errors;
        }
        if (request.Pages.Count > 100)
        {
            errors["pages"] = ["简历不能超过 100 页。"];
        }

        var expectedPageNumber = 1;
        foreach (var page in request.Pages.OrderBy(page => page.PageNumber))
        {
            if (page.PageNumber != expectedPageNumber)
            {
                errors["pages.pageNumber"] = ["页码必须从 1 开始且连续、不能重复。"];
                break;
            }

            expectedPageNumber++;
        }
        if (request.Pages.Any(page => page.Text is null))
        {
            errors["pages.text"] = ["页面文本不能为 null；无文字页面请使用空字符串。"];
        }
        if (request.Pages.All(page => string.IsNullOrWhiteSpace(page.Text)))
        {
            errors["pages.text"] = ["简历没有可供 AI 分析的文字；扫描件或图片需要先完成 OCR。"];
        }
        if (request.Pages.Sum(page => page.Text?.Length ?? 0) > 200_000)
        {
            errors["pages.text"] = ["简历文本不能超过 200,000 个字符。"];
        }
        return errors;
    }
}
