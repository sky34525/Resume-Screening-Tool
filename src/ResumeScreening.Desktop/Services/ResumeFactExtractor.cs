using System.Globalization;
using System.Text.RegularExpressions;

namespace ResumeScreening_Desktop.Services;

internal sealed record ResumeFacts(
    string Age,
    string Gender,
    string Education,
    string School,
    string Major);

internal static class ResumeFactExtractor
{
    private const string Unknown = "未识别";

    private static readonly Regex LabeledAgePattern = new(
        @"(?:年\s*龄|Age)\s*[:：]?\s*(?<value>\d{1,2})\s*岁?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex StandaloneAgePattern = new(
        @"(?<!\d)(?<value>1[6-9]|[2-9]\d|100)\s*岁",
        RegexOptions.CultureInvariant);

    private static readonly Regex BirthDatePattern = new(
        @"(?:出生\s*日期|出生\s*年月|生日|DOB|Date\s+of\s+Birth)\s*[:：]?\s*(?<year>19\d{2}|20\d{2})(?:[年./-](?<month>\d{1,2}))?(?:[月./-](?<day>\d{1,2}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BirthYearPattern = new(
        @"(?<!\d)(?<year>19\d{2}|20\d{2})\s*年\s*(?:生|出生)",
        RegexOptions.CultureInvariant);

    private static readonly Regex LabeledGenderPattern = new(
        @"(?:性\s*别|Gender)\s*[:：]?\s*(?<value>男性|女性|男|女|Male|Female)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LabeledInstitutionPattern = new(
        @"(?:毕业\s*学校|毕业\s*院校|学校|院校)\s*[:：]?\s*(?<value>[\p{IsCJKUnifiedIdeographs}A-Za-z·]{2,24}(?:大学|学院))",
        RegexOptions.CultureInvariant);

    private static readonly Regex InstitutionPattern = new(
        @"(?<value>[\p{IsCJKUnifiedIdeographs}A-Za-z·]{2,24}(?:大学|学院))",
        RegexOptions.CultureInvariant);

    private static readonly Regex LabeledMajorPattern = new(
        @"(?:^|[\s|｜,，])(?:所\s*学\s*专\s*业|专\s*业\s*名\s*称|专\s*业|主\s*修|Major)\s*[:：]\s*(?<value>[\p{IsCJKUnifiedIdeographs}A-Za-z0-9+#.&·（）()/-]{2,40})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SuffixedMajorPattern = new(
        @"(?<value>[\p{IsCJKUnifiedIdeographs}A-Za-z0-9+#.&·（）()/-]{2,40})专业(?=\s|[（(]|$)",
        RegexOptions.CultureInvariant);

    private static readonly Regex MajorAfterDegreePattern = new(
        @"(?:博士研究生|博士|硕士研究生|硕士|研究生|本科|大专|专科)\s*[-—–:：]?\s*(?<value>[\p{IsCJKUnifiedIdeographs}A-Za-z0-9+#.&·/-]{2,40})(?=\s|GPA|[（(]|(?:19|20)\d{2}|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MajorBeforeDegreePattern = new(
        @"(?<value>[\p{IsCJKUnifiedIdeographs}A-Za-z0-9+#.&·/-]{2,40})\s*[（(](?:博士研究生|博士|硕士研究生|硕士|研究生|本科|大专|专科)",
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

    private static readonly HashSet<string> InvalidMajors = new(StringComparer.OrdinalIgnoreCase)
    {
        "相关", "相关专业", "专业", "工科", "理工科", "不限", "无",
        "博士研究生", "博士", "硕士研究生", "硕士", "研究生", "本科", "学士", "大专", "专科"
    };

    public static ResumeFacts Extract(string text, IReadOnlyList<string> lines)
    {
        return new ResumeFacts(
            ExtractAge(text),
            ExtractGender(text, lines),
            ExtractEducation(text),
            ExtractSchool(lines),
            ExtractMajor(lines));
    }

    private static string ExtractAge(string text)
    {
        var labeledAge = LabeledAgePattern.Match(text);
        if (TryFormatExplicitAge(labeledAge, out var explicitAge))
        {
            return explicitAge;
        }

        var standaloneAge = StandaloneAgePattern.Match(text);
        if (TryFormatExplicitAge(standaloneAge, out explicitAge))
        {
            return explicitAge;
        }

        var birthDate = BirthDatePattern.Match(text);
        if (birthDate.Success
            && int.TryParse(birthDate.Groups["year"].Value, out var birthYear))
        {
            var month = int.TryParse(birthDate.Groups["month"].Value, out var parsedMonth)
                ? parsedMonth
                : 1;
            var day = int.TryParse(birthDate.Groups["day"].Value, out var parsedDay)
                ? parsedDay
                : 1;
            return CalculateAge(birthYear, month, day, approximate: false);
        }

        var birthYearOnly = BirthYearPattern.Match(text);
        if (birthYearOnly.Success
            && int.TryParse(birthYearOnly.Groups["year"].Value, out birthYear))
        {
            return CalculateAge(birthYear, 1, 1, approximate: true);
        }

        return Unknown;
    }

    private static bool TryFormatExplicitAge(Match match, out string age)
    {
        if (match.Success
            && int.TryParse(match.Groups["value"].Value, out var value)
            && value is >= 16 and <= 100)
        {
            age = $"{value} 岁";
            return true;
        }

        age = Unknown;
        return false;
    }

    private static string CalculateAge(int year, int month, int day, bool approximate)
    {
        try
        {
            var birthday = new DateOnly(year, month, day);
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - birthday.Year;
            if (!approximate && birthday > today.AddYears(-age))
            {
                age--;
            }

            if (age is < 16 or > 100)
            {
                return Unknown;
            }

            return approximate ? $"约 {age} 岁" : $"{age} 岁";
        }
        catch (ArgumentOutOfRangeException)
        {
            return Unknown;
        }
    }

    private static string ExtractGender(string text, IReadOnlyList<string> lines)
    {
        var labeled = LabeledGenderPattern.Match(text);
        if (labeled.Success)
        {
            return NormalizeGender(labeled.Groups["value"].Value);
        }

        foreach (var line in lines.Take(100))
        {
            var tokens = Regex.Split(line, @"[\s|｜,，/·;；—–-]+")
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

    private static string ExtractSchool(IReadOnlyList<string> lines)
    {
        var candidates = new List<(string Value, int Rank, bool Labeled, int Index)>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var context = index + 1 < lines.Count ? $"{line} {lines[index + 1]}" : line;
            var rank = GetEducationRank(context);
            var labeled = LabeledInstitutionPattern.Match(line);
            if (labeled.Success)
            {
                AddSchoolCandidate(candidates, labeled.Groups["value"].Value, rank, true, index);
            }

            foreach (Match match in InstitutionPattern.Matches(line))
            {
                AddSchoolCandidate(candidates, match.Groups["value"].Value, rank, false, index);
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Rank)
            .ThenByDescending(candidate => candidate.Labeled)
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => candidate.Value)
            .FirstOrDefault() ?? Unknown;
    }

    private static void AddSchoolCandidate(
        ICollection<(string Value, int Rank, bool Labeled, int Index)> candidates,
        string value,
        int rank,
        bool labeled,
        int index)
    {
        var cleaned = Regex.Replace(
            value,
            @"(?<=[\p{IsCJKUnifiedIdeographs}])[A-Za-z](?=[\p{IsCJKUnifiedIdeographs}])",
            string.Empty);
        cleaned = CollapseWhitespace(cleaned);
        if (cleaned.Length is >= 4 and <= 30)
        {
            candidates.Add((cleaned, rank, labeled, index));
        }
    }

    private static string ExtractMajor(IReadOnlyList<string> lines)
    {
        var candidates = new List<(string Value, int Score, int Index)>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            AddMajorCandidate(candidates, LabeledMajorPattern.Match(line), 400, index);
            AddMajorCandidate(candidates, MajorBeforeDegreePattern.Match(line), 300 + GetEducationRank(line), index);
            AddMajorCandidate(candidates, MajorAfterDegreePattern.Match(line), 250 + GetEducationRank(line), index);
            AddMajorCandidate(candidates, SuffixedMajorPattern.Match(line), 200, index);
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => candidate.Value)
            .FirstOrDefault() ?? Unknown;
    }

    private static void AddMajorCandidate(
        ICollection<(string Value, int Score, int Index)> candidates,
        Match match,
        int score,
        int index)
    {
        if (!match.Success)
        {
            return;
        }

        var cleaned = CleanMajor(match.Groups["value"].Value);
        if (cleaned != Unknown)
        {
            candidates.Add((cleaned, score, index));
        }
    }

    private static string CleanMajor(string value)
    {
        var cleaned = CollapseWhitespace(value)
            .Trim('：', ':', '|', '｜', ',', '，', '-', '—', '–')
            .TrimEnd('专', '业');
        cleaned = Regex.Replace(
            cleaned,
            @"(?<=[\p{IsCJKUnifiedIdeographs}])[A-Za-z]{1,3}$",
            string.Empty);

        return cleaned.Length is >= 2 and <= 40 && !InvalidMajors.Contains(cleaned)
            ? cleaned
            : Unknown;
    }

    private static int GetEducationRank(string text)
    {
        return EducationLevels
            .Where(level => text.Contains(level.Pattern, StringComparison.OrdinalIgnoreCase))
            .Select(level => level.Rank)
            .DefaultIfEmpty(-1)
            .Max();
    }

    private static string NormalizeGender(string value) =>
        value.Equals("男", StringComparison.OrdinalIgnoreCase)
        || value.Equals("男性", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Male", StringComparison.OrdinalIgnoreCase)
            ? "男"
            : "女";

    private static string CollapseWhitespace(string value) =>
        Regex.Replace(value ?? string.Empty, @"[\t\u00A0 ]+", " ").Trim();
}
