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
    private readonly string launcherVersion;
    private readonly LaunchCoordinator launchCoordinator;
    private readonly LauncherConfigService launcherConfigService;
    private readonly ReplayLauncherService replayLauncherService;
    private readonly SettingsService settingsService;
    private readonly LauncherSettingsProfileService settingsProfileService;

    private readonly Label gamePathLabel;
    private readonly Label detectionLabel;
    private readonly Label patchStatusLabel;
    private readonly ComboBox serverComboBox;
    private readonly ComboBox settingsProfileComboBox;
    private readonly Button launchButton;
    private readonly Button featureSettingsButton;
    private readonly Button importExportButton;
    private readonly Button moreOptionsButton;
    private readonly Button audioReplacerButton;
    private readonly Button meshReplacerButton;
    private readonly Button openReplayFolderButton;
    private readonly Button analyzeReplayButton;
    private readonly CheckBox recordReplaysCheckBox;
    private readonly CheckBox recordCustomReplaysCheckBox;
    private readonly CheckBox recordCasualReplaysCheckBox;
    private readonly CheckBox recordRankedReplaysCheckBox;
    private readonly Button textureReplacerButton;
    private readonly TextBox statusTextBox;

    private LauncherConfig? launcherConfig;
    private bool syncingReplayRecorderToggle;
    private bool syncingSettingsProfileComboBox;
    private bool shouldShowUpdatedRecommendedNotice;

    public LauncherMainForm(
        AppPaths paths,
        Logger logger,
        LauncherSettings settings,
        GameInstallInfo installInfo,
        LauncherConfig? launcherConfig,
        HttpClient httpClient)
    {
        this.paths = paths;
        this.logger = logger;
        this.settings = settings;
        this.installInfo = installInfo;
        this.launcherConfig = launcherConfig;
        launcherVersion = LauncherVersion.GetDisplayVersion();
        launchCoordinator = new LaunchCoordinator(paths, logger);
        launcherConfigService = new LauncherConfigService();
        replayLauncherService = new ReplayLauncherService(paths, logger, settings, httpClient);
        settingsService = new SettingsService(paths);
        settingsProfileService = new LauncherSettingsProfileService(paths);

        Text = $"Block N Load Community Fixes V2 - {launcherVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ClientSize = new System.Drawing.Size(760, 556);

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

        var versionLabel = new Label
        {
            Text = $"Version {launcherVersion}",
            AutoSize = false,
            Size = new System.Drawing.Size(230, 22),
            TextAlign = System.Drawing.ContentAlignment.TopRight,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Location = new System.Drawing.Point(506, 24)
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

        var settingsProfileLabel = new Label
        {
            Text = "Settings profile",
            AutoSize = true,
            Location = new System.Drawing.Point(24, 212)
        };

        settingsProfileComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(24, 230),
            Size = new System.Drawing.Size(230, 26)
        };
        settingsProfileComboBox.Items.Add("Recommended Settings");
        settingsProfileComboBox.Items.Add("Personal Settings");
        settingsProfileComboBox.SelectedIndexChanged += (_, _) => ChangeSettingsProfileFromUi();

        featureSettingsButton = new Button
        {
            Text = "Feature Settings",
            Location = new System.Drawing.Point(24, 270),
            Size = new System.Drawing.Size(120, 28)
        };
        featureSettingsButton.Click += (_, _) => OpenFeatureSettings();

        importExportButton = new Button
        {
            Text = "Import / Export...",
            Location = new System.Drawing.Point(150, 270),
            Size = new System.Drawing.Size(120, 28)
        };
        importExportButton.Click += (_, _) => OpenConfigTransfer();

        moreOptionsButton = new Button
        {
            Text = "More options...",
            Location = new System.Drawing.Point(276, 270),
            Size = new System.Drawing.Size(110, 28)
        };
        moreOptionsButton.Click += (_, _) => OpenMoreOptions();

        audioReplacerButton = new Button
        {
            Text = "Audio Replacer",
            Location = new System.Drawing.Point(392, 270),
            Size = new System.Drawing.Size(120, 28)
        };
        audioReplacerButton.Click += (_, _) => OpenAudioReplacer();

        meshReplacerButton = new Button
        {
            Text = "Mesh Replacer",
            Location = new System.Drawing.Point(518, 270),
            Size = new System.Drawing.Size(110, 28)
        };
        meshReplacerButton.Click += (_, _) => OpenMeshReplacer();

        textureReplacerButton = new Button
        {
            Text = "Texture Replacer",
            Location = new System.Drawing.Point(634, 270),
            Size = new System.Drawing.Size(102, 28)
        };
        textureReplacerButton.Click += (_, _) => OpenTextureReplacer();

        var replayLabel = new Label
        {
            Text = "Match Replays",
            AutoSize = true,
            Location = new System.Drawing.Point(24, 314),
            Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold)
        };

        openReplayFolderButton = new Button
        {
            Text = "Open Folder",
            Location = new System.Drawing.Point(24, 336),
            Size = new System.Drawing.Size(112, 28)
        };
        openReplayFolderButton.Click += (_, _) => OpenReplayFolder();

        analyzeReplayButton = new Button
        {
            Text = "Browse Replays",
            Location = new System.Drawing.Point(142, 336),
            Size = new System.Drawing.Size(112, 28)
        };
        analyzeReplayButton.Click += (_, _) => OpenReplayBrowser();

        recordReplaysCheckBox = new CheckBox
        {
            Text = "Record match replays",
            Location = new System.Drawing.Point(260, 338),
            Size = new System.Drawing.Size(160, 22)
        };
        recordReplaysCheckBox.CheckedChanged += (_, _) => ToggleReplayRecording();
        recordCustomReplaysCheckBox = new CheckBox
        {
            Text = "Custom",
            Location = new System.Drawing.Point(424, 338),
            Size = new System.Drawing.Size(76, 22)
        };
        recordCustomReplaysCheckBox.CheckedChanged += (_, _) => ToggleReplayRecording();
        recordCasualReplaysCheckBox = new CheckBox
        {
            Text = "Casual",
            Location = new System.Drawing.Point(506, 338),
            Size = new System.Drawing.Size(72, 22)
        };
        recordCasualReplaysCheckBox.CheckedChanged += (_, _) => ToggleReplayRecording();
        recordRankedReplaysCheckBox = new CheckBox
        {
            Text = "Ranked",
            Location = new System.Drawing.Point(584, 338),
            Size = new System.Drawing.Size(78, 22)
        };
        recordRankedReplaysCheckBox.CheckedChanged += (_, _) => ToggleReplayRecording();

        statusTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new System.Drawing.Point(24, 376),
            Size = new System.Drawing.Size(712, 160),
            Font = new System.Drawing.Font("Consolas", 9F)
        };

        Controls.AddRange(
        [
            titleLabel,
            versionLabel,
            gamePathLabel,
            detectionLabel,
            patchStatusLabel,
            serverLabel,
            serverComboBox,
            launchButton,
            settingsProfileLabel,
            settingsProfileComboBox,
            featureSettingsButton,
            importExportButton,
            moreOptionsButton,
            audioReplacerButton,
            meshReplacerButton,
            replayLabel,
            openReplayFolderButton,
            analyzeReplayButton,
            recordReplaysCheckBox,
            recordCustomReplaysCheckBox,
            recordCasualReplaysCheckBox,
            recordRankedReplaysCheckBox,
            textureReplacerButton,
            statusTextBox
        ]);

        shouldShowUpdatedRecommendedNotice = settingsProfileService.ApplyUpdateDefaultsIfNeeded(settings, launcherVersion, logger);
        settingsProfileService.SyncActiveSettingsToRuntime(installInfo);
        settingsService.Save(settings);
        ReloadConfig();
        Shown += (_, _) => ShowUpdatedRecommendedNoticeIfNeeded();
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
            $"Launcher version: {launcherVersion}",
            $"Manifest: {settings.ManifestUrl}",
            $"Settings profile: {(LauncherSettingsProfileService.IsPersonalProfile(settings.SettingsProfile) ? "Personal Settings" : "Recommended Settings")}",
            $"Settings file: {Path.Combine(paths.DataDir, "launcher-settings.json")}",
            $"Patching dir: {paths.PatchingDir}"
        };

        if (installInfo.IsDetected)
        {
            lines.Add($"servers.txt: {installInfo.ServersFilePath}");
            lines.Add($"Managed dir: {installInfo.ManagedDirectoryPath}");
            lines.Add($"Replay dir: {replayLauncherService.GetReplayDirectory(installInfo)}");
            lines.Add($"Latest replay: {replayLauncherService.GetLatestCapture(installInfo)?.Name ?? "none"}");
            lines.Add($"Latest replay analysis: {replayLauncherService.LatestAnalysisDirectory}");
            lines.Add($"Feature builder script: {Path.Combine(paths.PatchingDir, "Build-ExperimentalCrosshairAssembly.ps1")}");
            lines.Add($"Feature DLL present: {File.Exists(Path.Combine(paths.PatchingDir, "Assembly-CSharp.experimental.dll"))}");
            lines.Add($"Helper DLL present: {File.Exists(Path.Combine(paths.PatchingDir, "BnlCommunityFixes.dll"))}");
            lines.Add($"Texture replacements file: {Path.Combine(paths.PatchingDir, "texture-replacements.txt")}");
            var replayRecorder = LoadReplayRecorderConfig();
            lines.Add($"Replay recording: {(replayRecorder.Enabled ? $"enabled ({replayRecorder.ScopeSummary})" : "disabled")}");
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

        var replayControlsEnabled = installInfo.IsDetected;
        openReplayFolderButton.Enabled = replayControlsEnabled;
        analyzeReplayButton.Enabled = replayControlsEnabled;
        settingsProfileComboBox.Enabled = true;
        SyncSettingsProfileComboBox();
        SyncReplayRecorderToggleFromConfig();
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
        using var form = new FeatureSettingsForm(paths, installInfo);
        form.ShowDialog(this);
        settingsProfileService.SyncSelectedSnapshotFromActive(settings, logger);
        settingsService.Save(settings);
        UpdateStatusSummary();
    }

    private void OpenConfigTransfer()
    {
        using var form = new ConfigTransferForm(paths);
        form.ShowDialog(this);
        settingsProfileService.SyncSelectedSnapshotFromActive(settings, logger);
        settingsService.Save(settings);
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

    private void OpenAudioReplacer()
    {
        using var form = new AudioReplacerForm(paths, installInfo);
        form.ShowDialog(this);
    }

    private void OpenMeshReplacer()
    {
        using var form = new MeshReplacerForm(paths, installInfo);
        form.ShowDialog(this);
    }

    private void OpenTextureReplacer()
    {
        using var form = new TextureReplacerForm(paths, installInfo);
        form.ShowDialog(this);
        UpdateStatusSummary();
    }

    private void OpenReplayFolder()
    {
        if (!installInfo.IsDetected)
        {
            return;
        }

        try
        {
            replayLauncherService.OpenReplayDirectory(installInfo);
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Failed to open replay folder");
            MessageBox.Show(
                exception.Message,
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task AnalyzeLatestReplayAsync()
    {
        if (!installInfo.IsDetected)
        {
            return;
        }

        analyzeReplayButton.Enabled = false;
        analyzeReplayButton.Text = "Analyzing...";

        try
        {
            var result = await replayLauncherService.AnalyzeLatestAsync(installInfo, CancellationToken.None).ConfigureAwait(true);
            UpdateStatusSummary();
            MessageBox.Show(
                $"Analyzed replay:{Environment.NewLine}{Path.GetFileName(result.CapturePath)}{Environment.NewLine}{Environment.NewLine}Output:{Environment.NewLine}{result.OutputDirectory}",
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Replay analysis failed");
            MessageBox.Show(
                exception.Message,
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            analyzeReplayButton.Text = "Analyze Latest";
            UpdateStatusSummary();
        }
    }

    private void OpenReplayBrowser()
    {
        if (!installInfo.IsDetected)
        {
            return;
        }

        using var form = new ReplayBrowserForm(installInfo, replayLauncherService, LaunchSelectedServer);
        form.ShowDialog(this);
        UpdateStatusSummary();
    }

    private string ReplayRecorderConfigPath =>
        Path.Combine(paths.PatchingDir, "experimental-match-replay-recorder-config.json");

    private ReplayRecorderUiConfig LoadReplayRecorderConfig()
    {
        try
        {
            var configPath = ReplayRecorderConfigPath;
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                return new ReplayRecorderUiConfig(
                    GetJsonBool(root, "enabled", false),
                    GetJsonBool(root, "capture_payload", true),
                    GetJsonInt(root, "max_payload_bytes", 262144),
                    GetJsonBool(root, "record_custom_games", true),
                    GetJsonBool(root, "record_casual_games", true),
                    GetJsonBool(root, "record_ranked_games", true));
            }
        }
        catch
        {
            // ignore - default below
        }

        return new ReplayRecorderUiConfig(false, true, 262144, true, true, true);
    }

    private static bool GetJsonBool(System.Text.Json.JsonElement root, string propertyName, bool defaultValue)
    {
        return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
            ? prop.GetBoolean()
            : defaultValue;
    }

    private static int GetJsonInt(System.Text.Json.JsonElement root, string propertyName, int defaultValue)
    {
        return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.Number && prop.TryGetInt32(out var value)
            ? value
            : defaultValue;
    }

    private void SyncReplayRecorderToggleFromConfig()
    {
        syncingReplayRecorderToggle = true;
        try
        {
            var config = LoadReplayRecorderConfig();
            recordReplaysCheckBox.Checked = config.Enabled;
            recordCustomReplaysCheckBox.Checked = config.RecordCustomGames;
            recordCasualReplaysCheckBox.Checked = config.RecordCasualGames;
            recordRankedReplaysCheckBox.Checked = config.RecordRankedGames;
            recordReplaysCheckBox.Enabled = installInfo.IsDetected;
            recordCustomReplaysCheckBox.Enabled = installInfo.IsDetected && config.Enabled;
            recordCasualReplaysCheckBox.Enabled = installInfo.IsDetected && config.Enabled;
            recordRankedReplaysCheckBox.Enabled = installInfo.IsDetected && config.Enabled;
        }
        finally
        {
            syncingReplayRecorderToggle = false;
        }
    }

    private void ToggleReplayRecording()
    {
        if (syncingReplayRecorderToggle)
        {
            return;
        }

        var enabled = recordReplaysCheckBox.Checked;
        var config = System.Text.Json.JsonSerializer.Serialize(new
        {
            enabled,
            capture_payload = true,
            max_payload_bytes = 262144,
            record_custom_games = recordCustomReplaysCheckBox.Checked,
            record_casual_games = recordCasualReplaysCheckBox.Checked,
            record_ranked_games = recordRankedReplaysCheckBox.Checked
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        try
        {
            Directory.CreateDirectory(paths.PatchingDir);
            File.WriteAllText(ReplayRecorderConfigPath, config + Environment.NewLine);
            settingsProfileService.SyncSelectedSnapshotFromActive(settings, logger);
            settingsService.Save(settings);
            UpdateStatusSummary();
        }
        catch (Exception ex)
        {
            logger.Exception(ex, "Failed to write replay recorder config");
            syncingReplayRecorderToggle = true;
            try
            {
                recordReplaysCheckBox.Checked = !enabled;
            }
            finally
            {
                syncingReplayRecorderToggle = false;
            }

            MessageBox.Show(
                $"Failed to update replay recording setting.{Environment.NewLine}{ex.Message}",
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SyncSettingsProfileComboBox()
    {
        syncingSettingsProfileComboBox = true;
        try
        {
            settingsProfileComboBox.SelectedIndex =
                LauncherSettingsProfileService.IsPersonalProfile(settings.SettingsProfile) ? 1 : 0;
        }
        finally
        {
            syncingSettingsProfileComboBox = false;
        }
    }

    private void ChangeSettingsProfileFromUi()
    {
        if (syncingSettingsProfileComboBox || settingsProfileComboBox.SelectedIndex < 0)
        {
            return;
        }

        var selectedProfile = settingsProfileComboBox.SelectedIndex == 1
            ? LauncherSettings.PersonalSettingsProfile
            : LauncherSettings.RecommendedSettingsProfile;

        try
        {
            settingsProfileService.ApplySelectedProfile(settings, selectedProfile, logger);
            settingsProfileService.SyncActiveSettingsToRuntime(installInfo);
            settingsService.Save(settings);
            UpdateStatusSummary();
        }
        catch (Exception exception)
        {
            logger.Exception(exception, "Failed to apply settings profile");
            MessageBox.Show(
                $"Failed to apply settings profile.{Environment.NewLine}{exception.Message}",
                "Block N Load Community Fixes V2",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            SyncSettingsProfileComboBox();
        }
    }

    private void ShowUpdatedRecommendedNoticeIfNeeded()
    {
        if (!shouldShowUpdatedRecommendedNotice ||
            !settingsProfileService.ShouldShowUpdateNotice(settings, launcherVersion))
        {
            return;
        }

        MessageBox.Show(
            "This launcher update reset the active feature settings to Recommended Settings." +
            Environment.NewLine + Environment.NewLine +
            "Use the Settings profile dropdown on the main screen if you want to restore your Personal Settings.",
            "Recommended Settings Applied",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        settingsProfileService.DismissUpdateNotice(settings, launcherVersion);
        settingsService.Save(settings);
        shouldShowUpdatedRecommendedNotice = false;
        UpdateStatusSummary();
    }

    private sealed record ServerItem(string Key, LauncherServer Server)
    {
        public override string ToString()
        {
            return $"{Server.Name} [{Server.Host}:{Server.Port}] ({Server.Patch})";
        }
    }

    private sealed record ReplayRecorderUiConfig(
        bool Enabled,
        bool CapturePayload,
        int MaxPayloadBytes,
        bool RecordCustomGames,
        bool RecordCasualGames,
        bool RecordRankedGames)
    {
        public string ScopeSummary
        {
            get
            {
                var scopes = new List<string>();
                if (RecordCustomGames) scopes.Add("custom");
                if (RecordCasualGames) scopes.Add("casual");
                if (RecordRankedGames) scopes.Add("ranked");
                return scopes.Count == 0 ? "no match types selected" : string.Join(", ", scopes);
            }
        }
    }
}
