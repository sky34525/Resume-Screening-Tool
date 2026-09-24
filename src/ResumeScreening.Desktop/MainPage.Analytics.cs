using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ResumeScreening.Contracts;
using ResumeScreening_Desktop.Services;
using Windows.Foundation;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;

namespace ResumeScreening_Desktop;

public sealed partial class MainPage
{
    private readonly IAnalyticsApiClient _analyticsApiClient = new AnalyticsApiClient();

    private async void RefreshAnalytics_Click(object sender, RoutedEventArgs e) =>
        await RefreshAnalyticsAsync();

    private async Task RefreshAnalyticsAsync()
    {
        RefreshAnalyticsButton.IsEnabled = false;
        AnalyticsProgressRing.Visibility = Visibility.Visible;
        AnalyticsProgressRing.IsActive = true;
        AnalyticsStatus.IsOpen = false;

        try
        {
            var report = await _analyticsApiClient.GetReportAsync();
            RenderAnalytics(report);
        }
        catch (AnalyticsApiException exception)
        {
            AnalyticsStatus.Title = "无法读取分析报表";
            AnalyticsStatus.Message = exception.Message;
            AnalyticsStatus.Severity = InfoBarSeverity.Error;
            AnalyticsStatus.IsOpen = true;
        }
        finally
        {
            AnalyticsProgressRing.IsActive = false;
            AnalyticsProgressRing.Visibility = Visibility.Collapsed;
            RefreshAnalyticsButton.IsEnabled = true;
        }
    }

    private void RenderAnalytics(AiAnalyticsReport report)
    {
        ParsedResumeCountText.Text = FormatMetric(report.ParsedResumes);
        AiCallCountText.Text = FormatMetric(report.AiCalls);
        TotalTokenCountText.Text = FormatMetric(report.TotalTokens);
        TokenBreakdownText.Text = $"输入 {FormatMetric(report.InputTokens)} · 输出 {FormatMetric(report.OutputTokens)}";
        AnalyticsLastUpdatedText.Text = $"更新于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        var points = report.LastSevenDays ?? [];
        AnalyticsRangeText.Text = points.Count == 0
            ? "最近 7 天"
            : $"{points[0].Date:MM-dd} 至 {points[^1].Date:MM-dd}";
        AnalyticsEmptyText.Visibility = report.AiCalls == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        RenderActivityChart(points);
        RenderTokenChart(points);
    }

    private void RenderActivityChart(IReadOnlyList<AiAnalyticsDailyPoint> points)
    {
        ActivityChartPanel.Children.Clear();
        var parsedResumes = points.Sum(point => point.ParsedResumes);
        var aiCalls = points.Sum(point => point.AiCalls);
        var otherCalls = Math.Max(0, aiCalls - parsedResumes);
        ActivityChartPanel.Children.Add(CreatePieChart(
            [
                new PieSlice("成功生成草稿", parsedResumes, "AccentFillColorDefaultBrush"),
                new PieSlice("其他 AI 调用", otherCalls, "AccentFillColorSecondaryBrush")
            ],
            $"最近 7 天成功生成 {parsedResumes} 份草稿，其他 AI 调用 {otherCalls} 次"));
    }

    private void RenderTokenChart(IReadOnlyList<AiAnalyticsDailyPoint> points)
    {
        TokenChartPanel.Children.Clear();
        var inputTokens = points.Sum(point => point.InputTokens);
        var outputTokens = points.Sum(point => point.OutputTokens);
        var totalTokens = points.Sum(point => point.TotalTokens);
        var unclassifiedTokens = Math.Max(0, totalTokens - inputTokens - outputTokens);
        TokenChartPanel.Children.Add(CreatePieChart(
            [
                new PieSlice("输入 Token", inputTokens, "AccentFillColorDefaultBrush"),
                new PieSlice("输出 Token", outputTokens, "AccentFillColorSecondaryBrush"),
                new PieSlice("未拆分 Token", unclassifiedTokens, "AccentFillColorTertiaryBrush")
            ],
            $"最近 7 天输入 {inputTokens} tokens，输出 {outputTokens} tokens，未拆分 {unclassifiedTokens} tokens"));
    }

    private static Grid CreatePieChart(IReadOnlyList<PieSlice> slices, string accessibleName)
    {
        const double chartSize = 220;
        const double radius = chartSize / 2;
        var visibleSlices = slices.Where(slice => slice.Value > 0).ToArray();
        var total = visibleSlices.Sum(slice => slice.Value);
        var root = new Grid { ColumnSpacing = 24 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(chartSize) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AutomationProperties.SetName(root, accessibleName);

        var chartSurface = new Grid
        {
            Width = chartSize,
            Height = chartSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        root.Children.Add(chartSurface);

        if (total == 0)
        {
            chartSurface.Children.Add(new Ellipse
            {
                Margin = new Thickness(8),
                Stroke = GetThemeBrush("CardStrokeColorDefaultBrush"),
                StrokeThickness = 2,
                Opacity = 0.65
            });
            chartSurface.Children.Add(new TextBlock
            {
                Text = "暂无数据",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.7
            });
        }
        else
        {
            var canvas = new Canvas { Width = chartSize, Height = chartSize };
            chartSurface.Children.Add(canvas);
            var startAngle = -90d;
            foreach (var slice in visibleSlices)
            {
                var sweepAngle = slice.Value / (double)total * 360d;
                canvas.Children.Add(CreatePieSlice(
                    new Point(radius, radius),
                    radius - 4,
                    startAngle,
                    sweepAngle,
                    GetThemeBrush(slice.BrushKey)));
                startAngle += sweepAngle;
            }
        }

        var legend = new StackPanel
        {
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var slice in slices)
        {
            var percentage = total == 0 ? 0 : slice.Value / (double)total * 100;
            legend.Children.Add(CreateLegendItem(
                slice.Label,
                slice.Value,
                percentage,
                GetThemeBrush(slice.BrushKey)));
        }
        Grid.SetColumn(legend, 1);
        root.Children.Add(legend);
        return root;
    }

    private static UIElement CreatePieSlice(
        Point center,
        double radius,
        double startAngle,
        double sweepAngle,
        Brush fill)
    {
        if (sweepAngle >= 359.999)
        {
            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fill
            };
            Canvas.SetLeft(ellipse, center.X - radius);
            Canvas.SetTop(ellipse, center.Y - radius);
            return ellipse;
        }

        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, startAngle + sweepAngle);
        var figure = new PathFigure
        {
            StartPoint = center,
            IsClosed = true,
            IsFilled = true
        };
        figure.Segments.Add(new LineSegment { Point = start });
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(radius, radius),
            IsLargeArc = sweepAngle > 180,
            SweepDirection = SweepDirection.Clockwise
        });
        figure.Segments.Add(new LineSegment { Point = center });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new XamlPath { Data = geometry, Fill = fill };
    }

    private static Grid CreateLegendItem(string label, long value, double percentage, Brush brush)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var marker = new Border
        {
            Width = 12,
            Height = 12,
            Background = brush,
            CornerRadius = new CornerRadius(2),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRowSpan(marker, 2);
        grid.Children.Add(marker);

        var labelText = new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(labelText, 1);
        grid.Children.Add(labelText);

        var valueText = new TextBlock
        {
            Text = $"{FormatMetric(value)} · {percentage:0.#}%",
            FontSize = 12,
            Opacity = 0.7
        };
        Grid.SetRow(valueText, 1);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);
        return grid;
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Point(
            center.X + radius * Math.Cos(radians),
            center.Y + radius * Math.Sin(radians));
    }

    private static Brush GetThemeBrush(string resourceKey) =>
        Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            && resource is Brush brush
                ? brush
                : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private static string FormatMetric(long value) => value.ToString("N0");

    private sealed record PieSlice(string Label, long Value, string BrushKey);
}
