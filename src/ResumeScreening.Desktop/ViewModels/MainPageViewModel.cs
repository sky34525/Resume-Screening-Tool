using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using ResumeScreening_Desktop.Models;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ResumeScreening_Desktop.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly HashSet<string> _importedPaths = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<ResumeItem> Resumes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsImporting { get; private set; }

    [ObservableProperty]
    public partial bool HasError { get; private set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "尚未导入简历";

    public bool HasResumes => Resumes.Count > 0;

    public bool IsEmpty => !HasResumes && !IsImporting;

    public bool IsIdle => !IsImporting;

    public string ResumeCountText => HasResumes ? $"已导入 {Resumes.Count} 份简历" : "支持 PDF、PNG、JPG、BMP、GIF 和 TIFF";

    public async Task ImportAsync(IEnumerable<string> filePaths)
    {
        var paths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (paths.Length == 0)
        {
            return;
        }

        IsImporting = true;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = "正在读取简历预览…";
        NotifyCollectionStateChanged();

        var importedCount = 0;
        var duplicateCount = 0;
        var failedFiles = new List<string>();

        foreach (var path in paths)
        {
            if (_importedPaths.Contains(path))
            {
                duplicateCount++;
                continue;
            }

            try
            {
                var item = await LoadResumeAsync(path);
                Resumes.Add(item);
                _importedPaths.Add(path);
                importedCount++;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or FileNotFoundException)
            {
                failedFiles.Add($"{Path.GetFileName(path)}（文件不可访问）");
            }
            catch
            {
                failedFiles.Add($"{Path.GetFileName(path)}（文件损坏或格式不受支持）");
            }
        }

        IsImporting = false;

        var summaryParts = new List<string>();
        if (importedCount > 0)
        {
            summaryParts.Add($"已导入 {importedCount} 份");
        }

        if (duplicateCount > 0)
        {
            summaryParts.Add($"跳过 {duplicateCount} 份重复文件");
        }

        StatusMessage = summaryParts.Count > 0 ? string.Join("，", summaryParts) : "没有导入新简历";

        if (failedFiles.Count > 0)
        {
            HasError = true;
            ErrorMessage = $"以下文件未能导入：{string.Join("、", failedFiles)}。请检查文件权限或重新导出后再试。";
        }

        NotifyCollectionStateChanged();
    }

    public void DismissError()
    {
        HasError = false;
    }

    private static async Task<ResumeItem> LoadResumeAsync(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var storageFile = await StorageFile.GetFileFromPathAsync(path);
        var preview = new BitmapImage();
        string documentInfo;
        string typeLabel;

        if (extension == ".pdf")
        {
            var pdf = await PdfDocument.LoadFromFileAsync(storageFile);
            if (pdf.PageCount == 0)
            {
                throw new InvalidDataException("PDF 中没有页面。");
            }

            using var page = pdf.GetPage(0);
            using var stream = new InMemoryRandomAccessStream();
            var renderOptions = new PdfPageRenderOptions { DestinationWidth = 560 };
            await page.RenderToStreamAsync(stream, renderOptions);
            stream.Seek(0);
            await preview.SetSourceAsync(stream);
            typeLabel = "PDF";
            documentInfo = $"{pdf.PageCount} 页 · 首页预览";
        }
        else
        {
            using var stream = await storageFile.OpenReadAsync();
            await preview.SetSourceAsync(stream);
            typeLabel = "图片";
            documentInfo = extension.TrimStart('.').ToUpperInvariant();
        }

        return new ResumeItem(
            Path.GetFileNameWithoutExtension(path),
            Path.GetFileName(path),
            path,
            typeLabel,
            documentInfo,
            DateTimeOffset.Now,
            preview);
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasResumes));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ResumeCountText));
    }
}
