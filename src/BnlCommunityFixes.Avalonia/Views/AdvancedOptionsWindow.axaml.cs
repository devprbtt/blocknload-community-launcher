using Avalonia.Controls;
using Avalonia.Interactivity;
using BnlCommunityFixes.Avalonia.ViewModels;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class AdvancedOptionsWindow : Window
{
    public AdvancedOptionsWindow() { InitializeComponent(); }

     public AdvancedOptionsWindow(AdvancedOptionsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.OpenPathRequested += OnOpenPathRequested;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is AdvancedOptionsViewModel vm)
            vm.OpenPathRequested -= OnOpenPathRequested;
        base.OnClosed(e);
    }

    private void OnOpenPathRequested(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            _ = new MessageDialog("Not found", $"Path not found:\n{path}").ShowDialog(this);
            return;
        }

        try { PlatformShell.OpenPath(path); }
        catch (Exception ex) { _ = new MessageDialog("Error", ex.Message, isError: true).ShowDialog(this); }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
