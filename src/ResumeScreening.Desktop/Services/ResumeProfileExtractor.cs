using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ResumeScreening_Desktop.Models;

namespace ResumeScreening_Desktop.Services;

internal static class ResumeProfileExtractor
{
    private const string Unknown = "未识别";

    private static readonly Regex AgePattern = new(
        @"(?:年龄|Age)\s*[:：]?\s*(?<value>\d{1,2})\s*岁?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BirthDatePattern = new(
        @"(?:出生日期|出生年月|生日|DOB|Date\s+of\s+Birth)\s*[:：]?\s*(?<year>19\d{2}|20\d{2})(?:[年./-](?<month>\d{1,2}))?(?:[月./-](?<day>\d{1,2}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GenderPattern = new(
        @"(?:性别|Gender)\s*[:：]?\s*(?<value>男性|女性|男|女|Male|Female)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LabeledMajorPattern = new(
        @"^(?:所学专业|专业名称|专业|主修|Major)\s*[:：]\s*(?<value>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SuffixedMajorPattern = new(
        @"(?<value>[\p{L}0-9+#.&·（）()/-]{2,30})专业(?=\s|$)",
        RegexOptions.CultureInvariant);

    private static readonly (string Pattern, string Label, int Rank)[] EducationLevels =
    [
        ("博士研究生", "博士", 6),
        ("博士", "博士", 6),
        ("硕士研究生", "硕士", 5),
        ("硕士", "硕士", 5),
        ("研究生", "研究生", 4),
        ("本科", "本科", 3),
        ("学士", "本科", 3),
        ("大专", "大专", 2),
        ("专科", "大专", 2),
        ("中专", "中专", 1),
        ("高中", "高中", 0)
    ];

    public static ResumeProfile Extract(IReadOnlyList<ResumePage> pages, string fallbackName)
    {
        var extractedText = string.Join("\n\n", pages.Select(page => page.Text));
        var normalizedText = Normalize(extractedText);
        var lines = normalizedText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CollapseWhitespace)
            .Where(line => line.Length > 0)
            .ToArray();

        var name = ExtractName(normalizedText, lines, fallbackName);
        var facts = ResumeFactExtractor.Extract(normalizedText, lines);
        return new ResumeProfile(
            name,
            facts.Age,
            facts.Gender,
            facts.Education,
            facts.School,
            facts.Major);
    }

    private static string ExtractName(string text, IReadOnlyList<string> lines, string fallbackName)
    {
        return ResumeNameExtractor.Extract(text, lines, fallbackName);
    }

    private static string ExtractAge(string text)
    {
        var age = AgePattern.Match(text);
        if (age.Success && int.TryParse(age.Groups["value"].Value, out var explicitAge) && explicitAge is >= 16 and <= 100)
        {
            return $"{explicitAge} 岁";
        }

        var birthDate = BirthDatePattern.Match(text);
        if (!birthDate.Success || !int.TryParse(birthDate.Groups["year"].Value, out var year))
        {
            return Unknown;
        }

        var month = int.TryParse(birthDate.Groups["month"].Value, out var parsedMonth) ? parsedMonth : 1;
        var day = int.TryParse(birthDate.Groups["day"].Value, out var parsedDay) ? parsedDay : 1;

        try
        {
            var birthday = new DateOnly(year, month, day);
            var today = DateOnly.FromDateTime(DateTime.Today);
            var calculatedAge = today.Year - birthday.Year;
            if (birthday > today.AddYears(-calculatedAge))
            {
                calculatedAge--;
            }

            return calculatedAge is >= 16 and <= 100 ? $"{calculatedAge} 岁" : Unknown;
        }
        catch (ArgumentOutOfRangeException)
        {
            return Unknown;
        }
    }

    private static string ExtractGender(string text, IReadOnlyList<string> lines)
    {
        var labeled = GenderPattern.Match(text);
        if (labeled.Success)
        {
            return NormalizeGender(labeled.Groups["value"].Value);
        }

        foreach (var line in lines.Take(12))
        {
            var tokens = Regex.Split(line, @"[\s|｜,，/]+")
                .Where(token => token.Length > 0);
            var token = tokens.FirstOrDefault(value => value is "男" or "女" or "男性" or "女性");
            if (token is not null)
            {
                return NormalizeGender(token);
            }
        }

        return Unknown;
    }

    private static string ExtractEducation(string text)
    {
        return EducationLevels
            .Where(level => text.Contains(level.Pattern, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(level => level.Rank)
            .Select(level => level.Label)
            .FirstOrDefault() ?? Unknown;
    }

    private static string ExtractMajor(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var labeled = LabeledMajorPattern.Match(line);
            if (labeled.Success)
            {
                return CleanMajor(labeled.Groups["value"].Value);
            }

            var suffixed = SuffixedMajorPattern.Match(line);
            if (suffixed.Success)
            {
                return CleanMajor(suffixed.Groups["value"].Value);
            }
        }

        return Unknown;
    }

    private static string CleanMajor(string value)
    {
        var withoutDates = Regex.Replace(value, @"\s+(?:19|20)\d{2}.*$", string.Empty);
        var cleaned = CleanValue(withoutDates).TrimEnd('专', '业');
        return cleaned.Length is >= 2 and <= 40 ? cleaned : Unknown;
    }

    private static string NormalizeGender(string value) =>
        value.Equals("男", StringComparison.OrdinalIgnoreCase)
        || value.Equals("男性", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Male", StringComparison.OrdinalIgnoreCase)
            ? "男"
            : "女";

    private static string Normalize(string value) =>
        (value ?? string.Empty)
        .Normalize(NormalizationForm.FormKC)
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n');

    private static string CollapseWhitespace(string value) =>
        Regex.Replace(value, @"[\t\u00A0 ]+", " ").Trim();

    private static string CleanValue(string value) =>
        CollapseWhitespace(value).Trim('：', ':', '|', '｜', ',', '，');
}
