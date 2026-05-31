using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BnlCommunityFixes.Avalonia.ViewModels;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class ConfigTransferWindow : Window
{
    public ConfigTransferWindow() { InitializeComponent(); }

     public ConfigTransferWindow(ConfigTransferViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.PickSaveFile = async () =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export config bundle",
                SuggestedFileName = "bnl-config-bundle.zip",
                FileTypeChoices = [new FilePickerFileType("Config bundle") { Patterns = ["*.zip"] }]
            });
            return file?.Path.LocalPath;
        };

        vm.PickOpenFile = async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import config bundle",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Config bundle") { Patterns = ["*.zip"] }]
            });
            return files.FirstOrDefault()?.Path.LocalPath;
        };

        vm.Notify += (title, msg) => _ = new MessageDialog(title, msg).ShowDialog(this);
        vm.ErrorOccurred += (title, msg) => _ = new MessageDialog(title, msg, isError: true).ShowDialog(this);
        vm.Confirm += async msg => await new ConfirmDialog(msg).ShowDialog<bool>(this);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
