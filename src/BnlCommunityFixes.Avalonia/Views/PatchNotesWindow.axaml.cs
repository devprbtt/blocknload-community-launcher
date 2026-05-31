using Avalonia.Controls;
using Avalonia.Interactivity;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Updating;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class PatchNotesWindow : Window
{
    private readonly bool _forced;

    public PatchNotesWindow() { InitializeComponent(); }

     public PatchNotesWindow(UpdateManifest manifest, bool forcedUpdate)
    {
        InitializeComponent();
        _forced = forcedUpdate;

        TitleLabel.Text = forcedUpdate
            ? $"Required update — v{manifest.Version}"
            : $"Update available — v{manifest.Version}";

        SubtitleLabel.Text = forcedUpdate
            ? "This version is required to continue. Please install it now."
            : "A new version is ready to install. Review the changes below.";

        NotesText.Text = string.IsNullOrWhiteSpace(manifest.Notes)
            ? "(No release notes provided.)"
            : manifest.Notes.Replace("\n", Environment.NewLine);

        if (forcedUpdate)
        {
            InstallButton.Content = "Install (required)";
            SkipButton.IsEnabled = false;
        }
    }

    private void Install_Click(object? sender, RoutedEventArgs e) { _accepted = true; Close(true); }
    private void Skip_Click(object? sender, RoutedEventArgs e) => Close(false);

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Prevent closing a forced update dialog unless the install button was clicked
        if (_forced && !_accepted)
            e.Cancel = true;
        base.OnClosing(e);
    }

    private bool _accepted;

    public static async Task<bool> ShowAsync(Window owner, UpdateManifest manifest, bool forcedUpdate)
    {
        var win = new PatchNotesWindow(manifest, forcedUpdate);
        return await win.ShowDialog<bool>(owner) == true;
    }
}
