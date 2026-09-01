using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;


namespace ResumeScreening.Contracts;

/// <summary>
/// 强制输出类型
/// </summary>
/// <param name="Candidate"></param>
/// <param name="Educations"></param>
/// <param name="WorkExperiences"></param>
/// <param name="Projects"></param>
/// <param name="Skills"></param>
/// <param name="Findings"></param>
/// <param name="MissingInformation"></param>
public sealed record ResumeAnalysisResult(
    CandidateFacts Candidate,
    IReadOnlyList<EducationEntry> Educations,
    IReadOnlyList<WorkExperienceEntry> WorkExperiences,
    IReadOnlyList<ProjectExperienceEntry> Projects,
    IReadOnlyList<SkillEntry> Skills,
    IReadOnlyList<ReviewFinding> Findings,
    IReadOnlyList<string> MissingInformation);

/// <summary>
/// 候选人基本信息
/// </summary>
/// <param name="Name">姓名</param>
/// <param name="Email">邮箱</param>
/// <param name="Phone">电话</param>
/// <param name="age">年龄</param>
/// <param name="gender">性别</param>
/// <param name="Institution">学校</param>
/// <param name="EducationLevel">学历</param>
/// <param name="Major">专业</param>
public sealed record CandidateFacts(
    EvidenceBackedText Name,
    EvidenceBackedText Email,
    EvidenceBackedText Phone,
    EvidenceBackedText Age,
    EvidenceBackedText Gender,
    EvidenceBackedText Institution,
    EvidenceBackedText EducationLevel,
    EvidenceBackedText Major);

/// <summary>
/// 带证据的文本结论
/// </summary>
/// <param name="Value">AI总结出的结论</param>
/// <param name="Evidence">AI结论对应的简历证据</param>
public sealed record EvidenceBackedText(
    string? Value,
    IReadOnlyList<ResumeEvidence> Evidence);

/// <summary>
/// 详细的带证据的教育信息
/// </summary>
/// <param name="Institution">学校</param>
/// <param name="Degree">学位</param>
/// <param name="Major">专业</param>
/// <param name="StartDate">开始时间</param>
/// <param name="EndDate">结束时间</param>
/// <param name="Evidence">证据</param>
public sealed record EducationEntry(
    string? Institution,
    string? Degree,
    string? Major,
    string? StartDate,
    string? EndDate,
    IReadOnlyList<ResumeEvidence> Evidence);

/// <summary>
/// 工作或实习经历
/// </summary>
/// <param name="Employer">公司或组织名称</param>
/// <param name="JobTitle">职位名称</param>
/// <param name="StartDate">入职时间</param>
/// <param name="EndDate">离职时间；如果仍在职，可以使用 "至今"</param>
/// <param name="Responsibilities">具体职责或工作内容</param>
/// <param name="Evidence">支持整段工作经历的页码和原文</param>
public sealed record WorkExperienceEntry(
    string? Employer,
    string? JobTitle,
    string? StartDate,
    string? EndDate,
    IReadOnlyList<string> Responsibilities,
    IReadOnlyList<ResumeEvidence> Evidence);

/// <summary>
/// 项目经历
/// </summary>
/// <param name="ProjectName">项目名称</param>
/// <param name="Role">候选人在项目中的角色</param>
/// <param name="Description">项目目标</param>
/// <param name="Technologies">项目中明确使用的技术</param>
/// <param name="Evidence">支持整段项目经历的页码和原文</param>
public sealed record ProjectExperienceEntry(
    string? ProjectName,
    string? Role,
    string? Description,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<ResumeEvidence> Evidence);

/// <summary>
/// 候选人技能
/// </summary>
/// <param name="Name">技能名称</param>
/// <param name="Context">技能的使用背景</param>
/// <param name="Evidence">支持该技能结论的页码和简历原文</param>
public sealed record SkillEntry(
    string Name,
    string? Context,
    IReadOnlyList<ResumeEvidence> Evidence);

/// <summary>
/// 审核发现
/// </summary>
/// <param name="Kind">结论类别</param>
/// <param name="Conclusion">给审核人员看的结论</param>
/// <param name="Evidence">支持结论的页码和原文</param>
public sealed record ReviewFinding(
    ReviewFindingKind Kind,
    string Conclusion,
    IReadOnlyList<ResumeEvidence> Evidence);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewFindingKind
{
    Strength,
    Concern,
    NeedsVerification
}

/// <summary>
/// AI结论对应的简历证据
/// </summary>
/// <param name="PageNumber">证据所在的简历页码</param>
/// <param name="Quote">从该页摘取的原文</param>
public sealed record ResumeEvidence(
    int PageNumber,
    string Quote);



/* -------------------------------------------------------- */
/// <summary>
/// 记录额外信息
/// </summary>
/// <param name="Result">AI 解析出的候选人事实、教育经历、工作经历、技能和审核发现</param>
/// <param name="SchemaVersion">结果结构的版本，例如 "1.0"</param>
/// <param name="Model">生成结果的模型名称，方便审计和复现</param>
/// <param name="GeneratedAt">生成草稿的时间</param>
/// <param name="ReviewStatus">人工审核状态</param>
/// <param name="ValidationWarnings">程序校验结果时发现的问题</param>
public sealed record ResumeAnalysisDraft(
    ResumeAnalysisResult Result,
    string SchemaVersion,
    string Model,
    DateTimeOffset GeneratedAt,
    HumanReviewStatus ReviewStatus,
    IReadOnlyList<string> ValidationWarnings);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HumanReviewStatus
{
    PendingHumanReview,
    Reviewed
}