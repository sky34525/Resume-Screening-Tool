using System.Text.RegularExpressions;

namespace ResumeScreening_Desktop.Services;

internal static class ResumeNameExtractor
{
    private const string Unknown = "未识别";

    private static readonly Regex LabeledNamePattern = new(
        @"(?:^|\n)\s*(?:姓\s*名|Name)\s*[:：]?\s*(?<value>[\p{IsCJKUnifiedIdeographs}·]{2,6}|[A-Za-z]+(?:[ .'-][A-Za-z]+){1,4})(?=\s|$|[|｜,，])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ChineseNameAtLineStartPattern = new(
        @"^(?<value>[\p{IsCJKUnifiedIdeographs}·]{2,4})(?:\s*[（(][^）)]{1,60}[）)])?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex EnglishNamePattern = new(
        @"^[A-Za-z]+(?:[ .'-][A-Za-z]+){1,4}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ChineseNamePattern = new(
        @"^[\p{IsCJKUnifiedIdeographs}·]{2,4}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ResumeFileNamePattern = new(
        @"^(?<value>[\p{IsCJKUnifiedIdeographs}·]{2,4})(?:的)?(?:个人)?简历$",
        RegexOptions.CultureInvariant);

    private static readonly Regex PrefixedResumeFileNamePattern = new(
        @"^(?<value>[\p{IsCJKUnifiedIdeographs}·]{2,4})[-_—–].*(?:简历|求职)",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SectionHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "个人简历", "求职简历", "简历", "个人信息", "基本信息", "个人资料",
        "教育背景", "教育经历", "学习经历", "工作经历", "实习经历", "项目经历",
        "校园经历", "科研经历", "培训经历", "获奖经历", "奖项成果", "荣誉证书",
        "专业技能", "技能特长", "个人技能", "个人优势", "自我评价", "个人评价",
        "求职意向", "联系方式", "工作内容", "项目描述", "岗位职责", "岗位要求"
    };

    private static readonly string[] NoiseTerms =
    [
        "简历", "教育", "背景", "经历", "技能", "优势", "评价", "项目", "工作",
        "实习", "岗位", "职责", "要求", "问题", "描述", "内容", "专业", "学校",
        "电话", "邮箱", "联系", "求职", "意向", "工程师", "公司", "证书", "荣誉",
        "奖项", "能力", "情况", "政治", "民族", "籍贯", "文化程度"
    ];

    private static readonly string[] CompoundSurnames =
    [
        "欧阳", "太史", "端木", "上官", "司马", "东方", "独孤", "南宫", "万俟",
        "闻人", "夏侯", "诸葛", "尉迟", "公羊", "赫连", "澹台", "皇甫", "宗政",
        "濮阳", "公冶", "太叔", "申屠", "公孙", "慕容", "仲孙", "钟离", "长孙",
        "宇文", "司徒", "鲜于", "司空", "闾丘", "子车", "亓官", "司寇", "巫马",
        "公西", "颛孙", "壤驷", "公良", "漆雕", "乐正", "宰父", "谷梁", "拓跋",
        "夹谷", "轩辕", "令狐", "段干", "百里", "呼延", "东郭", "南门", "羊舌",
        "微生", "梁丘", "左丘", "东门", "西门", "第五"
    ];

    private static readonly HashSet<char> CommonSingleCharacterSurnames = new(
        "赵钱孙李周吴郑王冯陈褚卫蒋沈韩杨朱秦尤许何吕施张孔曹严华金魏陶姜戚谢邹喻柏窦章云苏潘葛奚范彭郎鲁韦昌马苗凤花方俞任袁柳鲍史唐费廉岑薛雷贺倪汤滕殷罗毕郝邬安常乐于时傅皮卞齐康伍余元卜顾孟平黄穆萧尹姚邵湛汪祁毛禹狄米贝明臧计伏成戴谈宋茅庞熊纪舒屈项祝董梁杜阮蓝闵季贾路娄江童颜郭梅盛林刁钟徐邱骆高夏蔡田樊胡凌霍虞万支柯管卢莫经房裘缪干解应宗丁宣邓郁单杭洪包诸左石崔吉龚程嵇邢滑裴陆荣翁荀羊惠甄曲家封芮储靳汲邴糜松井段富巫乌焦巴弓牧隗山谷车侯宓蓬全班仰秋仲伊宫宁仇栾甘厉戎祖武符刘景詹束龙叶幸司韶郜黎薄印宿白怀蒲从鄂索咸籍赖卓蔺屠蒙池乔胥苍双闻党翟谭贡劳姬申扶堵冉宰雍桑桂濮牛寿通边扈燕冀浦尚农温别庄晏柴瞿阎充慕连茹习宦艾鱼容向古易慎戈廖庾居衡步都耿满弘匡国文寇广东欧殳沃利蔚越隆师巩厍聂晁勾敖融冷辛阚那简饶空曾沙养鞠须丰巢关蒯相查后荆红游竺权盖益桓公".ToCharArray());

    public static string Extract(string text, IReadOnlyList<string> lines, string fallbackName)
    {
        var labeled = LabeledNamePattern.Match(text);
        if (labeled.Success)
        {
            var labeledName = Clean(labeled.Groups["value"].Value);
            if (IsPlausibleName(labeledName, requireKnownChineseSurname: false))
            {
                return labeledName;
            }
        }

        foreach (var line in lines.Take(80))
        {
            var match = ChineseNameAtLineStartPattern.Match(line);
            var candidate = match.Success ? Clean(match.Groups["value"].Value) : Clean(line);
            if (IsPlausibleName(candidate, requireKnownChineseSurname: true))
            {
                return candidate;
            }
        }

        var fileNameCandidate = ExtractFromFileName(fallbackName);
        return fileNameCandidate ?? Unknown;
    }

    private static string? ExtractFromFileName(string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(fallbackName))
        {
            return null;
        }

        var normalized = Clean(fallbackName);
        foreach (var pattern in new[] { ResumeFileNamePattern, PrefixedResumeFileNamePattern })
        {
            var match = pattern.Match(normalized);
            if (!match.Success)
            {
                continue;
            }

            var candidate = Clean(match.Groups["value"].Value);
            if (IsPlausibleName(candidate, requireKnownChineseSurname: true))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsPlausibleName(string candidate, bool requireKnownChineseSurname)
    {
        if (candidate.Length == 0
            || SectionHeadings.Contains(candidate)
            || NoiseTerms.Any(candidate.Contains))
        {
            return false;
        }

        if (EnglishNamePattern.IsMatch(candidate))
        {
            return true;
        }

        if (!ChineseNamePattern.IsMatch(candidate))
        {
            return false;
        }

        return !requireKnownChineseSurname || HasKnownChineseSurname(candidate);
    }

    private static bool HasKnownChineseSurname(string candidate)
    {
        return CompoundSurnames.Any(candidate.StartsWith)
            || CommonSingleCharacterSurnames.Contains(candidate[0]);
    }

    private static string Clean(string value) =>
        Regex.Replace(value ?? string.Empty, @"[\t\u00A0 ]+", " ")
            .Trim()
            .Trim('：', ':', '|', '｜', ',', '，');
}
