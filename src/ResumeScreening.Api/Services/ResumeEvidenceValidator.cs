using System.Text.RegularExpressions;
using ResumeScreening.Contracts;

namespace ResumeScreening.Api.Services;

internal static partial class ResumeEvidenceValidator
{
    public static IReadOnlyList<string> Validate(
        ResumeAnalysisResult result,
        IReadOnlyList<ResumePage> pages)
    {
        var warnings = new List<string>();
        var pageLookup = pages.ToDictionary(page => page.PageNumber);

        ValidateField("candidate.name", result.Candidate.Name, pageLookup, warnings);
        ValidateField("candidate.email", result.Candidate.Email, pageLookup, warnings);
        ValidateField("candidate.phone", result.Candidate.Phone, pageLookup, warnings);
        ValidateField("candidate.age", result.Candidate.Age, pageLookup, warnings);
        ValidateField("candidate.gender", result.Candidate.Gender, pageLookup, warnings);
        ValidateField("candidate.institution", result.Candidate.Institution, pageLookup, warnings);
        ValidateField("candidate.educationLevel", result.Candidate.EducationLevel, pageLookup, warnings);
        ValidateField("candidate.major", result.Candidate.Major, pageLookup, warnings);

        for (var index = 0; index < result.Educations.Count; index++)
        {
            ValidateEvidence($"educations[{index}]", result.Educations[index].Evidence, true, pageLookup, warnings);
        }

        for (var index = 0; index < result.WorkExperiences.Count; index++)
        {
            ValidateEvidence($"workExperiences[{index}]", result.WorkExperiences[index].Evidence, true, pageLookup, warnings);
        }

        for (var index = 0; index < result.Projects.Count; index++)
        {
            ValidateEvidence($"projects[{index}]", result.Projects[index].Evidence, true, pageLookup, warnings);
        }

        for (var index = 0; index < result.Skills.Count; index++)
        {
            ValidateEvidence($"skills[{index}]", result.Skills[index].Evidence, true, pageLookup, warnings);
        }

        for (var index = 0; index < result.Findings.Count; index++)
        {
            ValidateEvidence($"findings[{index}]", result.Findings[index].Evidence, true, pageLookup, warnings);
        }

        return warnings;
    }

    private static void ValidateField(
        string path,
        EvidenceBackedText field,
        IReadOnlyDictionary<int, ResumePage> pageLookup,
        ICollection<string> warnings)
    {
        ValidateEvidence(
            path,
            field.Evidence,
            !string.IsNullOrWhiteSpace(field.Value),
            pageLookup,
            warnings);
    }

    private static void ValidateEvidence(
        string path,
        IReadOnlyList<ResumeEvidence> evidenceItems,
        bool evidenceRequired,
        IReadOnlyDictionary<int, ResumePage> pageLookup,
        ICollection<string> warnings)
    {
        if (evidenceRequired && evidenceItems.Count == 0)
        {
            warnings.Add($"{path} 缺少原文证据。");
            return;
        }

        for (var index = 0; index < evidenceItems.Count; index++)
        {
            var evidence = evidenceItems[index];
            var evidencePath = $"{path}.evidence[{index}]";

            if (!pageLookup.TryGetValue(evidence.PageNumber, out var page))
            {
                warnings.Add($"{evidencePath} 引用了不存在的第 {evidence.PageNumber} 页。");
                continue;
            }

            var normalizedQuote = NormalizeWhitespace(evidence.Quote);
            if (normalizedQuote.Length == 0)
            {
                warnings.Add($"{evidencePath} 的原文引用为空。");
                continue;
            }

            var normalizedPage = NormalizeWhitespace(page.Text);
            if (!normalizedPage.Contains(normalizedQuote, StringComparison.Ordinal))
            {
                warnings.Add($"{evidencePath} 的引用无法在第 {evidence.PageNumber} 页原文中找到。");
            }
        }
    }

    private static string NormalizeWhitespace(string value) =>
        WhitespacePattern().Replace(value ?? string.Empty, " ").Trim();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
