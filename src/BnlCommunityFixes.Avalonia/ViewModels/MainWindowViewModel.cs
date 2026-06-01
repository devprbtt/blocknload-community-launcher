using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;
using System.Reflection;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    internal readonly AppPaths _paths;
    internal readonly Logger _logger;
    internal readonly LauncherSettings _settings;
    internal readonly GameInstallInfo _installInfo;
    internal readonly LaunchCoordinator _launchCoordinator;
    internal readonly LauncherConfigService _launcherConfigService;
    internal readonly ReplayLauncherService _replayLauncherService;
    internal readonly SettingsService _settingsService;
    internal readonly LauncherSettingsProfileService _settingsProfileService;
    private readonly string _launcherVersion;

    private LauncherConfig? _launcherConfig;
    private bool _syncingReplayRecorder;
    private bool _syncingProfileCombo;

    // ── Observable properties ────────────────────────────────────────────────

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _gamePathText = string.Empty;
    [ObservableProperty] private string _detectionText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty] private ObservableCollection<ServerItem> _servers = [];
    [ObservableProperty] private ServerItem? _selectedServer;

    [ObservableProperty] private int _selectedProfileIndex;   // 0 = Recommended, 1 = Personal

    [ObservableProperty] private bool _launchEnabled;
    [ObservableProperty] private bool _replayControlsEnabled;
    [ObservableProperty] private bool _recordReplays;
    [ObservableProperty] private bool _recordCustom;
    [ObservableProperty] private bool _recordCasual;
    [ObservableProperty] private bool _recordRanked;
    [ObservableProperty] private bool _recordSubOptionsEnabled;

    // ── Factory ──────────────────────────────────────────────────────────────

    public static MainWindowViewModel Create(string[] args)
    {
        var paths = new AppPaths();
        paths.EnsureDirectories();
        var logger = new Logger(paths.LauncherLogPath);
        var settingsService = new SettingsService(paths);
        settingsService.EnsureDefaultFile();
        var settings = settingsService.Load();
        var installService = new BlockNLoadInstallService();
        var installInfo = installService.Detect(settings);
        var launcherConfigService = new LauncherConfigService();
        var launcherConfig = launcherConfigService.LoadOrCreate(installInfo, logger);
        using var httpClient = new System.Net.Http.HttpClient();
        return new MainWindowViewModel(paths, logger, settings, installInfo, launcherConfig, httpClient);
    }

    public MainWindowViewModel(
        AppPaths paths,
        Logger logger,
        LauncherSettings settings,
        GameInstallInfo installInfo,
        LauncherConfig? launcherConfig,
        System.Net.Http.HttpClient httpClient)
    {
        _paths = paths;
        _logger = logger;
        _settings = settings;
        _installInfo = installInfo;
        _launcherConfig = launcherConfig;
        _launcherVersion = LauncherVersion.GetDisplayVersion();
        _launchCoordinator = new LaunchCoordinator(paths, logger);
        _launcherConfigService = new LauncherConfigService();
        _replayLauncherService = new ReplayLauncherService(paths, logger, settings, httpClient);
        _settingsService = new SettingsService(paths);
        _settingsProfileService = new LauncherSettingsProfileService(paths);

        Title = $"Block N Load Community Fixes V2 - {_launcherVersion}";

        var shouldNotify = _settingsProfileService.ApplyUpdateDefaultsIfNeeded(settings, _launcherVersion, logger);
        _settingsProfileService.SyncActiveSettingsToRuntime(installInfo);
        EnsureSegmentedHealthbarTextureState();
        _settingsService.Save(settings);

        Reload();

        if (shouldNotify && _settingsProfileService.ShouldShowUpdateNotice(settings, _launcherVersion))
        {
            _pendingUpdateNotice = true;
        }
    }

    // ── Design-time constructor ───────────────────────────────────────────────

    public MainWindowViewModel()
    {
        _paths = null!; _logger = null!; _settings = null!; _installInfo = null!;
        _launchCoordinator = null!; _launcherConfigService = null!; _replayLauncherService = null!;
        _settingsService = null!; _settingsProfileService = null!; _launcherVersion = "0.0.0";
        Title = "Block N Load Community Fixes V2 - 0.0.0 (design)";
        GamePathText = "Game path: C:\\Games\\BlockNLoad";
        DetectionText = "Detection: steam:manifest";
        StatusText = "Design-time preview";
        Servers = [new ServerItem("eu", new LauncherServer { Name = "EU", Host = "eu.example.com", Port = 28100, Patch = "v310" })];
        SelectedServer = Servers[0];
        LaunchEnabled = true;
        ReplayControlsEnabled = true;
    }

    // ── Pending notice (shown by View after window opens) ────────────────────

    private bool _pendingUpdateNotice;
    public bool ConsumePendingUpdateNotice()
    {
        if (!_pendingUpdateNotice) return false;
        _pendingUpdateNotice = false;
        _settingsProfileService.DismissUpdateNotice(_settings, _launcherVersion);
        _settingsService.Save(_settings);
        return true;
    }

    // ── Reload ───────────────────────────────────────────────────────────────

    public void Reload()
    {
        if (_installInfo.IsDetected)
        {
            _launcherConfig = _launcherConfigService.LoadOrCreate(_installInfo, _logger);
        }

        PopulateServers();
        RefreshStatus();
    }

    private void PopulateServers()
    {
        Servers.Clear();

        if (_launcherConfig is null)
        {
            LaunchEnabled = false;
            return;
        }

        foreach (var entry in _launcherConfig.Servers.OrderBy(static e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            Servers.Add(new ServerItem(entry.Key, entry.Value));
        }

        LaunchEnabled = _installInfo.IsDetected && Servers.Count > 0;

        if (!string.IsNullOrWhiteSpace(_launcherConfig.SelectedServer))
        {
            SelectedServer = Servers.FirstOrDefault(s =>
                string.Equals(s.Key, _launcherConfig.SelectedServer, StringComparison.OrdinalIgnoreCase));
        }

        SelectedServer ??= Servers.FirstOrDefault();
    }

    private void RefreshStatus()
    {
        GamePathText = _installInfo.IsDetected
            ? $"Game path: {_installInfo.GameRoot}"
            : "Game path: not detected";

        DetectionText = _installInfo.IsDetected
            ? $"Detection: {_installInfo.DetectionSource}"
            : $"Detection failed: {_installInfo.FailureReason}";

        var lines = new List<string>
        {
            $"Launcher version: {_launcherVersion}",
            $"Manifest: {_settings.ManifestUrl}",
            $"Settings profile: {(LauncherSettingsProfileService.IsPersonalProfile(_settings.SettingsProfile) ? "Personal Settings" : "Recommended Settings")}",
            $"Settings file: {Path.Combine(_paths.DataDir, "launcher-settings.json")}",
            $"Patching dir: {_paths.PatchingDir}"
        };

        if (_installInfo.IsDetected)
        {
            lines.Add($"servers.txt: {_installInfo.ServersFilePath}");
            lines.Add($"Managed dir: {_installInfo.ManagedDirectoryPath}");
            lines.Add($"Replay dir: {_replayLauncherService.GetReplayDirectory(_installInfo)}");
            lines.Add($"Latest replay: {_replayLauncherService.GetLatestCapture(_installInfo)?.Name ?? "none"}");
            lines.Add($"Feature DLL present: {File.Exists(Path.Combine(_paths.PatchingDir, "Assembly-CSharp.experimental.dll"))}");
            lines.Add($"Helper DLL present: {File.Exists(Path.Combine(_paths.PatchingDir, "BnlCommunityFixes.dll"))}");
            var replayRecorder = LoadReplayRecorderConfig();
            lines.Add($"Replay recording: {(replayRecorder.Enabled ? $"enabled ({replayRecorder.ScopeSummary})" : "disabled")}");
        }

        if (SelectedServer is { } server)
        {
            lines.Add($"Selected server: {server.Key}");
            lines.Add($"Target: {server.Server.Host}:{server.Server.Port}");
            lines.Add($"Patch: {server.Server.Patch}");
        }

        StatusText = string.Join(Environment.NewLine, lines);
        ReplayControlsEnabled = _installInfo.IsDetected;

        SyncProfileCombo();
        SyncReplayRecorderFromConfig();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Launch()
    {
        if (!_installInfo.IsDetected || _launcherConfig is null || SelectedServer is not { } item)
            return;

        try
        {
            _launcherConfig.SelectedServer = item.Key;
            _launchCoordinator.LaunchSelectedServer(_installInfo, _launcherConfig);
        }
        catch (Exception ex)
        {
            _logger.Exception(ex, "Launch failed");
            OnError("Launch failed", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenReplayFolder()
    {
        if (!_installInfo.IsDetected) return;
        try { _replayLauncherService.OpenReplayDirectory(_installInfo); }
        catch (Exception ex) { _logger.Exception(ex, "Open replay folder failed"); OnError("Open folder failed", ex.Message); }
    }

    // ── Profile combo ─────────────────────────────────────────────────────────

    partial void OnSelectedProfileIndexChanged(int value)
    {
        if (_syncingProfileCombo || _settings is null) return;

        var profile = value == 1
            ? LauncherSettings.PersonalSettingsProfile
            : LauncherSettings.RecommendedSettingsProfile;

        try
        {
            _settingsProfileService.ApplySelectedProfile(_settings, profile, _logger);
            _settingsProfileService.SyncActiveSettingsToRuntime(_installInfo);
            _settingsService.Save(_settings);
            RefreshStatus();
        }
        catch (Exception ex)
        {
            _logger.Exception(ex, "Failed to apply settings profile");
            OnError("Settings profile error", ex.Message);
            SyncProfileCombo();
        }
    }

    private void SyncProfileCombo()
    {
        _syncingProfileCombo = true;
        try
        {
            SelectedProfileIndex = LauncherSettingsProfileService.IsPersonalProfile(_settings.SettingsProfile) ? 1 : 0;
        }
        finally { _syncingProfileCombo = false; }
    }

    // ── Replay recorder ───────────────────────────────────────────────────────

    partial void OnRecordReplaysChanged(bool value) => WriteReplayRecorderConfig();
    partial void OnRecordCustomChanged(bool value) => WriteReplayRecorderConfig();
    partial void OnRecordCasualChanged(bool value) => WriteReplayRecorderConfig();
    partial void OnRecordRankedChanged(bool value) => WriteReplayRecorderConfig();

    private void SyncReplayRecorderFromConfig()
    {
        _syncingReplayRecorder = true;
        try
        {
            var config = LoadReplayRecorderConfig();
            RecordReplays = config.Enabled;
            RecordCustom = config.RecordCustomGames;
            RecordCasual = config.RecordCasualGames;
            RecordRanked = config.RecordRankedGames;
            RecordSubOptionsEnabled = _installInfo.IsDetected && config.Enabled;
        }
        finally { _syncingReplayRecorder = false; }
    }

    private void WriteReplayRecorderConfig()
    {
        if (_syncingReplayRecorder || _paths is null) return;

        RecordSubOptionsEnabled = RecordReplays;

        var json = JsonSerializer.Serialize(new
        {
            enabled = RecordReplays,
            capture_payload = true,
            max_payload_bytes = 262144,
            record_custom_games = RecordCustom,
            record_casual_games = RecordCasual,
            record_ranked_games = RecordRanked
        }, new JsonSerializerOptions { WriteIndented = true });

        try
        {
            Directory.CreateDirectory(_paths.PatchingDir);
            File.WriteAllText(Path.Combine(_paths.PatchingDir, "experimental-match-replay-recorder-config.json"), json + Environment.NewLine);
            _settingsProfileService.SyncSelectedSnapshotFromActive(_settings, _logger);
            _settingsService.Save(_settings);
            RefreshStatus();
        }
        catch (Exception ex)
        {
            _logger.Exception(ex, "Failed to write replay recorder config");
            OnError("Replay config error", ex.Message);
        }
    }

    private ReplayRecorderConfig LoadReplayRecorderConfig()
    {
        try
        {
            var path = Path.Combine(_paths.PatchingDir, "experimental-match-replay-recorder-config.json");
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var r = doc.RootElement;
                return new ReplayRecorderConfig(
                    GetBool(r, "enabled", false),
                    GetBool(r, "record_custom_games", true),
                    GetBool(r, "record_casual_games", true),
                    GetBool(r, "record_ranked_games", true));
            }
        }
        catch { }
        return new ReplayRecorderConfig(false, true, true, true);
    }

    private static bool GetBool(JsonElement el, string key, bool def) =>
        el.TryGetProperty(key, out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? p.GetBoolean() : def;

    private void EnsureSegmentedHealthbarTextureState()
    {
        var configPath = Path.Combine(_paths.PatchingDir, "experimental-segmented-healthbar-config.json");
        var enabled = false;

        try
        {
            if (File.Exists(configPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                enabled = doc.RootElement.TryGetProperty("enabled", out var p) &&
                          p.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                          p.GetBoolean();
            }
        }
        catch
        {
            enabled = false;
        }

        var mappingPath = Path.Combine(_paths.PatchingDir, "texture-replacements.txt");
        const string fillEntry = "white_rect=white_rect.png";
        const string bgEntry = "white_rect_bg=white_rect_bg.png";
        var lines = File.Exists(mappingPath)
            ? File.ReadAllLines(mappingPath, System.Text.Encoding.UTF8).ToList()
            : [];

        if (enabled)
        {
            if (_installInfo.IsDetected)
            {
                Directory.CreateDirectory(_installInfo.CustomTexturesDirectoryPath);
                var asm = typeof(MainWindowViewModel).Assembly;
                foreach (var (resName, fileName) in new[]
                {
                    ("Patching.white_rect.png", "white_rect.png"),
                    ("Patching.white_rect_bg.png", "white_rect_bg.png")
                })
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream == null)
                    {
                        continue;
                    }

                    using var dest = File.Create(Path.Combine(_installInfo.CustomTexturesDirectoryPath, fileName));
                    stream.CopyTo(dest);
                }
            }

            if (!lines.Any(static l => l.StartsWith("white_rect=", StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(fillEntry);
            }

            if (!lines.Any(static l => l.StartsWith("white_rect_bg=", StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(bgEntry);
            }
        }
        else
        {
            lines.RemoveAll(static l =>
                l.StartsWith("white_rect=", StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith("white_rect_bg=", StringComparison.OrdinalIgnoreCase));
        }

        Directory.CreateDirectory(_paths.PatchingDir);
        File.WriteAllLines(mappingPath, lines, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // ── Error notification (raised to View) ───────────────────────────────────

    public event Action<string, string>? ErrorOccurred;
    private void OnError(string title, string message) => ErrorOccurred?.Invoke(title, message);

    // ── Nested types ──────────────────────────────────────────────────────────

    public sealed record ServerItem(string Key, LauncherServer Server)
    {
        public override string ToString() =>
            $"{Server.Name} [{Server.Host}:{Server.Port}] ({Server.Patch})";
    }

    private sealed record ReplayRecorderConfig(bool Enabled, bool RecordCustomGames, bool RecordCasualGames, bool RecordRankedGames)
    {
        public string ScopeSummary
        {
            get
            {
                var parts = new List<string>();
                if (RecordCustomGames) parts.Add("custom");
                if (RecordCasualGames) parts.Add("casual");
                if (RecordRankedGames) parts.Add("ranked");
                return parts.Count == 0 ? "no match types selected" : string.Join(", ", parts);
            }
        }
    }
}
