using System.Diagnostics;
using System.Windows.Forms;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class AdvancedOptionsForm : Form
{
    public AdvancedOptionsForm(
        AppPaths paths,
        GameInstallInfo installInfo,
        LauncherConfigService launcherConfigService,
        Action reloadConfig,
        Action manageServers,
        Action verifyFiles)
    {
        Text = "Block N Load Community Fixes V2";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new System.Drawing.Size(380, 284);

        using var iconStream = typeof(AdvancedOptionsForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new System.Drawing.Icon(iconStream);

        var gameLabel = new Label
        {
            Text = "Game",
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(14, 12)
        };

        var verifyButton = new Button
        {
            Text = "Verify Files",
            Location = new System.Drawing.Point(14, 32),
            Size = new System.Drawing.Size(166, 28),
            Enabled = installInfo.IsDetected
        };
        verifyButton.Click += (_, _) => verifyFiles();

        var refreshButton = new Button
        {
            Text = "Refresh",
            Location = new System.Drawing.Point(196, 32),
            Size = new System.Drawing.Size(166, 28)
        };
        refreshButton.Click += (_, _) => reloadConfig();

        var manageServersButton = new Button
        {
            Text = "Manage Servers",
            Location = new System.Drawing.Point(14, 66),
            Size = new System.Drawing.Size(348, 28),
            Enabled = installInfo.IsDetected
        };
        manageServersButton.Click += (_, _) => manageServers();

        var filesLabel = new Label
        {
            Text = "Open files",
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(14, 110)
        };

        var openConfigButton = new Button
        {
            Text = "config.json",
            Location = new System.Drawing.Point(14, 130),
            Size = new System.Drawing.Size(166, 28),
            Enabled = installInfo.IsDetected
        };
        openConfigButton.Click += (_, _) => OpenPath(installInfo.IsDetected
            ? launcherConfigService.GetContext(installInfo).ConfigPath
            : string.Empty);

        var openCustomButton = new Button
        {
            Text = "custom.json",
            Location = new System.Drawing.Point(196, 130),
            Size = new System.Drawing.Size(166, 28),
            Enabled = installInfo.IsDetected
        };
        openCustomButton.Click += (_, _) => OpenPath(installInfo.IsDetected
            ? launcherConfigService.GetContext(installInfo).CustomConfigPath
            : string.Empty);

        var openPatchingButton = new Button
        {
            Text = "Patching folder",
            Location = new System.Drawing.Point(14, 164),
            Size = new System.Drawing.Size(166, 28)
        };
        openPatchingButton.Click += (_, _) => OpenPath(paths.PatchingDir);

        var openSettingsButton = new Button
        {
            Text = "Launcher settings",
            Location = new System.Drawing.Point(196, 164),
            Size = new System.Drawing.Size(166, 28)
        };
        openSettingsButton.Click += (_, _) => OpenPath(Path.Combine(paths.DataDir, "launcher-settings.json"));

        var openLogButton = new Button
        {
            Text = "Launcher log",
            Location = new System.Drawing.Point(14, 198),
            Size = new System.Drawing.Size(166, 28)
        };
        openLogButton.Click += (_, _) => OpenPath(paths.LauncherLogPath);

        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.Cancel,
            Location = new System.Drawing.Point(14, 240),
            Size = new System.Drawing.Size(348, 30)
        };
        CancelButton = closeButton;

        Controls.AddRange([
            gameLabel, verifyButton, refreshButton, manageServersButton,
            filesLabel, openConfigButton, openCustomButton,
            openPatchingButton, openSettingsButton, openLogButton,
            closeButton
        ]);
    }

    private static void OpenPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show(
                "Path not available.",
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            MessageBox.Show(
                $"Path not found:{Environment.NewLine}{path}",
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            PlatformShell.OpenPath(path);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
