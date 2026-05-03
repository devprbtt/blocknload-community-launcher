namespace BnlCommunityFixes.Launcher;

partial class ProgressDialog
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Label lblStatus;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.progressBar = new System.Windows.Forms.ProgressBar();
        this.lblStatus = new System.Windows.Forms.Label();
        this.SuspendLayout();
        //
        // progressBar
        //
        this.progressBar.Location = new System.Drawing.Point(12, 45);
        this.progressBar.Name = "progressBar";
        this.progressBar.Size = new System.Drawing.Size(360, 23);
        this.progressBar.TabIndex = 0;
        //
        // lblStatus
        //
        this.lblStatus.AutoSize = true;
        this.lblStatus.Location = new System.Drawing.Point(12, 20);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(81, 15);
        this.lblStatus.TabIndex = 1;
        this.lblStatus.Text = "Downloading...";
        //
        // ProgressDialog
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(384, 91);
        this.ControlBox = false;
        this.Controls.Add(this.lblStatus);
        this.Controls.Add(this.progressBar);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "ProgressDialog";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Progress";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
