using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog() { InitializeComponent(); }

     public ConfirmDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void Yes_Click(object? sender, RoutedEventArgs e) => Close(true);
    private void No_Click(object? sender, RoutedEventArgs e) => Close(false);
}
