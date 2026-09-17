using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ResumeScreening.Contracts;
using ResumeScreening_Desktop.Models;

namespace ResumeScreening_Desktop;

public sealed partial class MainPage
{
    private void ResumeList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ResumeItem resume)
        {
            return;
        }

        ViewModel.SelectedResume = resume;
        ResumeDetailsTitle.Text = $"{resume.CandidateName} · 审核发现";
        BuildReviewFindings(ViewModel.GetAnalysisDraft(resume));

        ResumeLibraryContent.Visibility = Visibility.Collapsed;
        ResumeDetailsContent.Visibility = Visibility.Visible;
        ShowOriginalResumePreview(resume);
    }

    private void BackToResumeList_Click(object sender, RoutedEventArgs e)
    {
        ResumePreviewImage.Source = null;
        ResumeDetailsContent.Visibility = Visibility.Collapsed;
        ResumeLibraryContent.Visibility = Visibility.Visible;
    }

    private void ShowOriginalResumePreview(ResumeItem resume)
    {
        ResumePreviewImage.Source = resume.Preview;
        ResumePreviewScrollViewer.ChangeView(null, 0, 1);
        PdfPlaceholder.Visibility = resume.Preview is null ? Visibility.Visible : Visibility.Collapsed;
        PdfPlaceholderText.Text = "没有可显示的简历预览图。";
    }

    private void BuildReviewFindings(ResumeAnalysisDraft? draft)
    {
        ReviewFindingsPanel.Children.Clear();

        if (draft is null)
        {
            ReviewFindingsPanel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Informational,
                Title = "尚无 AI 审核草稿",
                Message = "请到 AI 初筛中选择职位并添加这份简历，完成统一解析后可在此查看审核发现。"
            });
            return;
        }

        if (draft.Result.Findings.Count == 0)
        {
            ReviewFindingsPanel.Children.Add(new TextBlock
            {
                Text = "AI 未生成审核发现。",
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var finding in draft.Result.Findings)
        {
            ReviewFindingsPanel.Children.Add(BuildFindingCard(finding));
        }
    }

    private static Border BuildFindingCard(ReviewFinding finding)
    {
        var kind = finding.Kind switch
        {
            ReviewFindingKind.Strength => "事实或优势",
            ReviewFindingKind.Concern => "疑点",
            _ => "待核实"
        };

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock
        {
            Text = kind,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = finding.Conclusion,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (var evidence in finding.Evidence)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"第 {evidence.PageNumber} 页原文：{evidence.Quote}",
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = content
        };
    }
}
