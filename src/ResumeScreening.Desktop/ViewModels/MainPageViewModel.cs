using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using ResumeScreening_Desktop.Models;
using ResumeScreening_Desktop.Services;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ResumeScreening_Desktop.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
    };

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

    public string ResumeCountText => HasResumes
        ? $"已导入 {Resumes.Count} 份简历"
        : "支持本地 PDF 和常见图片；单文件不超过 25 MB";

    public async Task ImportAsync(IEnumerable<StorageFile> storageFiles)
    {
        var files = storageFiles
            .Where(file => file is not null)
            .GroupBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (files.Length == 0)
        {
            return;
        }

        IsImporting = true;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = "正在读取本地简历并提取 PDF 文本…";
        NotifyCollectionStateChanged();

        var importedCount = 0;
        var duplicateCount = 0;
        var failedFiles = new List<string>();

        foreach (var storageFile in files)
        {
            var path = storageFile.Path;
            if (_importedPaths.Contains(path))
            {
                duplicateCount++;
                continue;
            }

            try
            {
                var item = await LoadResumeAsync(storageFile);
                Resumes.Add(item);
                _importedPaths.Add(path);
                importedCount++;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or FileNotFoundException)
            {
                failedFiles.Add($"{storageFile.Name}（文件不可访问）");
            }
            catch (InvalidDataException exception)
            {
                failedFiles.Add($"{storageFile.Name}（{exception.Message}）");
            }
            catch
            {
                failedFiles.Add($"{storageFile.Name}（文件损坏或格式不受支持）");
            }
        }

        IsImporting = false;

        var summaryParts = new List<string>();
        if (importedCount > 0)
        {
            summaryParts.Add($"已从本机导入 {importedCount} 份");
        }

        if (duplicateCount > 0)
        {
            summaryParts.Add($"跳过 {duplicateCount} 份重复文件");
        }

        StatusMessage = summaryParts.Count > 0 ? string.Join("，", summaryParts) : "没有导入新简历";

        if (failedFiles.Count > 0)
        {
            HasError = true;
            ErrorMessage = $"以下文件未能导入：{string.Join("、", failedFiles)}。请检查文件权限、PDF 密码或重新导出后再试。";
        }

        NotifyCollectionStateChanged();
    }

    public void DismissError()
    {
        HasError = false;
    }

    private static async Task<ResumeItem> LoadResumeAsync(StorageFile storageFile)
    {
        var extension = Path.GetExtension(storageFile.Name).ToLowerInvariant();
        var bytes = await ReadWithLimitAsync(storageFile);

        if (extension == ".pdf" && !HasPdfSignature(bytes))
        {
            bytes = await PythonPdfByteReader.ReadAsync(storageFile.Path, MaxFileSizeBytes);
        }

        var isPdf = extension == ".pdf" || HasPdfSignature(bytes);
        var isImage = SupportedImageExtensions.Contains(extension);

        if (!isPdf && !isImage)
        {
            throw new InvalidDataException("文件不是 PDF 或受支持的图片");
        }

        var preview = new BitmapImage();
        string documentInfo;
        string typeLabel;
        string extractedText;

        if (isPdf)
        {
            var pdf = await PdfResumeReader.ReadAsync(bytes);
            await SetBitmapSourceAsync(preview, pdf.FirstPagePng);
            typeLabel = "PDF";
            extractedText = pdf.ExtractedText;
            documentInfo = extractedText.Length > 0
                ? $"{pdf.PageCount} 页 · 已提取 {extractedText.Length:N0} 字"
                : $"{pdf.PageCount} 页 · 未检测到可复制文本";
        }
        else
        {
            await SetBitmapSourceAsync(preview, bytes);
            typeLabel = "图片";
            documentInfo = extension.TrimStart('.').ToUpperInvariant();
            extractedText = string.Empty;
        }

        return new ResumeItem(
            Path.GetFileNameWithoutExtension(storageFile.Name),
            storageFile.Name,
            storageFile.Path,
            typeLabel,
            documentInfo,
            extractedText,
            DateTimeOffset.Now,
            preview);
    }

    private static async Task<byte[]> ReadWithLimitAsync(StorageFile storageFile)
    {
        var properties = await storageFile.GetBasicPropertiesAsync();
        if (properties.Size > (ulong)MaxFileSizeBytes)
        {
            throw new InvalidDataException("文件超过 25 MB 上限");
        }

        using var stream = await storageFile.OpenReadAsync();
        if (stream.Size > (ulong)MaxFileSizeBytes)
        {
            throw new InvalidDataException("文件超过 25 MB 上限");
        }

        var length = checked((uint)stream.Size);
        var bytes = new byte[checked((int)length)];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        var loadedLength = await reader.LoadAsync(length);

        if (loadedLength != length)
        {
            throw new InvalidDataException("文件读取不完整");
        }

        reader.ReadBytes(bytes);
        return bytes;
    }

    private static async Task SetBitmapSourceAsync(BitmapImage bitmap, byte[] imageBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(imageBytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        await bitmap.SetSourceAsync(stream);
    }

    private static bool HasPdfSignature(byte[] bytes) =>
        bytes.Length >= 5
        && bytes[0] == '%'
        && bytes[1] == 'P'
        && bytes[2] == 'D'
        && bytes[3] == 'F'
        && bytes[4] == '-';

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasResumes));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ResumeCountText));
    }
}
