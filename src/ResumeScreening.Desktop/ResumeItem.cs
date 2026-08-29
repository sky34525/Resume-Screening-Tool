using Microsoft.UI.Xaml.Media;

namespace ResumeScreening_Desktop.Models;

public sealed class ResumeItem(
    string candidateName,
    string fileName,
    string filePath,
    string typeLabel,
    string documentInfo,
    DateTimeOffset importedAt,
    ImageSource preview)
{
    public string CandidateName { get; } = candidateName;

    public string FileName { get; } = fileName;

    public string FilePath { get; } = filePath;

    public string TypeLabel { get; } = typeLabel;

    public string DocumentInfo { get; } = documentInfo;

    public string ImportedAtText { get; } = $"导入于 {importedAt:MM-dd HH:mm}";

    public string AccessibleName { get; } = $"候选人 {candidateName}，{typeLabel} 简历，{documentInfo}";

    public ImageSource Preview { get; } = preview;
}
