using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using ResumeScreening_Desktop.ViewModels;

namespace ResumeScreening_Desktop;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private async void ImportResumes_Click(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var picker = new FileOpenPicker(element.XamlRoot.ContentIslandEnvironment.AppWindowId)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            CommitButtonText = "导入简历"
        };

        foreach (var extension in new[] { ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" })
        {
            picker.FileTypeFilter.Add(extension);
        }

        var results = await picker.PickMultipleFilesAsync();
        await ViewModel.ImportAsync(results.Select(result => result.Path));
    }

    private void ImportError_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissError();
    }
}
