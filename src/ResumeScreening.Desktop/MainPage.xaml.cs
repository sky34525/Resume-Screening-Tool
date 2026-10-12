using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ResumeScreening_Desktop.Models;
using ResumeScreening_Desktop.ViewModels;
using Windows.Storage.Pickers;

namespace ResumeScreening_Desktop;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    private readonly HashSet<ResumeItem> _checkedResumes = [];
    private bool _updatingCheckBoxes;

    public MainPage()
    {
        InitializeComponent();

        if (ShellNavigation.SettingsItem is NavigationViewItem settingsItem)
        {
            settingsItem.Content = "设置";
            AutomationProperties.SetName(settingsItem, "设置");
        }

        InitializeSettingsPage();
        InitializeWorkspacePersistence();
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private async void ImportResumes_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            CommitButtonText = "导入简历"
        };

        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);

        foreach (var extension in new[] { ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" })
        {
            picker.FileTypeFilter.Add(extension);
        }

        var files = await picker.PickMultipleFilesAsync();
        await ViewModel.ImportAsync(files);
        await SaveWorkspaceAsync();
    }

    private void ImportError_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissError();
    }

    private void SelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingCheckBoxes)
        {
            return;
        }

        var isChecked = ((CheckBox)sender).IsChecked == true;
        _updatingCheckBoxes = true;
        try
        {
            _checkedResumes.Clear();

            foreach (var item in ResumeList.Items)
            {
                if (item is not ResumeItem resume)
                {
                    continue;
                }

                var container = ResumeList.ContainerFromItem(item);
                if (container is null)
                {
                    continue;
                }

                var checkBox = FindCheckBoxInContainer(container);
                if (checkBox is not null)
                {
                    checkBox.IsChecked = isChecked;
                }

                if (isChecked)
                {
                    _checkedResumes.Add(resume);
                }
            }
        }
        finally
        {
            _updatingCheckBoxes = false;
        }

        UpdateBatchDeleteButton();
    }

    private void ResumeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingCheckBoxes)
        {
            return;
        }

        var checkBox = (CheckBox)sender;
        if (checkBox.Tag is not ResumeItem resume)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            _checkedResumes.Add(resume);
        }
        else
        {
            _checkedResumes.Remove(resume);
        }

        UpdateSelectAllCheckBox();
        UpdateBatchDeleteButton();
    }

    private async void BatchDeleteResumes_Click(object sender, RoutedEventArgs e)
    {
        if (_checkedResumes.Count == 0)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "批量删除简历",
            Content = $"确定要删除选中的 {_checkedResumes.Count} 份简历吗？\n删除后不可恢复，已关联的初筛草稿也会一并清除。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        foreach (var resume in _checkedResumes.ToArray())
        {
            foreach (var job in _screeningJobs)
            {
                job.Resumes.RemoveAll(r => ReferenceEquals(r.Resume, resume));
            }

            ViewModel.RemoveResume(resume);
            _checkedResumes.Remove(resume);
        }

        await SaveWorkspaceAsync();
        UpdateSelectAllCheckBox();
        UpdateBatchDeleteButton();
    }

    private void UpdateBatchDeleteButton()
    {
        BatchDeleteResumesButton.IsEnabled = _checkedResumes.Count > 0;
    }

    private void UpdateSelectAllCheckBox()
    {
        if (_updatingCheckBoxes)
        {
            return;
        }

        _updatingCheckBoxes = true;
        try
        {
            SelectAllCheckBox.IsChecked = ViewModel.Resumes.Count > 0
                && _checkedResumes.Count == ViewModel.Resumes.Count;
        }
        finally
        {
            _updatingCheckBoxes = false;
        }
    }

    private static CheckBox? FindCheckBoxInContainer(DependencyObject container)
    {
        var count = VisualTreeHelper.GetChildrenCount(container);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(container, i);
            if (child is CheckBox checkBox)
            {
                return checkBox;
            }

            var found = FindCheckBoxInContainer(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void ResumeSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResumeSortComboBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        var mode = tag switch
        {
            "SchoolTierDesc" => ResumeSortMode.SchoolTierDesc,
            "SchoolTierAsc" => ResumeSortMode.SchoolTierAsc,
            "NameAsc" => ResumeSortMode.NameAsc,
            _ => ResumeSortMode.Default
        };

        ViewModel.SortResumes(mode);
    }
}
