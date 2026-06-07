using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class GameSetupWindow : Window
{
    private const string GameDownloadUrlZip = "https://prbtthome.loan/local/BlockNLoad/BlockNLoad.zip";
    private const string MegaMirror1 = "https://mega.nz/file/Dqw3mb5B#T4h8zbLuBkaLWu0wEvHXjjfpvsYQAmb8ERRlmTXE-EI";
    private const string MegaMirror2 = "https://mega.nz/file/rjA2VYjR#MuDUPb5wQk1RuwUanJg8_gEakM2dmy5NZlkIcjLm_Rg";
    private const string MegaMirrorDeps = "https://mega.nz/file/X6wyyaJS#BPOu-0BMTLB5QbglEFJOpO8QozDQCMCWx2isIfACRCM";
    private const string DepsDownloadUrl = "https://prbtthome.loan/local/BlockNLoad/BlockNLoad%20Dependencies.zip";
    private const string DefaultInstallPath = @"C:\Games\BlockNLoad";

    private readonly AppPaths _paths;
    private readonly Logger _logger;
    private readonly LauncherSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly NoSteamInstallService _noSteamService;

    private CancellationTokenSource? _cts;

    public GameInstallInfo? ResultInstallInfo { get; private set; }

    public GameSetupWindow() : this(
        new AppPaths(), new Logger(""), new LauncherSettings(),
        new SettingsService(new AppPaths()), new HttpClient())
    { }

    public GameSetupWindow(
        AppPaths paths, Logger logger, LauncherSettings settings,
        SettingsService settingsService, HttpClient httpClient)
    {
        _paths = paths;
        _logger = logger;
        _settings = settings;
        _settingsService = settingsService;
        _httpClient = httpClient;
        _noSteamService = new NoSteamInstallService(typeof(GameSetupWindow).Assembly);

        InitializeComponent();
        InstallPathBox.Text = DefaultInstallPath;
    }

    private async void BrowseButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select install folder for Block N Load",
            AllowMultiple = false
        });
        if (folders.Count > 0)
            InstallPathBox.Text = folders[0].Path.LocalPath;
    }

    private async void DownloadButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var installPath = InstallPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(installPath))
        {
            await new MessageDialog("Missing Path", "Please choose an installation folder.", isError: false)
                .ShowDialog(this);
            return;
        }

        SetControlsEnabled(false);
        ProgressPanel.IsVisible = true;
        _cts = new CancellationTokenSource();

        try
        {
            Directory.CreateDirectory(installPath);

            var tempZip = Path.Combine(Path.GetTempPath(), "BlockNLoad_download.zip");
            await DownloadWithProgressAsync(GameDownloadUrlZip, tempZip, "Downloading game... {0}%", _cts.Token);

            SetProgress("Extracting game files...", -1);
            await Task.Run(() => ExtractZip(tempZip, installPath, (current, total) =>
                SetProgress($"Extracting game files... {current}/{total}", current * 100 / total)), _cts.Token);
            TryDelete(tempZip);

            if (DepsCheckBox.IsChecked == true)
            {
                var tempDeps = Path.Combine(Path.GetTempPath(), "BlockNLoad_deps.zip");
                await DownloadWithProgressAsync(DepsDownloadUrl, tempDeps, "Downloading audio dependencies... {0}%", _cts.Token);
                await Task.Run(() => ExtractZip(tempDeps, installPath, (current, total) =>
                    SetProgress($"Extracting audio dependencies... {current}/{total}", current * 100 / total)), _cts.Token);
                TryDelete(tempDeps);
            }

            // Fix files are already included in the game zip; only apply from embedded resources
            // if they weren't extracted (e.g. a zip without them).
            if (!_noSteamService.IsFixApplied(installPath))
            {
                SetProgress("Applying no-Steam fix files...", -1);
                _noSteamService.ApplyFixFiles(installPath);
            }

            FinishSetup(installPath);
        }
        catch (OperationCanceledException)
        {
            SetProgress("Download cancelled.", 0);
            SetControlsEnabled(true);
        }
        catch (Exception ex)
        {
            _logger.Exception(ex, "Game download/install failed");
            await new MessageDialog("Download Failed",
                $"{ex.Message}\n\nUse a MEGA mirror link to download manually, then click \"I already have the files\".",
                isError: true).ShowDialog(this);
            SetControlsEnabled(true);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async void AlreadyHaveButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select existing Block N Load folder (the one containing the Win64 subfolder)",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        var selectedPath = folders[0].Path.LocalPath;

        var exePath = Path.Combine(selectedPath, "Win64", "BlockNLoad.exe");
        if (!File.Exists(exePath))
        {
            await new MessageDialog("Not Found",
                $"BlockNLoad.exe was not found in the selected folder.\n\nExpected: {exePath}\n\nSelect the root game folder (the one containing the Win64 subfolder).",
                isError: false).ShowDialog(this);
            return;
        }

        if (!_noSteamService.IsFixApplied(selectedPath))
        {
            var apply = await new ConfirmDialog("The no-Steam fix files are not present. Apply them now?")
                .ShowDialog<bool>(this);
            if (apply)
            {
                try
                {
                    _noSteamService.ApplyFixFiles(selectedPath);
                }
                catch (Exception ex)
                {
                    _logger.Exception(ex, "Failed to apply no-Steam fix files");
                    await new MessageDialog("Setup Failed", $"Could not apply fix files: {ex.Message}", isError: true)
                        .ShowDialog(this);
                    return;
                }
            }
        }

        FinishSetup(selectedPath);
    }

    private void CancelButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }
        else
        {
            Close(false);
        }
    }

    private void Mirror1_Click(object? sender, PointerPressedEventArgs e) => OpenUrl(MegaMirror1);
    private void Mirror2_Click(object? sender, PointerPressedEventArgs e) => OpenUrl(MegaMirror2);
    private void Mirror3_Click(object? sender, PointerPressedEventArgs e) => OpenUrl(MegaMirrorDeps);

    private void FinishSetup(string installPath)
    {
        _settings.GamePath = installPath;
        _settings.NoSteamMode = true;
        _settingsService.Save(_settings);
        _logger.Info($"No-Steam install configured at: {installPath}");

        var installService = new BlockNLoadInstallService();
        ResultInstallInfo = installService.Detect(_settings);

        if (!ResultInstallInfo.IsDetected)
        {
            _ = new MessageDialog("Setup Warning",
                $"Setup completed but the game could not be validated:\n{ResultInstallInfo.FailureReason}",
                isError: false).ShowDialog(this);
            return;
        }

        Close(true);
    }

    private async Task DownloadWithProgressAsync(string url, string destPath, string statusFormat, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        long downloadedBytes = 0;

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
            SetProgress(string.Format(statusFormat, percent) + $"  ({mbDownloaded:F1} MB {mbTotal})", percent);
        }
    }

    private static void ExtractZip(string zipPath, string destDir, Action<int, int>? onProgress = null)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var fileEntries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();

        // Detect and strip a single common root folder inside the archive (e.g. "BlockNLoad/...")
        var topFolders = archive.Entries
            .Select(e => e.FullName.Split('/')[0])
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var stripPrefix = topFolders.Count == 1 ? topFolders[0] + "/" : null;

        var total = fileEntries.Count;
        var current = 0;
        foreach (var entry in fileEntries)
        {
            var relativePath = stripPrefix != null && entry.FullName.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase)
                ? entry.FullName[stripPrefix.Length..]
                : entry.FullName;

            if (string.IsNullOrEmpty(relativePath)) continue;

            var destPath = Path.Combine(destDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
            onProgress?.Invoke(++current, total);
        }
    }

    private void SetProgress(string text, int percent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusLabel.Text = text;
            if (percent >= 0)
            {
                PercentLabel.Text = $"{percent}%";
                ProgressBar.Value = Math.Clamp(percent * 10, 0, 1000);
            }
            else
            {
                PercentLabel.Text = "";
            }
        });
    }

    private void SetControlsEnabled(bool enabled)
    {
        Dispatcher.UIThread.Post(() =>
        {
            DownloadButton.IsEnabled = enabled;
            AlreadyHaveButton.IsEnabled = enabled;
            BrowseButton.IsEnabled = enabled;
            InstallPathBox.IsEnabled = enabled;
            DepsCheckBox.IsEnabled = enabled;
            CancelButton.Content = enabled ? "Cancel" : "Stop Download";
        });
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
