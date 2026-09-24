using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ResumeScreening.Contracts;
using ResumeScreening_Desktop.Services;

namespace ResumeScreening_Desktop;

public sealed partial class MainPage
{
    private readonly IAiSettingsApiClient _aiSettingsApiClient = new AiSettingsApiClient();
    private bool _isInitializingTheme;
    private bool _isInitializingWeights;
    private ScreeningWeights _screeningWeights = ScreeningWeights.Default;

    private void InitializeSettingsPage()
    {
        _isInitializingTheme = true;
        var themeName = ThemePreferenceStore.Load();
        ThemeOptions.SelectedIndex = themeName switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
        ApplyTheme(themeName);
        _isInitializingTheme = false;

        LoadScreeningWeights();

        Loaded += MainPage_Loaded;
    }

    private void LoadScreeningWeights()
    {
        _isInitializingWeights = true;
        _screeningWeights = ScreeningWeightPreferenceStore.Load();
        EducationWeightBox.Value = _screeningWeights.Education;
        GenderWeightBox.Value = _screeningWeights.Gender;
        AgeWeightBox.Value = _screeningWeights.Age;
        WorkExperienceWeightBox.Value = _screeningWeights.WorkExperience;
        MajorMatchWeightBox.Value = _screeningWeights.MajorMatch;
        _isInitializingWeights = false;
        UpdateScreeningWeightTotal();
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainPage_Loaded;
        await RefreshAiSettingsStatusAsync();
    }

    private async Task RefreshAiSettingsStatusAsync()
    {
        try
        {
            var status = await _aiSettingsApiClient.GetStatusAsync();
            ApiKeyStateText.Text = status.Configured
                ? "已缓存 API Key。保存新值会覆盖原有密钥。"
                : "尚未配置 API Key。";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or AiSettingsApiException)
        {
            ApiKeyStateText.Text = "暂时无法读取后台配置状态，请确认 API 项目正在运行。";
        }
    }

    private async void SaveAiSettings_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = DeepSeekApiKeyBox.Password.Trim();
        if (apiKey.Length == 0)
        {
            ShowSettingsStatus(
                InfoBarSeverity.Error,
                "无法保存",
                "请输入 DeepSeek API Key。");
            DeepSeekApiKeyBox.Focus(FocusState.Programmatic);
            return;
        }

        SaveAiSettingsButton.IsEnabled = false;
        SettingsProgressRing.Visibility = Visibility.Visible;
        SettingsProgressRing.IsActive = true;
        ShowSettingsStatus(
            InfoBarSeverity.Informational,
            "正在保存",
            "正在将 API Key 写入本机加密缓存。");

        try
        {
            var status = await _aiSettingsApiClient.SaveApiKeyAsync(apiKey);
            DeepSeekApiKeyBox.Password = string.Empty;
            ApiKeyStateText.Text = status.Configured
                ? "已缓存 API Key。保存新值会覆盖原有密钥。"
                : "尚未配置 API Key。";
            ShowSettingsStatus(
                InfoBarSeverity.Success,
                "保存成功",
                "DeepSeek API Key 已加密缓存，重启后仍然有效。");
        }
        catch (HttpRequestException)
        {
            ShowSettingsStatus(
                InfoBarSeverity.Error,
                "保存失败",
                "无法连接本机 API，请先启动 ResumeScreening.Api。");
        }
        catch (TaskCanceledException)
        {
            ShowSettingsStatus(
                InfoBarSeverity.Error,
                "保存失败",
                "本机 API 响应超时，请稍后重试。");
        }
        catch (AiSettingsApiException exception)
        {
            ShowSettingsStatus(InfoBarSeverity.Error, "保存失败", exception.Message);
        }
        finally
        {
            SettingsProgressRing.IsActive = false;
            SettingsProgressRing.Visibility = Visibility.Collapsed;
            SaveAiSettingsButton.IsEnabled = true;
        }
    }

    private void ThemeOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeOptions.SelectedItem is not RadioButton selectedTheme
            || selectedTheme.Tag is not string themeName)
        {
            return;
        }

        ApplyTheme(themeName);
        if (!_isInitializingTheme)
        {
            ThemePreferenceStore.Save(themeName);
        }
    }

    private void ScreeningWeight_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isInitializingWeights)
        {
            UpdateScreeningWeightTotal();
        }
    }

    private void SaveScreeningWeights_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadWeight(EducationWeightBox, "学历", out var education)
            || !TryReadWeight(GenderWeightBox, "性别", out var gender)
            || !TryReadWeight(AgeWeightBox, "年龄", out var age)
            || !TryReadWeight(WorkExperienceWeightBox, "工作经验", out var workExperience)
            || !TryReadWeight(MajorMatchWeightBox, "专业对口", out var majorMatch))
        {
            return;
        }

        var weights = new ScreeningWeights(
            education,
            gender,
            age,
            workExperience,
            majorMatch);
        if (!weights.IsValid)
        {
            ShowSettingsStatus(
                InfoBarSeverity.Error,
                "无法保存筛选权重",
                $"五项权重当前合计为 {weights.Total}%，必须恰好为 100%。");
            return;
        }

        try
        {
            ScreeningWeightPreferenceStore.Save(weights);
            _screeningWeights = weights;
            ShowSettingsStatus(
                InfoBarSeverity.Success,
                "筛选权重已保存",
                "新权重会用于之后生成的 AI 初筛草稿；已有评级不会自动改变。");
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException)
        {
            ShowSettingsStatus(
                InfoBarSeverity.Error,
                "无法保存筛选权重",
                "本机配置文件写入失败，请检查当前 Windows 用户的目录权限。");
        }
    }

    private async void ClearWorkspaceCache_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsImporting || !AddResumeToJobButton.IsEnabled)
        {
            ShowSettingsStatus(
                InfoBarSeverity.Warning,
                "暂时无法清除缓存",
                "请等待当前简历导入或 AI 初筛完成后再试。");
            return;
        }

        var resumeCount = ViewModel.Resumes.Count;
        var jobCount = _screeningJobs.Count;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "清除简历和职位缓存？",
            PrimaryButtonText = "清除缓存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            Content = new TextBlock
            {
                Text = $"将清除 {resumeCount} 份简历、{jobCount} 个职位及其 AI 审核草稿和 A/B/C 评级。原始 PDF/图片、DeepSeek API Key、界面颜色和筛选权重不会被删除。此操作无法撤销。",
                TextWrapping = TextWrapping.Wrap
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ClearWorkspaceCacheButton.IsEnabled = false;
        try
        {
            await ClearWorkspaceAsync();
            ShowSettingsStatus(
                InfoBarSeverity.Success,
                "缓存已清除",
                $"已清除 {resumeCount} 份简历和 {jobCount} 个职位；原始文件和其他设置保持不变。");
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException)
        {
            ShowSettingsStatus(
                InfoBarSeverity.Error,
                "清除失败",
                "无法删除本地缓存文件，请检查当前 Windows 用户目录权限后重试。");
        }
        finally
        {
            ClearWorkspaceCacheButton.IsEnabled = true;
        }
    }

    private bool TryReadWeight(NumberBox numberBox, string label, out int value)
    {
        value = 0;
        var rawValue = numberBox.Value;
        if (double.IsNaN(rawValue)
            || double.IsInfinity(rawValue)
            || rawValue < 0
            || rawValue > 100
            || rawValue != Math.Truncate(rawValue))
        {
            ShowSettingsStatus(
                InfoBarSeverity.Error,
                "筛选权重格式错误",
                $"{label}权重必须是 0 到 100 之间的整数。");
            numberBox.Focus(FocusState.Programmatic);
            return false;
        }

        value = (int)rawValue;
        return true;
    }

    private void UpdateScreeningWeightTotal()
    {
        var boxes = new[]
        {
            EducationWeightBox,
            GenderWeightBox,
            AgeWeightBox,
            WorkExperienceWeightBox,
            MajorMatchWeightBox
        };
        var valuesAreValid = boxes.All(box =>
            !double.IsNaN(box.Value)
            && !double.IsInfinity(box.Value)
            && box.Value is >= 0 and <= 100);
        var total = valuesAreValid ? boxes.Sum(box => box.Value) : double.NaN;
        ScreeningWeightTotalText.Text = double.IsNaN(total)
            ? "当前合计：输入无效（必须为 100%）"
            : $"当前合计：{total:0.##}%（必须为 100%）";
    }

    private void ApplyTheme(string themeName)
    {
        RequestedTheme = themeName switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void ShowSettingsStatus(InfoBarSeverity severity, string title, string message)
    {
        SettingsStatus.Severity = severity;
        SettingsStatus.Title = title;
        SettingsStatus.Message = message;
        SettingsStatus.IsOpen = true;
    }

    private static class ThemePreferenceStore
    {
        private static readonly string PreferencePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ResumeScreening",
            "ui-theme.txt");

        public static string Load()
        {
            try
            {
                return File.Exists(PreferencePath)
                    ? File.ReadAllText(PreferencePath).Trim()
                    : "Default";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return "Default";
            }
        }

        public static void Save(string themeName)
        {
            try
            {
                var directory = Path.GetDirectoryName(PreferencePath);
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(PreferencePath, themeName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Theme switching still works for the current process if persistence fails.
            }
        }
    }
}
