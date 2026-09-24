using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ResumeScreening_Desktop.Models;
using ResumeScreening_Desktop.Services;

namespace ResumeScreening_Desktop;

public sealed partial class MainPage
{
    private readonly SemaphoreSlim _workspaceSaveLock = new(1, 1);
    private bool _workspaceRestored;

    private void InitializeWorkspacePersistence()
    {
        Loaded += MainPage_WorkspaceLoaded;
    }

    private async void MainPage_WorkspaceLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainPage_WorkspaceLoaded;

        try
        {
            var workspace = await WorkspaceCacheStore.LoadAsync();
            await ViewModel.RestoreAsync(workspace.Resumes);

            var resumesById = ViewModel.Resumes
                .GroupBy(resume => resume.Id)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var cachedJob in workspace.Jobs)
            {
                if (cachedJob is null
                    || cachedJob.Id == Guid.Empty
                    || string.IsNullOrWhiteSpace(cachedJob.Title)
                    || string.IsNullOrWhiteSpace(cachedJob.Requirements))
                {
                    continue;
                }

                var job = new ScreeningJob(
                    cachedJob.Title,
                    cachedJob.Requirements,
                    cachedJob.Id,
                    cachedJob.CreatedAt);

                foreach (var cachedResume in cachedJob.Resumes ?? [])
                {
                    if (cachedResume is null
                        || cachedResume.Draft is null
                        || !resumesById.TryGetValue(cachedResume.ResumeId, out var resume))
                    {
                        continue;
                    }

                    job.Resumes.Add(new ScreenedResume(resume, cachedResume.Draft));
                    ViewModel.SetAnalysisDraft(resume, cachedResume.Draft);
                }

                _screeningJobs.Add(job);
            }

            _workspaceRestored = true;
            RenderJobCards();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException)
        {
            _workspaceRestored = true;
            ShowScreeningStatus(
                "本地缓存未能恢复",
                $"本次将使用空列表继续运行：{exception.Message}",
                InfoBarSeverity.Warning);
        }
        finally
        {
            ClearWorkspaceCacheButton.IsEnabled = true;
        }
    }

    private async Task SaveWorkspaceAsync()
    {
        if (!_workspaceRestored)
        {
            return;
        }

        await _workspaceSaveLock.WaitAsync();
        try
        {
            try
            {
                await WorkspaceCacheStore.SaveAsync(
                    ViewModel.Resumes,
                    _screeningJobs,
                    ViewModel.GetAnalysisDraft);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or System.Text.Json.JsonException)
            {
                System.Diagnostics.Debug.WriteLine($"保存工作区缓存失败：{exception}");
            }
        }
        finally
        {
            _workspaceSaveLock.Release();
        }
    }

    private async Task ClearWorkspaceAsync()
    {
        await _workspaceSaveLock.WaitAsync();
        try
        {
            WorkspaceCacheStore.Clear();
            ViewModel.ClearCachedWorkspace();
            _screeningJobs.Clear();
            _selectedJob = null;

            ResumePreviewImage.Source = null;
            ResumeDetailsContent.Visibility = Visibility.Collapsed;
            ResumeLibraryContent.Visibility = Visibility.Visible;
            ScreeningStatus.IsOpen = false;
            ShowJobs();
        }
        finally
        {
            _workspaceSaveLock.Release();
        }
    }
}
