using System.Windows.Forms;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class LauncherMainForm : Form
{
    private readonly AppPaths paths;
    private readonly Logger logger;
    private readonly LauncherSettings settings;
    private readonly GameInstallInfo installInfo;
    private readonly LaunchCoordinator launchCoordinator;
    private readonly LauncherConfigService launcherConfigService;

    private readonly Label gamePathLabel;
    private readonly Label detectionLabel;
    private readonly Label patchStatusLabel;
    private readonly ComboBox serverComboBox;
    private readonly Button launchButton;
    private readonly Button featureSettingsButton;
    private readonly Button moreOptionsButton;
    private readonly TextBox statusTextBox;

    private LauncherConfig? launcherConfig;

    public LauncherMainForm(
        AppPaths paths,
        Logger logger,
        LauncherSettings settings,
        GameInstallInfo installInfo,
        LauncherConfig? launcherConfig)
    {
        this.paths = paths;
        this.logger = logger;
        this.settings = settings;
        this.installInfo = installInfo;
        this.launcherConfig = launcherConfig;
        launchCoordinator = new LaunchCoordinator(paths, logger);
        launcherConfigService = new LauncherConfigService();

        Text = "Block N Load Community Fixes V2";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ClientSize = new System.Drawing.Size(760, 430);

        using var appIconStream = typeof(LauncherMainForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (appIconStream != null) Icon = new System.Drawing.Icon(appIconStream);

        System.Drawing.Image? launchIcon = null;
        using var gameIconStream = typeof(LauncherMainForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.game-icon.ico");
        if (gameIconStream != null)
            launchIcon = new System.Drawing.Bitmap(new System.Drawing.Icon(gameIconStream).ToBitmap(), new System.Drawing.Size(22, 22));

        var titleLabel = new Label
        {
            Text = "Block N Load Community Fixes V2",
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI Semibold", 16F, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(20, 18)
        };

        gamePathLabel = new Label
        {
            AutoSize = false,
            Size = new System.Drawing.Size(716, 36),
            Location = new System.Drawing.Point(24, 58)
        };

        detectionLabel = new Label
        {
            AutoSize = false,
            Size = new System.Drawing.Size(716, 20),
            Location = new System.Drawing.Point(24, 96)
        };

        patchStatusLabel = new Label
        {
            AutoSize = false,
            Size = new System.Drawing.Size(716, 36),
            Location = new System.Drawing.Point(24, 110),
            Text = "Base patching and feature rebuild/deploy are available. Press F8 in-game for the settings menu."
        };

        var serverLabel = new Label
        {
            Text = "Server",
            AutoSize = true,
            Location = new System.Drawing.Point(24, 150)
        };

        serverComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(24, 168),
            Size = new System.Drawing.Size(502, 26)
        };
        serverComboBox.SelectedIndexChanged += (_, _) => UpdateStatusSummary();

        // Primary action — prominent, blue, with icon
        launchButton = new Button
        {
            Text = "  Launch Game",
            Location = new System.Drawing.Point(536, 156),
            Size = new System.Drawing.Size(200, 46),
            BackColor = System.Drawing.Color.FromArgb(0, 120, 215),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
            Image = launchIcon,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = System.Drawing.ContentAlignment.MiddleLeft,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        launchButton.FlatAppearance.BorderSize = 0;
        launchButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(16, 110, 190);
        launchButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(0, 90, 158);
        launchButton.Click += (_, _) => LaunchSelectedServer();
        AcceptButton = launchButton;

        featureSettingsButton = new Button
        {
            Text = "Feature Settings",
            Location = new System.Drawing.Point(24, 212),
            Size = new System.Drawing.Size(120, 28)
        };
        featureSettingsButton.Click += (_, _) => OpenFeatureSettings();

        moreOptionsButton = new Button
        {
            Text = "More options...",
            Location = new System.Drawing.Point(150, 212),
            Size = new System.Drawing.Size(110, 28)
        };
        moreOptionsButton.Click += (_, _) => OpenMoreOptions();

        statusTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new System.Drawing.Point(24, 255),
            Size = new System.Drawing.Size(712, 153),
            Font = new System.Drawing.Font("Consolas", 9F)
        };

        Controls.AddRange(
        [
            titleLabel,
            gamePathLabel,
            detectionLabel,
            patchStatusLabel,
            serverLabel,
            serverComboBox,
            launchButton,
            featureSettingsButton,
            moreOptionsButton,
            statusTextBox
        ]);

        ReloadConfig();
    }

    private void ReloadConfig()
    {
        try
        {
            if (installInfo.IsDetected)
            {
                launcherConfig = launcherConfigService.LoadOrCreate(installInfo, logger);
            }

            PopulateServerList();
            UpdateStatusSummary();
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Failed to reload launcher config");
            MessageBox.Show(
                exception.Message,
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void PopulateServerList()
    {
        serverComboBox.Items.Clear();

        if (launcherConfig is null)
        {
            serverComboBox.Enabled = false;
            launchButton.Enabled = false;
            return;
        }

        foreach (var serverEntry in launcherConfig.Servers.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            serverComboBox.Items.Add(new ServerItem(serverEntry.Key, serverEntry.Value));
        }

        serverComboBox.Enabled = serverComboBox.Items.Count > 0;
        launchButton.Enabled = installInfo.IsDetected && serverComboBox.Items.Count > 0;

        if (!string.IsNullOrWhiteSpace(launcherConfig.SelectedServer))
        {
            for (var i = 0; i < serverComboBox.Items.Count; i++)
            {
                if (serverComboBox.Items[i] is ServerItem item &&
                    string.Equals(item.Key, launcherConfig.SelectedServer, StringComparison.OrdinalIgnoreCase))
                {
                    serverComboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        if (serverComboBox.Items.Count > 0)
        {
            serverComboBox.SelectedIndex = 0;
        }
    }

    private void UpdateStatusSummary()
    {
        gamePathLabel.Text = installInfo.IsDetected
            ? $"Game path: {installInfo.GameRoot}"
            : "Game path: not detected";

        detectionLabel.Text = installInfo.IsDetected
            ? $"Detection: {installInfo.DetectionSource}"
            : $"Detection failed: {installInfo.FailureReason}";

        var lines = new List<string>
        {
            $"Manifest: {settings.ManifestUrl}",
            $"Settings file: {Path.Combine(paths.DataDir, "launcher-settings.json")}",
            $"Patching dir: {paths.PatchingDir}"
        };

        if (installInfo.IsDetected)
        {
            lines.Add($"servers.txt: {installInfo.ServersFilePath}");
            lines.Add($"Managed dir: {installInfo.ManagedDirectoryPath}");
            lines.Add($"Feature builder script: {Path.Combine(paths.PatchingDir, "Build-ExperimentalCrosshairAssembly.ps1")}");
            lines.Add($"Feature DLL present: {File.Exists(Path.Combine(paths.PatchingDir, "Assembly-CSharp.experimental.dll"))}");
            lines.Add($"Helper DLL present: {File.Exists(Path.Combine(paths.PatchingDir, "BnlCommunityFixes.dll"))}");
        }

        if (serverComboBox.SelectedItem is ServerItem selectedItem)
        {
            lines.Add($"Selected server: {selectedItem.Key}");
            lines.Add($"Target: {selectedItem.Server.Host}:{selectedItem.Server.Port}");
            lines.Add($"Patch: {selectedItem.Server.Patch}");
        }
        else
        {
            lines.Add("Selected server: none");
        }

        statusTextBox.Text = string.Join(Environment.NewLine, lines);
    }

    private void LaunchSelectedServer()
    {
        if (!installInfo.IsDetected || launcherConfig is null)
        {
            return;
        }

        if (serverComboBox.SelectedItem is not ServerItem item)
        {
            MessageBox.Show(
                "No server selected.",
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            launcherConfig.SelectedServer = item.Key;
            launchCoordinator.LaunchSelectedServer(installInfo, launcherConfig);
            Close();
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Launch failed");
            MessageBox.Show(
                exception.Message,
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ManageServers()
    {
        if (!installInfo.IsDetected)
        {
            return;
        }

        using var form = new CustomServerManagerForm(logger, installInfo, launcherConfigService);
        form.ShowDialog(this);
        ReloadConfig();
    }

    private void OpenFeatureSettings()
    {
        using var form = new FeatureSettingsForm(paths);
        form.ShowDialog(this);
        UpdateStatusSummary();
    }

    private void VerifyGameFiles()
    {
        try
        {
            launchCoordinator.VerifyGameFiles();
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Steam verification failed");
            MessageBox.Show(
                exception.Message,
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenMoreOptions()
    {
        using var form = new AdvancedOptionsForm(
            paths,
            installInfo,
            launcherConfigService,
            ReloadConfig,
            ManageServers,
            VerifyGameFiles);
        form.ShowDialog(this);
    }

    private sealed record ServerItem(string Key, LauncherServer Server)
    {
        public override string ToString()
        {
            return $"{Server.Name} [{Server.Host}:{Server.Port}] ({Server.Patch})";
        }
    }
}
