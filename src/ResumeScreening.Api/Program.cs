using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ResumeScreening.Api;
using ResumeScreening.Api.Services;
using ResumeScreening.Contracts;

var builder = WebApplication.CreateBuilder(args);

//配置JSON序列化行为
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;    //首字母小写
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());        //枚举序列化为字符串
});

builder.Services
    .AddOptions<AiOptions>()                                                        //注册强类型对象AiOptions
    .Bind(builder.Configuration.GetSection(AiOptions.SectionName))                  //配置文件中的节点绑定到AiOptions
    .Validate(                                                                      //启动时的配置校验规则
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "AI:BaseUrl 必须是绝对地址。")
    .Validate(
        options => options.RequestTimeoutSeconds is >= 1 and <= 300,
        "AI:RequestTimeoutSeconds 必须在 1 到 300 之间。")
    .Validate(
        options => options.MaxOutputTokens is >= 256 and <= 16_384,
        "AI:MaxOutputTokens 必须在 256 到 16384 之间。")
    .Validate(
        options => !options.Enabled
            || (!string.IsNullOrWhiteSpace(options.ApiKey)
                && !string.IsNullOrWhiteSpace(options.Model)),
        "启用 AI 时必须配置 AI:ApiKey 和 AI:Model。")
    .ValidateOnStart();

builder.Services.AddHttpClient<IResumeAnalysisService, OpenAiResumeAnalysisService>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
        var normalizedBaseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? options.BaseUrl
            : $"{options.BaseUrl}/";
        client.BaseAddress = new Uri(normalizedBaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    });

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost(
    "/api/resume-analysis",
    async (ResumeAnalysisRequest request, IResumeAnalysisService service, CancellationToken cancellationToken) =>
    {
        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        try
        {
            var draft = await service.AnalyzeAsync(request.Pages, cancellationToken);
            return Results.Ok(draft);
        }
        catch (AiAnalysisUnavailableException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "AI 解析不可用",
                detail: exception.Message);
        }
        catch (OpenAiRefusalException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "AI 拒绝处理",
                detail: exception.Message);
        }
        catch (OpenAiAnalysisException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "AI 解析失败",
                detail: exception.Message);
        }
    });

app.Run();

static Dictionary<string, string[]> ValidateRequest(ResumeAnalysisRequest request)
{
    const int maxPages = 100;
    const int maxCharacters = 200_000;
    var errors = new Dictionary<string, string[]>();

    if (request.Pages is null || request.Pages.Count == 0)
    {
        errors["pages"] = ["至少需要一页简历内容。"];
        return errors;
    }

    if (request.Pages.Count > maxPages)
    {
        errors["pages"] = [$"简历不能超过 {maxPages} 页。"];
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

    var totalCharacters = request.Pages.Sum(page => page.Text?.Length ?? 0);
    if (totalCharacters > maxCharacters)
    {
        errors["pages.text"] = [$"简历文本不能超过 {maxCharacters:N0} 个字符。"];
    }

    return errors;
}
