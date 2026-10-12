using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using ResumeScreening.Api;
using ResumeScreening.Api.Services;
using ResumeScreening.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>                                      //配置JSON序列化行为
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;          //首字母小写
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());              //枚举序列化为字符串
});

builder.Services
    .AddOptions<AiOptions>()                                                              //注册强类型对象AiOptions
    .Bind(builder.Configuration.GetSection(AiOptions.SectionName))                        //配置文件中的节点绑定到AiOptions
    .Validate(                                                                            //启动时的配置校验规则
        options => !options.Enabled || Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "启用 AI 时 AI:BaseUrl 必须配置为绝对地址。")
    .Validate(
        options => options.RequestTimeoutSeconds is >= 1 and <= 300,
        "AI:RequestTimeoutSeconds 必须在 1 到 300 之间。")
    .Validate(
        options => options.MaxOutputTokens is >= 256 and <= 16_384,
        "AI:MaxOutputTokens 必须在 256 到 16384 之间。")
    .Validate(
        options => !options.Enabled
            || !string.IsNullOrWhiteSpace(options.Model),
        "启用 AI 时必须配置 AI:Model。")
    .ValidateOnStart();

var aiDataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ResumeScreening");
var dataProtectionDirectory = Directory.CreateDirectory(
    Path.Combine(aiDataDirectory, "DataProtectionKeys"));

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("ResumeScreening")
    .PersistKeysToFileSystem(dataProtectionDirectory);
if (OperatingSystem.IsWindows())
{
    dataProtectionBuilder.ProtectKeysWithDpapi();
}
builder.Services.AddSingleton<IAiCredentialStore, ProtectedAiCredentialStore>();
builder.Services.AddSingleton<IAiUsageMetricsStore, AiUsageMetricsStore>();

builder.Services.AddHttpClient<IResumeAnalysisService, OpenAiResumeAnalysisService>(      //注册带类型的HttpClient，在需要IResumeAnalysisService时创建OpenAiResumeAnalysisService并注入client
    (serviceProvider, client) =>                                                          //serviceProvider代表当前的DI容器,client为注入OpenAiResumeAnalysisService中的client
    {
        var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;    //从DI容器中获取已经注册的AiOptions
        var normalizedBaseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)   //确保BaseUrl以/结尾,避免后续拼接接口出错
            ? options.BaseUrl
            : $"{options.BaseUrl}/";
        client.BaseAddress = new Uri(normalizedBaseUrl, UriKind.Absolute);                //给client设置基础请求地址，normalizedBaseUrl  解析成一个 URI，并且要求它是完整的绝对 UR
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);             //设置请求超时时间，单位为秒
        client.DefaultRequestHeaders.Accept.Add(                                          //给client添加默认请求头 Accept: application/json
            new MediaTypeWithQualityHeaderValue("application/json"));
    });

builder.Services.AddJobScreening();

var app = builder.Build();                                                                //构建WebApplication对象

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));                           //健康检查Get接口

app.MapPost(
    "/api/resume-analysis",                                                               //接口路径
    async (ResumeAnalysisRequest request, IResumeAnalysisService service, CancellationToken cancellationToken) =>  //传入：简历分析请求数据，简历分析业务数据，ASP.NET Core 自动提供的中断异步操作
    {
        var validationErrors = ValidateRequest(request);                                  //校验客户端提交的请求数据，如果不校验就调用AI会浪费AI请求次数，影响系统稳定性
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);                           //返回 HTTP 400 Bad Request，并带上标准格式的校验错误信息
        }

        try
        {
            var draft = await service.AnalyzeAsync(request.Pages, cancellationToken);     //异步等待简历解析结果， cancellationToken用于发送停止信号
            return Results.Ok(draft);
        }
        catch (AiAnalysisUnavailableException exception)                                  //捕获AI分析不可用异常
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "AI 解析不可用",
                detail: exception.Message);
        }
        catch (OpenAiRefusalException exception)                                          //捕获AI拒接处理异常
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "AI 拒绝处理",
                detail: exception.Message);
        }
        catch (OpenAiAnalysisException exception)                                         //捕获AI解析失败异常
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "AI 解析失败",
                detail: exception.Message);
        }
    });

app.MapJobScreening();
app.MapAiSettings();
app.MapAnalytics();

app.Run();                                                                                //启动 Web 服务器，开始监听 HTTP 请求。

static Dictionary<string, string[]> ValidateRequest(ResumeAnalysisRequest request)        //校验客户端提交的请求数据
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
