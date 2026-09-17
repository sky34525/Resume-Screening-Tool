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

    private void ShellNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag?.ToString() == "screening")
        {
            ContentRoot.Visibility = Visibility.Collapsed;
            ScreeningRoot.Visibility = Visibility.Visible;
            ShowJobs();
            return;
        }

        ScreeningRoot.Visibility = Visibility.Collapsed;
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
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "创建职位信息",
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = new StackPanel { Spacing = 12, Children = { title, requirements } }
        };

        while (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (!string.IsNullOrWhiteSpace(title.Text) && !string.IsNullOrWhiteSpace(requirements.Text))
            {
                _screeningJobs.Add(new ScreeningJob(title.Text.Trim(), requirements.Text.Trim()));
                RenderJobCards();
                return;
            }

            dialog.Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new InfoBar
                    {
                        IsOpen = true,
                        IsClosable = false,
                        Severity = InfoBarSeverity.Error,
                        Message = "职位名称和招聘要求都不能为空。"
                    },
                    title,
                    requirements
                }
            };
        }
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
                        new JobScreeningRequest(job.Title, job.Requirements, resume.Pages),
                        CancellationToken.None);
                    ViewModel.SetAnalysisDraft(resume, draft);
                    job.Resumes.Add(new ScreenedResume(resume, draft));
                    addedCount++;
                    RenderGradeBoard();
                }
                catch (JobScreeningApiException exception)
                {
                    failures.Add($"{resume.FileName}：{exception.Message}");
                }
            }

            if (failures.Count == 0)
            {
                ShowScreeningStatus(
                    "初筛草稿已生成",
                    $"已根据职位要求完成 {addedCount} 份简历的 AI 评级。A/B/C 仅供人工审核，不会自动淘汰候选人。",
                    InfoBarSeverity.Success);
            }
            else if (addedCount > 0)
            {
                ShowScreeningStatus(
                    "部分简历初筛完成",
                    $"已加入 {addedCount} 份，失败 {failures.Count} 份。{string.Join("；", failures)}",
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
        var list = new ListView
        {
            MaxHeight = 560,
            SelectionMode = ListViewSelectionMode.None
        };

        if (resumes.Length == 0)
        {
            list.Items.Add(new ListViewItem
            {
                IsEnabled = false,
                Content = new TextBlock
                {
                    Text = $"当前没有 {grade} 级简历。",
                    Opacity = 0.75,
                    Margin = new Thickness(8)
                }
            });
        }

        foreach (var item in resumes)
        {
            var card = new StackPanel { Spacing = 6, MaxWidth = 700 };
            card.Children.Add(new TextBlock
            {
                Text = $"{item.Resume.CandidateName} · {item.Resume.FileName}",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            card.Children.Add(new TextBlock
            {
                Text = item.Draft.Result.Summary,
                TextWrapping = TextWrapping.Wrap
            });
            foreach (var evidence in item.Draft.Result.Evidence)
            {
                card.Children.Add(new TextBlock
                {
                    Text = $"第 {evidence.PageNumber} 页：{evidence.Quote}",
                    Opacity = 0.75,
                    TextWrapping = TextWrapping.Wrap
                });
            }
            list.Items.Add(new ListViewItem
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = card
            });
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"{_selectedJob.Title} · {grade} 级简历（{resumes.Length}）",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            MinWidth = 700,
            MaxWidth = 820,
            Content = list
        };
        await dialog.ShowAsync();
    }

    private void ShowScreeningStatus(string title, string message, InfoBarSeverity severity)
    {
        ScreeningStatus.Title = title;
        ScreeningStatus.Message = message;
        ScreeningStatus.Severity = severity;
        ScreeningStatus.IsOpen = true;
    }
}
