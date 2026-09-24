using Microsoft.UI.Xaml.Media;
using ResumeScreening_Desktop.Models;
using ResumeScreening_Desktop.Services;
using Windows.Storage;

namespace ResumeScreening_Desktop.ViewModels;

public partial class MainPageViewModel
{
    internal async Task RestoreAsync(IEnumerable<CachedResume> cachedResumes)
    {
        ArgumentNullException.ThrowIfNull(cachedResumes);

        foreach (var cachedResume in cachedResumes)
        {
            if (cachedResume is null
                || cachedResume.Profile is null
                || cachedResume.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(cachedResume.FilePath)
                || _importedPaths.Contains(cachedResume.FilePath))
            {
                continue;
            }

            ImageSource? preview = null;
            if (File.Exists(cachedResume.FilePath))
            {
                try
                {
                    var storageFile = await StorageFile.GetFileFromPathAsync(cachedResume.FilePath);
                    preview = (await LoadResumeAsync(storageFile)).Preview;
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException
                        and not OutOfMemoryException)
                {
                    preview = null;
                }
            }

            var item = new ResumeItem(
                cachedResume.Profile,
                cachedResume.FileName,
                cachedResume.FilePath,
                cachedResume.TypeLabel,
                cachedResume.DocumentInfo,
                cachedResume.Pages ?? [],
                cachedResume.ImportedAt,
                preview,
                cachedResume.Id);

            Resumes.Add(item);
            _importedPaths.Add(item.FilePath);
            if (cachedResume.AnalysisDraft is not null)
            {
                _analysisDrafts[item] = cachedResume.AnalysisDraft;
            }
        }

        StatusMessage = Resumes.Count > 0
            ? $"已从本地缓存恢复 {Resumes.Count} 份简历"
            : "尚未导入简历";
        NotifyCollectionStateChanged();
    }

    internal void ClearCachedWorkspace()
    {
        SelectedResume = null;
        _analysisDrafts.Clear();
        _importedPaths.Clear();
        Resumes.Clear();
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = "尚未导入简历";
        NotifyCollectionStateChanged();
    }
}
