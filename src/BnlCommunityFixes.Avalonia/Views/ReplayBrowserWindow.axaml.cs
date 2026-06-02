using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Selection;
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

        ReplayGrid.SelectionChanged += ReplayGrid_SelectionChanged;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void ReplayGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ReplayBrowserViewModel vm)
        {
            return;
        }

        vm.SetSelectedReplays(ReplayGrid.SelectedItems.Cast<ReplayBrowserViewModel.ReplayRow?>());
    }
}
