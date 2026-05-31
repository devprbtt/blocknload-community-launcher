using Avalonia.Controls;
using Avalonia.Threading;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusLabel.Text = text;
            PercentLabel.Text = string.Empty;
            Bar.Value = Bar.Maximum;
        });
    }

    public void SetDownloadProgress(string label, double fraction)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusLabel.Text = label;
            PercentLabel.Text = $"{fraction * 100:F0}%";
            Bar.Value = Math.Clamp((int)(fraction * 1000), 0, 1000);
        });
    }
}
