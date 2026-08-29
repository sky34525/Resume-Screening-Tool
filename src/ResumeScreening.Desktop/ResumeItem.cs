using Microsoft.UI.Xaml.Media;

namespace ResumeScreening_Desktop.Models;

/// <summary>
/// 一份简历对应的所有数据
/// </summary>
public sealed class ResumeItem(
    ResumeProfile profile,
    string fileName,
    string filePath,
    string typeLabel,
    string documentInfo,
    IReadOnlyList<ResumePage> pages,
    DateTimeOffset importedAt,
    ImageSource preview)
{
    public string CandidateName { get; } = profile.Name;

    public string Age { get; } = profile.Age;

    public string Gender { get; } = profile.Gender;

    public string Education { get; } = profile.Education;

    public string Major { get; } = profile.Major;

    public string FileName { get; } = fileName;

    public string FilePath { get; } = filePath;

    public string TypeLabel { get; } = typeLabel;

    public string DocumentInfo { get; } = documentInfo;

    public IReadOnlyList<ResumePage> Pages { get; } = pages.ToArray();

    public string ImportedAtText { get; } = $"导入于 {importedAt:MM-dd HH:mm}";

    public string AccessibleName { get; } =
        $"候选人 {profile.Name}，年龄 {profile.Age}，性别 {profile.Gender}，学历 {profile.Education}，专业 {profile.Major}";

    public ImageSource Preview { get; } = preview;
}
