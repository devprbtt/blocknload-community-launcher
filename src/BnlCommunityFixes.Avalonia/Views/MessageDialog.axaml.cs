using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class MessageDialog : Window
{
    public MessageDialog() { InitializeComponent(); }

     public MessageDialog(string title, string message, bool isError = false)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        if (isError) MessageText.Foreground = Brushes.DarkRed;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close();
}
