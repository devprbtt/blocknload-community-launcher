using Avalonia.Controls;
using Avalonia.Interactivity;
using BnlCommunityFixes.Avalonia.ViewModels;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class CustomServerManagerWindow : Window
{
    public CustomServerManagerWindow() { InitializeComponent(); }

     public CustomServerManagerWindow(CustomServerManagerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.ErrorOccurred += (t, m) => _ = new MessageDialog(t, m, isError: true).ShowDialog(this);
        vm.ConfirmDelete += async msg =>
        {
            var dlg = new ConfirmDialog(msg);
            return await dlg.ShowDialog<bool>(this);
        };
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
