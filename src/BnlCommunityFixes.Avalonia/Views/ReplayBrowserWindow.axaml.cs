using Avalonia.Controls;
using Avalonia.Interactivity;
using BnlCommunityFixes.Avalonia.ViewModels;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class ReplayBrowserWindow : Window
{
    public ReplayBrowserWindow() { InitializeComponent(); }

     public ReplayBrowserWindow(ReplayBrowserViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.ErrorOccurred += (t, m) => _ = new MessageDialog(t, m, isError: true).ShowDialog(this);
        vm.ConfirmDelete += async msg => await new ConfirmDialog(msg).ShowDialog<bool>(this);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
