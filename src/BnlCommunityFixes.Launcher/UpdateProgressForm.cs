using System.Drawing;
using System.Windows.Forms;

namespace BnlCommunityFixes.Launcher;

public sealed class UpdateProgressForm : Form
{
    private readonly Label statusLabel;
    private readonly Label percentLabel;
    private readonly ProgressBar progressBar;

    public UpdateProgressForm()
    {
        Text = "BNL Community Fixes — Updating";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        ClientSize = new Size(420, 96);

        using var iconStream = typeof(UpdateProgressForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new Icon(iconStream);

        var titleLabel = new Label
        {
            Text = "Installing update…",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Location = new Point(16, 12)
        };

        statusLabel = new Label
        {
            Text = "Preparing…",
            AutoSize = false,
            Size = new Size(340, 18),
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 9F),
            Location = new Point(16, 34)
        };

        percentLabel = new Label
        {
            Text = "",
            AutoSize = false,
            Size = new Size(60, 18),
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 9F),
            Location = new Point(344, 34)
        };

        progressBar = new ProgressBar
        {
            Location = new Point(16, 58),
            Size = new Size(388, 20),
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 1000
        };

        Controls.AddRange([titleLabel, statusLabel, percentLabel, progressBar]);
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

    public void SetStatus(string text)
    {
        statusLabel.Text = text;
        percentLabel.Text = "";
        progressBar.Value = progressBar.Maximum;
    }

    public void SetDownloadProgress(string label, double fraction)
    {
        statusLabel.Text = label;
        percentLabel.Text = $"{fraction * 100:F0}%";
        progressBar.Value = Math.Clamp((int)(fraction * 1000), 0, 1000);
    }
}
