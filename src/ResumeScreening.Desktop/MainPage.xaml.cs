using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ResumeScreening_Desktop.ViewModels;
using Windows.Storage.Pickers;

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
    }

    private void ImportError_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissError();
    }
}
