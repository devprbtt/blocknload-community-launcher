using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BnlCommunityFixes.Avalonia.ViewModels;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class ReplacerWindow : Window
{
    public ReplacerWindow() { InitializeComponent(); }

     public ReplacerWindow(ReplacerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.PickFile = async filter =>
        {
            var types = ParseFilter(filter);
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = types
            });
            return files.FirstOrDefault()?.Path.LocalPath;
        };

        vm.PickFolder = async _ =>
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
            if (!folder.Any()) return null;
            var dir = folder[0].Path.LocalPath;
            return Directory.GetFiles(dir);
        };

        vm.ErrorOccurred += (title, msg) => _ = new MessageDialog(title, msg, isError: true).ShowDialog(this);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ReplacerViewModel vm) vm.Save();
        Close();
    }

    private static FilePickerFileType[] ParseFilter(string filter)
    {
        // Simple "Description (*.ext)|*.ext" parser
        var parts = filter.Split('|');
        if (parts.Length >= 2)
        {
            var extensions = parts[1].Split(';').Select(static e => e.TrimStart('*')).ToList();
            return [new FilePickerFileType(parts[0]) { Patterns = extensions.Select(static e => "*" + e).ToList() }];
        }
        return [];
    }
}
