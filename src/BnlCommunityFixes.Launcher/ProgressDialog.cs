using System.Windows.Forms;

namespace BnlCommunityFixes.Launcher;

public partial class ProgressDialog : Form
{
    public ProgressDialog(string title)
    {
        InitializeComponent();
        this.Text = title;
    }

    public void SetProgress(int percent, string status)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                Invoke(new Action(() => SetProgress(percent, status)));
            }
            catch (ObjectDisposedException)
            {
                // Silently ignore if disposed between check and invoke
            }
            return;
        }

        progressBar.Value = Math.Clamp(percent, 0, 100);
        lblStatus.Text = status;
    }
}
