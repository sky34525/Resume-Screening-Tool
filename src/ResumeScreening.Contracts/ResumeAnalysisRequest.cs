namespace ResumeScreening.Contracts;

/// <summary>
/// 请求 API 解析一份已经按物理页拆分的简历。
/// </summary>
public sealed record ResumeAnalysisRequest(
    IReadOnlyList<ResumePage> Pages);
