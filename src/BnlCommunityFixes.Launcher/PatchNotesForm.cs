using System.Drawing;
using System.Windows.Forms;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Launcher;

public sealed class PatchNotesForm : Form
{
    private readonly bool forcedUpdate;

    public PatchNotesForm(UpdateManifest manifest, bool forcedUpdate)
    {
        this.forcedUpdate = forcedUpdate;

        var title = forcedUpdate
            ? $"Required update — v{manifest.Version}"
            : $"Update available — v{manifest.Version}";

        Text = "Block N Load Community Fixes — Update";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = !forcedUpdate;
        ClientSize = new Size(560, 420);

        using var iconStream = typeof(PatchNotesForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new Icon(iconStream);

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            Location = new Point(16, 14)
        };

        var subLabel = new Label
        {
            Text = forcedUpdate
                ? "This version is required to continue. Please install it now."
                : "A new version is ready to install. Review the changes below.",
            AutoSize = false,
            Size = new Size(528, 20),
            Location = new Point(16, 44),
            ForeColor = SystemColors.GrayText
        };

        var notesBox = new RichTextBox
        {
            ReadOnly = true,
            BackColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(16, 72),
            Size = new Size(528, 282),
            Text = string.IsNullOrWhiteSpace(manifest.Notes)
                ? "(No release notes provided.)"
                : manifest.Notes.Replace("\n", Environment.NewLine),
            DetectUrls = false
        };

        var installButton = new Button
        {
            Text = forcedUpdate ? "Install (required)" : "Install now",
            DialogResult = DialogResult.Yes,
            Location = new Point(340, 374),
            Size = new Size(140, 32),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };
        installButton.FlatAppearance.BorderSize = 0;
        installButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(16, 110, 190);

        var skipButton = new Button
        {
            Text = forcedUpdate ? "Cancel" : "Skip for now",
            DialogResult = DialogResult.No,
            Size = new Size(96, 32)
        };

        // Place the skip button to the left of the install button so it fits the dialog.
        var skipX = installButton.Left - 8 - skipButton.Width;
        if (skipX < 12) skipX = 12;
        skipButton.Location = new Point(skipX, 374);

        if (forcedUpdate)
        {
            skipButton.Enabled = false;
        }

        AcceptButton = installButton;
        CancelButton = forcedUpdate ? null : skipButton;

        Controls.AddRange([titleLabel, subLabel, notesBox, installButton, skipButton]);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (forcedUpdate &&
            DialogResult != DialogResult.Yes &&
            e.CloseReason is CloseReason.UserClosing or CloseReason.None)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            var screen = Screen.FromControl(this);
            var bounds = screen.WorkingArea;
            this.Location = new System.Drawing.Point(bounds.Left + (bounds.Width - this.Width) / 2,
                                                   bounds.Top + (bounds.Height - this.Height) / 2);
        }
        catch
        {
            // ignore
        }
    }

    public static bool Show(UpdateManifest manifest, bool forcedUpdate)
    {
        using var form = new PatchNotesForm(manifest, forcedUpdate);
        return form.ShowDialog() == DialogResult.Yes;
    }
}
