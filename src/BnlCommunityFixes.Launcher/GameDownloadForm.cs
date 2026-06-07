using System.IO.Compression;
using System.Windows.Forms;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class GameDownloadForm : Form
{
    private const string GameDownloadUrlZip = "https://prbtthome.loan/local/BlockNLoad/BlockNLoad.zip";
    private const string MegaMirror1 = "https://mega.nz/file/Dqw3mb5B#T4h8zbLuBkaLWu0wEvHXjjfpvsYQAmb8ERRlmTXE-EI";
    private const string MegaMirror2 = "https://mega.nz/file/rjA2VYjR#MuDUPb5wQk1RuwUanJg8_gEakM2dmy5NZlkIcjLm_Rg";
    private const string DepsDownloadUrl = "https://prbtthome.loan/local/BlockNLoad/BlockNLoad%20Dependencies.zip";
    private const string DefaultInstallPath = @"C:\Games\BlockNLoad";

    private readonly AppPaths paths;
    private readonly Logger logger;
    private readonly LauncherSettings settings;
    private readonly SettingsService settingsService;
    private readonly HttpClient httpClient;
    private readonly NoSteamInstallService noSteamInstallService;

    private readonly TextBox installPathTextBox;
    private readonly Button browseButton;
    private readonly CheckBox depsCheckBox;
    private readonly Button downloadButton;
    private readonly Button alreadyHaveButton;
    private readonly ProgressBar progressBar;
    private readonly Label statusLabel;
    private readonly Button cancelButton;

    private CancellationTokenSource? cts;

    public GameInstallInfo? ResultInstallInfo { get; private set; }

    public GameDownloadForm(
        AppPaths paths,
        Logger logger,
        LauncherSettings settings,
        SettingsService settingsService,
        HttpClient httpClient)
    {
        this.paths = paths;
        this.logger = logger;
        this.settings = settings;
        this.settingsService = settingsService;
        this.httpClient = httpClient;
        noSteamInstallService = new NoSteamInstallService(typeof(GameDownloadForm).Assembly);

        Text = "Block N Load — Game Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new System.Drawing.Size(520, 310);

        using var iconStream = typeof(GameDownloadForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new System.Drawing.Icon(iconStream);

        // ── Header ────────────────────────────────────────────────────────────
        var headerLabel = new Label
        {
            Text = "Block N Load was not found on this PC.",
            Font = new System.Drawing.Font(Font.FontFamily, 11f, System.Drawing.FontStyle.Bold),
            AutoSize = true,
            Location = new System.Drawing.Point(16, 16)
        };

        var subLabel = new Label
        {
            Text = "Download the game below, or point the launcher to an existing installation.",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 42)
        };

        // ── Install path ──────────────────────────────────────────────────────
        var pathLabel = new Label
        {
            Text = "Install folder:",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 78)
        };

        installPathTextBox = new TextBox
        {
            Text = DefaultInstallPath,
            Location = new System.Drawing.Point(16, 96),
            Width = 390
        };

        browseButton = new Button
        {
            Text = "Browse...",
            Location = new System.Drawing.Point(414, 94),
            Width = 90
        };
        browseButton.Click += BrowseButton_Click;

        // ── Options ───────────────────────────────────────────────────────────
        depsCheckBox = new CheckBox
        {
            Text = "Also download audio dependencies (required if you have no sound in-game)",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 130)
        };

        // ── MEGA mirror links ─────────────────────────────────────────────────
        var mirrorLabel = new Label
        {
            Text = "Manual download mirrors (if the download below fails):",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 158)
        };

        var mirror1Link = new LinkLabel
        {
            Text = "MEGA mirror — ZIP",
            AutoSize = true,
            Location = new System.Drawing.Point(16, 176)
        };
        mirror1Link.LinkClicked += (_, _) => OpenUrl(MegaMirror1);

        var mirror2Link = new LinkLabel
        {
            Text = "MEGA mirror — 7z",
            AutoSize = true,
            Location = new System.Drawing.Point(130, 176)
        };
        mirror2Link.LinkClicked += (_, _) => OpenUrl(MegaMirror2);

        // ── Progress area ─────────────────────────────────────────────────────
        progressBar = new ProgressBar
        {
            Location = new System.Drawing.Point(16, 206),
            Width = 488,
            Height = 20,
            Visible = false
        };

        statusLabel = new Label
        {
            AutoSize = true,
            Location = new System.Drawing.Point(16, 232),
            Text = "",
            Visible = false
        };

        // ── Buttons ───────────────────────────────────────────────────────────
        downloadButton = new Button
        {
            Text = "Download && Install",
            Location = new System.Drawing.Point(16, 268),
            Width = 160,
            Height = 30
        };
        downloadButton.Click += DownloadButton_Click;

        alreadyHaveButton = new Button
        {
            Text = "I already have the game files",
            Location = new System.Drawing.Point(188, 268),
            Width = 190,
            Height = 30
        };
        alreadyHaveButton.Click += AlreadyHaveButton_Click;

        cancelButton = new Button
        {
            Text = "Cancel",
            Location = new System.Drawing.Point(430, 268),
            Width = 74,
            Height = 30,
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange([
            headerLabel, subLabel, pathLabel, installPathTextBox, browseButton,
            depsCheckBox, mirrorLabel, mirror1Link, mirror2Link,
            progressBar, statusLabel,
            downloadButton, alreadyHaveButton, cancelButton
        ]);

        CancelButton = cancelButton;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder where Block N Load will be installed",
            UseDescriptionForTitle = true,
            SelectedPath = installPathTextBox.Text
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            installPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void DownloadButton_Click(object? sender, EventArgs e)
    {
        var installPath = installPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(installPath))
        {
            MessageBox.Show("Please choose an installation folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetControlsEnabled(false);
        progressBar.Visible = true;
        statusLabel.Visible = true;
        cts = new CancellationTokenSource();

        try
        {
            Directory.CreateDirectory(installPath);

            var tempZip = Path.Combine(Path.GetTempPath(), "BlockNLoad_download.zip");

            await DownloadWithProgressAsync(GameDownloadUrlZip, tempZip, "Downloading game... {0}%", cts.Token);

            SetStatus("Extracting game files...", -1);
            await Task.Run(() => ExtractZip(tempZip, installPath), cts.Token);
            TryDelete(tempZip);

            if (depsCheckBox.Checked)
            {
                var tempDeps = Path.Combine(Path.GetTempPath(), "BlockNLoad_deps.zip");
                await DownloadWithProgressAsync(DepsDownloadUrl, tempDeps, "Downloading audio dependencies... {0}%", cts.Token);
                SetStatus("Extracting audio dependencies...", -1);
                await Task.Run(() => ExtractZip(tempDeps, installPath), cts.Token);
                TryDelete(tempDeps);
            }

            SetStatus("Applying no-Steam fix files...", -1);
            noSteamInstallService.ApplyFixFiles(installPath);

            FinishSetup(installPath);
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "Download cancelled.";
            SetControlsEnabled(true);
        }
        catch (Exception ex)
        {
            logger.Exception(ex, "Game download/install failed");
            MessageBox.Show(
                $"Download failed: {ex.Message}\n\nYou can use the MEGA mirror links to download manually, then use \"I already have the game files\".",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetControlsEnabled(true);
        }
        finally
        {
            cts?.Dispose();
            cts = null;
        }
    }

    private void AlreadyHaveButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the existing Block N Load installation folder (containing the Win64 subfolder)",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        var selectedPath = dialog.SelectedPath;
        var exePath = Path.Combine(selectedPath, "Win64", "BlockNLoad.exe");
        if (!File.Exists(exePath))
        {
            MessageBox.Show(
                $"BlockNLoad.exe was not found in the selected folder.\n\nExpected: {exePath}\n\nMake sure you select the root game folder (the one containing the Win64 subfolder).",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!noSteamInstallService.IsFixApplied(selectedPath))
        {
            var apply = MessageBox.Show(
                "The no-Steam fix files are not present in this installation. Apply them now?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (apply == DialogResult.Yes)
            {
                noSteamInstallService.ApplyFixFiles(selectedPath);
            }
        }

        FinishSetup(selectedPath);
    }

    private void FinishSetup(string installPath)
    {
        settings.GamePath = installPath;
        settings.NoSteamMode = true;
        settingsService.Save(settings);
        logger.Info($"No-Steam install configured at: {installPath}");

        var installService = new BlockNLoadInstallService();
        ResultInstallInfo = installService.Detect(settings);

        if (!ResultInstallInfo.IsDetected)
        {
            MessageBox.Show(
                $"Setup completed but the game could not be validated:\n{ResultInstallInfo.FailureReason}",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task DownloadWithProgressAsync(string url, string destPath, string statusFormat, CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        long downloadedBytes = 0;

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        totalBytes = response.Content.Headers.ContentLength ?? 0;
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destPath);

        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            int percent = totalBytes > 0 ? (int)(downloadedBytes * 100 / totalBytes) : 0;
            var mbDownloaded = downloadedBytes / 1_048_576.0;
            var mbTotal = totalBytes > 0 ? $"/ {totalBytes / 1_048_576.0:F0} MB" : "";
            SetStatus(string.Format(statusFormat, percent) + $"  ({mbDownloaded:F1} MB {mbTotal})", percent);
        }
    }

    private static void ExtractZip(string zipPath, string destDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var destPath = Path.Combine(destDir, entry.FullName);
            var destDirForEntry = Path.GetDirectoryName(destPath)!;
            Directory.CreateDirectory(destDirForEntry);
            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    private void SetStatus(string text, int percent)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(text, percent));
            return;
        }
        statusLabel.Text = text;
        if (percent >= 0)
            progressBar.Value = Math.Clamp(percent, 0, 100);
    }

    private void SetControlsEnabled(bool enabled)
    {
        if (InvokeRequired) { Invoke(() => SetControlsEnabled(enabled)); return; }
        downloadButton.Enabled = enabled;
        alreadyHaveButton.Enabled = enabled;
        browseButton.Enabled = enabled;
        installPathTextBox.Enabled = enabled;
        depsCheckBox.Enabled = enabled;
        cancelButton.Text = enabled ? "Cancel" : "Cancel Download";
        cancelButton.DialogResult = enabled ? DialogResult.Cancel : DialogResult.None;
        if (!enabled) cancelButton.Click += CancelDownload_Click;
        else cancelButton.Click -= CancelDownload_Click;
    }

    private void CancelDownload_Click(object? sender, EventArgs e)
    {
        cts?.Cancel();
    }

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
