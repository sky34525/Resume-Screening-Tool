using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using ResumeScreening.Contracts;
using ResumeScreening_Desktop.Models;
using ResumeScreening_Desktop.Services;

namespace ResumeScreening_Desktop;

public sealed partial class MainPage
{
    private readonly List<ScreeningJob> _screeningJobs = [];
    private readonly IJobScreeningApiClient _jobScreeningApiClient = new JobScreeningApiClient();
    private ScreeningJob? _selectedJob;

    private async void ShellNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag?.ToString() == "screening")
        {
            ContentRoot.Visibility = Visibility.Collapsed;
            SettingsRoot.Visibility = Visibility.Collapsed;
            AnalyticsRoot.Visibility = Visibility.Collapsed;
            ScreeningRoot.Visibility = Visibility.Visible;
            ShowJobs();
            return;
        }

        if (args.SelectedItemContainer?.Tag?.ToString() == "analytics")
        {
            ContentRoot.Visibility = Visibility.Collapsed;
            ScreeningRoot.Visibility = Visibility.Collapsed;
            SettingsRoot.Visibility = Visibility.Collapsed;
            AnalyticsRoot.Visibility = Visibility.Visible;
            await RefreshAnalyticsAsync();
            return;
        }

        if (args.IsSettingsSelected)
        {
            ContentRoot.Visibility = Visibility.Collapsed;
            ScreeningRoot.Visibility = Visibility.Collapsed;
            AnalyticsRoot.Visibility = Visibility.Collapsed;
            SettingsRoot.Visibility = Visibility.Visible;
            return;
        }

        ScreeningRoot.Visibility = Visibility.Collapsed;
        SettingsRoot.Visibility = Visibility.Collapsed;
        AnalyticsRoot.Visibility = Visibility.Collapsed;
        ContentRoot.Visibility = Visibility.Visible;
        ResumeDetailsContent.Visibility = Visibility.Collapsed;
        ResumeLibraryContent.Visibility = Visibility.Visible;
    }

    private async void CreateJob_Click(object sender, RoutedEventArgs e)
    {
        var title = new TextBox { Header = "职位名称", MaxLength = 100 };
        var requirements = new TextBox
        {
            Header = "招聘要求",
            AcceptsReturn = true,
            MaxLength = 10_000,
            MinHeight = 180,
            TextWrapping = TextWrapping.Wrap
        };
        var validation = new InfoBar
        {
            IsOpen = false,
            IsClosable = false,
            Severity = InfoBarSeverity.Error,
            Title = "信息不完整",
            Message = "职位名称和招聘要求都不能为空。"
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "创建职位信息",
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel
            {
                Spacing = 12,
                Children = { validation, title, requirements }
            }
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            var titleIsEmpty = string.IsNullOrWhiteSpace(title.Text);
            var requirementsAreEmpty = string.IsNullOrWhiteSpace(requirements.Text);
            if (!titleIsEmpty && !requirementsAreEmpty)
            {
                return;
            }

            args.Cancel = true;
            validation.Message = (titleIsEmpty, requirementsAreEmpty) switch
            {
                (true, true) => "请输入职位名称和招聘要求。",
                (true, false) => "请输入职位名称。",
                _ => "请输入招聘要求。"
            };
            validation.IsOpen = true;

            if (titleIsEmpty)
            {
                title.Focus(FocusState.Programmatic);
            }
            else
            {
                requirements.Focus(FocusState.Programmatic);
            }
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _screeningJobs.Add(new ScreeningJob(title.Text.Trim(), requirements.Text.Trim()));
            RenderJobCards();
            await SaveWorkspaceAsync();
        }
    }

    private async void BatchDeleteJobs_Click(object sender, RoutedEventArgs e)
    {
        if (_screeningJobs.Count == 0)
        {
            ShowScreeningStatus("没有可删除的职位", "请先创建职位信息。", InfoBarSeverity.Informational);
            return;
        }

        var stackPanel = new StackPanel { Spacing = 8 };
        var checkBoxes = new List<CheckBox>();
        foreach (var job in _screeningJobs)
        {
            var checkBox = new CheckBox
            {
                Content = $"{job.Title}（{job.Resumes.Count} 位候选人）",
                Tag = job
            };
            checkBoxes.Add(checkBox);
            stackPanel.Children.Add(checkBox);
        }

        var selectAllCheckBox = new CheckBox
        {
            Content = "全选 / 取消全选",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        selectAllCheckBox.Checked += (_, _) =>
        {
            foreach (var cb in checkBoxes) cb.IsChecked = true;
        };
        selectAllCheckBox.Unchecked += (_, _) =>
        {
            foreach (var cb in checkBoxes) cb.IsChecked = false;
        };

        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 400,
            Content = new StackPanel { Spacing = 12, Children = { selectAllCheckBox, stackPanel } }
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "批量删除职位",
            Content = scrollViewer,
            PrimaryButtonText = "删除选中",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var selectedJobs = checkBoxes
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (ScreeningJob)cb.Tag)
            .ToArray();

        if (selectedJobs.Length == 0)
        {
            ShowScreeningStatus("未选择职位", "请至少勾选一个职位。", InfoBarSeverity.Warning);
            return;
        }

        var confirmDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "确认删除",
            Content = $"确定要删除选中的 {selectedJobs.Length} 个职位及其所有初筛草稿吗？删除后不可恢复。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        foreach (var job in selectedJobs)
        {
            _screeningJobs.Remove(job);
            if (ReferenceEquals(_selectedJob, job))
            {
                _selectedJob = null;
            }
        }

        // 如果有正在查看的职位被删除，返回到职位列表
        if (_selectedJob is null || !_screeningJobs.Contains(_selectedJob))
        {
            ShowJobs();
        }
        else
        {
            RenderJobCards();
        }

        await SaveWorkspaceAsync();
    }

    private void RenderJobCards()
    {
        JobCardsList.Items.Clear();

        foreach (var job in _screeningJobs)
        {
            var button = new Button
            {
                Width = 204,
                Height = 204,
                Margin = new Thickness(0, 0, 12, 12),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Tag = job,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE821", FontSize = 28 },
                        new TextBlock
                        {
                            Text = job.Title,
                            FontSize = 20,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = job.Requirements,
                            MaxLines = 4,
                            Opacity = 0.75,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock { Text = $"{job.Resumes.Count} 位候选人", Opacity = 0.75 }
                    }
                }
            };
            AutomationProperties.SetName(button, $"打开职位 {job.Title}");
            button.Click += JobCard_Click;
            JobCardsList.Items.Add(button);
        }
    }

    private void JobCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScreeningJob job }) ShowJob(job);
    }

    private void ShowJobs()
    {
        _selectedJob = null;
        ScreeningTitle.Text = "AI 初筛";
        ScreeningSubtitle.Text = "按职位要求整理候选人，A/B/C 均为人工审核草稿，不自动淘汰。";
        CreateJobButton.Visibility = Visibility.Visible;
        BatchDeleteJobsButton.Visibility = Visibility.Visible;
        BackToJobsButton.Visibility = Visibility.Collapsed;
        AddResumeToJobButton.Visibility = Visibility.Collapsed;
        GradeBoard.Visibility = Visibility.Collapsed;
        JobCardsList.Visibility = Visibility.Visible;
        RenderJobCards();
    }

    private void ShowJob(ScreeningJob job)
    {
        _selectedJob = job;
        ScreeningTitle.Text = job.Title;
        ScreeningSubtitle.Text = job.Requirements;
        CreateJobButton.Visibility = Visibility.Collapsed;
        BatchDeleteJobsButton.Visibility = Visibility.Collapsed;
        BackToJobsButton.Visibility = Visibility.Visible;
        AddResumeToJobButton.Visibility = Visibility.Visible;
        JobCardsList.Visibility = Visibility.Collapsed;
        GradeBoard.Visibility = Visibility.Visible;
        RenderGradeBoard();
    }

    private void BackToJobs_Click(object sender, RoutedEventArgs e) => ShowJobs();

    private async void AddResumeToJob_Click(object sender, RoutedEventArgs e)
    {
        var job = _selectedJob;
        if (job is null) return;

        var candidates = ViewModel.Resumes
            .Where(item => job.Resumes.All(existing => !ReferenceEquals(existing.Resume, item)))
            .ToArray();
        if (candidates.Length == 0)
        {
            ShowScreeningStatus("没有可添加的简历", "请先在简历库导入简历，或该职位已包含全部简历。", InfoBarSeverity.Warning);
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 420
        };
        foreach (var resume in candidates)
        {
            list.Items.Add(new ListViewItem { Content = $"{resume.CandidateName} · {resume.FileName}", Tag = resume });
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "从简历库添加到初筛",
            PrimaryButtonText = "添加并评级",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = list
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var selected = list.SelectedItems
            .OfType<ListViewItem>()
            .Select(item => (ResumeItem)item.Tag)
            .ToArray();
        if (selected.Length == 0)
        {
            ShowScreeningStatus("尚未选择简历", "请至少选择一份简历。", InfoBarSeverity.Warning);
            return;
        }

        AddResumeToJobButton.IsEnabled = false;
        var addedCount = 0;
        var reviewWarningCount = 0;
        var failures = new List<string>();
        try
        {
            foreach (var resume in selected)
            {
                try
                {
                    if (resume.Pages.Count == 0
                        || resume.Pages.All(page => string.IsNullOrWhiteSpace(page.Text)))
                    {
                        failures.Add($"{resume.FileName}：未提取到可解析文字；扫描件或图片需要先完成 OCR。");
                        continue;
                    }

                    ShowScreeningStatus(
                        "AI 初筛中",
                        $"正在一次请求中解析 {resume.FileName} 并根据 {job.Title} 的要求评级…",
                        InfoBarSeverity.Informational);
                    var draft = await _jobScreeningApiClient.ScreenAsync(
                        new JobScreeningRequest(
                            job.Title,
                            job.Requirements,
                            resume.Pages,
                            _screeningWeights),
                        CancellationToken.None);
                    ViewModel.SetAnalysisDraft(resume, draft);
                    job.Resumes.Add(new ScreenedResume(resume, draft));
                    await SaveWorkspaceAsync();
                    addedCount++;
                    reviewWarningCount += draft.ValidationWarnings.Count;
                    RenderGradeBoard();
                }
                catch (JobScreeningApiException exception)
                {
                    failures.Add($"{resume.FileName}：{exception.Message}");
                }
            }

            if (failures.Count == 0)
            {
                var warningText = reviewWarningCount == 0
                    ? "证据校验未发现问题。"
                    : $"其中 {reviewWarningCount} 条证据无法自动核对，已保留为人工复核提醒。";
                ShowScreeningStatus(
                    "初筛草稿已生成",
                    $"已根据职位要求完成 {addedCount} 份简历的 AI 评级。{warningText}A/B/C 仅供人工审核，不会自动淘汰候选人。",
                    reviewWarningCount == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            }
            else if (addedCount > 0)
            {
                ShowScreeningStatus(
                    "部分简历初筛完成",
                    $"已加入 {addedCount} 份，失败 {failures.Count} 份，另有 {reviewWarningCount} 条证据需人工核实。{string.Join("；", failures)}",
                    InfoBarSeverity.Warning);
            }
            else
            {
                ShowScreeningStatus(
                    "AI 初筛失败",
                    $"没有简历被加入评级。{string.Join("；", failures)}",
                    InfoBarSeverity.Error);
            }
        }
        finally
        {
            AddResumeToJobButton.IsEnabled = true;
        }
    }

    private void RenderGradeBoard()
    {
        RenderGradeColumn(GradeAPanel, ScreeningGrade.A, "A", "关键要求有充分证据");
        RenderGradeColumn(GradeBPanel, ScreeningGrade.B, "B", "部分匹配，需人工核实");
        RenderGradeColumn(GradeCPanel, ScreeningGrade.C, "C", "证据不足，需重点审核");
    }

    private void RenderGradeColumn(StackPanel panel, ScreeningGrade grade, string title, string description)
    {
        panel.Children.Clear();
        panel.Spacing = 10;
        var count = _selectedJob?.Resumes.Count(item => item.Draft.Result.Grade == grade) ?? 0;
        panel.Children.Add(new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                new TextBlock { Text = title, FontSize = 34, FontWeight = Microsoft.UI.Text.FontWeights.Bold },
                new TextBlock { Text = description, Opacity = 0.75, TextWrapping = TextWrapping.Wrap },
                new TextBlock
                {
                    Text = $"{count} 份简历",
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 18, 0, 0)
                },
                new TextBlock { Text = "点击查看列表", Opacity = 0.75 }
            }
        });
    }

    private async void GradeModule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string gradeText }
            || !Enum.TryParse<ScreeningGrade>(gradeText, out var grade)
            || _selectedJob is null)
        {
            return;
        }

        var resumes = _selectedJob.Resumes
            .Where(item => item.Draft.Result.Grade == grade)
            .ToArray();
        var table = new Grid
        {
            MinWidth = 920,
            MaxWidth = 1080
        };
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerTemplate = (DataTemplate)Resources["ResumeTableHeaderTemplate"];
        var header = (UIElement)headerTemplate.LoadContent();
        table.Children.Add(header);

        ScreenedResume? selectedResume = null;
        ContentDialog? dialog = null;

        if (resumes.Length == 0)
        {
            var emptyState = new TextBlock
            {
                Text = $"当前没有 {grade} 级简历。",
                Opacity = 0.75,
                Margin = new Thickness(16, 24, 16, 24),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(emptyState, 1);
            table.Children.Add(emptyState);
        }
        else
        {
            var itemContainerStyle = new Style(typeof(ListViewItem));
            itemContainerStyle.Setters.Add(new Setter(
                ListViewItem.HorizontalContentAlignmentProperty,
                HorizontalAlignment.Stretch));
            itemContainerStyle.Setters.Add(new Setter(
                ListViewItem.MinHeightProperty,
                52d));
            itemContainerStyle.Setters.Add(new Setter(
                ListViewItem.PaddingProperty,
                new Thickness(0)));

            var list = new ListView
            {
                MaxHeight = 560,
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = resumes,
                ItemTemplate = (DataTemplate)Resources["ScreenedResumeTableRowTemplate"],
                ItemContainerStyle = itemContainerStyle,
                TabNavigation = Microsoft.UI.Xaml.Input.KeyboardNavigationMode.Once
            };
            AutomationProperties.SetName(list, $"{grade} 级候选人简历列表");
            list.ItemClick += (_, args) =>
            {
                if (args.ClickedItem is ScreenedResume item)
                {
                    selectedResume = item;
                    dialog?.Hide();
                }
            };
            Grid.SetRow(list, 1);
            table.Children.Add(list);
        }

        dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"{_selectedJob.Title} · {grade} 级简历（{resumes.Length}）",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            MinWidth = 960,
            MaxWidth = 1120,
            Content = table
        };
        await dialog.ShowAsync();

        if (selectedResume is not null)
        {
            ShellNavigation.SelectedItem = ResumeLibraryItem;
            ScreeningRoot.Visibility = Visibility.Collapsed;
            ContentRoot.Visibility = Visibility.Visible;
            ShowResumeDetails(selectedResume.Resume);
        }
    }

    private void ShowScreeningStatus(string title, string message, InfoBarSeverity severity)
    {
        ScreeningStatus.Title = title;
        ScreeningStatus.Message = message;
        ScreeningStatus.Severity = severity;
        ScreeningStatus.IsOpen = true;
    }
}
