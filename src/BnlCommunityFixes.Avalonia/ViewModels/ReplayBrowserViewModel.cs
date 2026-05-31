using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed partial class ReplayBrowserViewModel : ViewModelBase
{
    private readonly GameInstallInfo _installInfo;
    private readonly ReplayLauncherService _replayService;
    private readonly Action _launchGame;

    [ObservableProperty] private ObservableCollection<ReplayRow> _replays = [];
    [ObservableProperty] private ReplayRow? _selectedReplay;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private bool _analyzeEnabled;
    [ObservableProperty] private bool _openValidationEnabled;
    [ObservableProperty] private bool _openLocationEnabled;
    [ObservableProperty] private bool _deleteEnabled;
    [ObservableProperty] private bool _launchReplayEnabled;

    public event Action<string, string>? ErrorOccurred;
    public event Func<string, Task<bool>>? ConfirmDelete;

    public ReplayBrowserViewModel(GameInstallInfo installInfo, ReplayLauncherService replayService, Action launchGame)
    {
        _installInfo = installInfo;
        _replayService = replayService;
        _launchGame = launchGame;
        Reload();
    }

    partial void OnSelectedReplayChanged(ReplayRow? value) => UpdateButtons();

    private void UpdateButtons()
    {
        var s = SelectedReplay;
        AnalyzeEnabled = s is not null;
        OpenValidationEnabled = s?.Capture.HasValidationReport == true;
        OpenLocationEnabled = s is not null;
        DeleteEnabled = s is not null;
        LaunchReplayEnabled = s is not null && _replayService.HasAnalyzedReplay(s.Capture.File);
    }

    [RelayCommand]
    private void Reload()
    {
        var captures = _replayService.ListCaptures(_installInfo);
        Replays.Clear();
        foreach (var c in captures) Replays.Add(new ReplayRow(c));
        Status = captures.Count == 0
            ? "No replay captures found. Finish a match and refresh."
            : $"{captures.Count} replay capture(s) found.";
        UpdateButtons();
    }

    [RelayCommand]
    private async Task Analyze()
    {
        if (SelectedReplay is not { } row) return;
        Busy = true;
        Status = $"Analyzing {row.Capture.File.Name}…";
        try
        {
            await _replayService.AnalyzeCaptureAsync(row.Capture.File, CancellationToken.None).ConfigureAwait(true);
            Status = $"Analyzed {row.Capture.File.Name}.";
            Reload();
        }
        catch (Exception ex) { ErrorOccurred?.Invoke("Analysis failed", ex.Message); Status = "Replay analysis failed."; }
        finally { Busy = false; }
    }

    [RelayCommand]
    private void OpenValidation()
    {
        if (SelectedReplay is not { } row) return;
        try { _replayService.OpenValidationReport(row.Capture.File); }
        catch (Exception ex) { ErrorOccurred?.Invoke("Error", ex.Message); }
    }

    [RelayCommand]
    private void OpenLocation()
    {
        if (SelectedReplay is not { } row) return;
        try { _replayService.OpenCaptureLocation(row.Capture.File); }
        catch (Exception ex) { ErrorOccurred?.Invoke("Error", ex.Message); }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedReplay is not { } row) return;
        if (ConfirmDelete is not null && !await ConfirmDelete($"Delete replay:\n{row.Capture.File.Name}?")) return;
        try { _replayService.DeleteCapture(row.Capture.File); Reload(); }
        catch (Exception ex) { ErrorOccurred?.Invoke("Delete failed", ex.Message); }
    }

    [RelayCommand]
    private void LaunchReplayMode()
    {
        if (SelectedReplay is not { } row) return;

        // Replay mode requires the match-replay-recorder feature to be enabled in the
        // feature bundle so that ReplayPlayerRuntime gets injected into Assembly-CSharp.
        if (!IsMatchReplayRecorderEnabled())
        {
            ErrorOccurred?.Invoke(
                "Replay mode requires Match Replay Recorder",
                "The 'Match Replay Recorder' feature must be enabled in Feature Settings for Replay Mode to work.\n\n" +
                "Go to Feature Settings → Misc, enable 'Match Replay Recorder', then launch the game once to rebuild the feature bundle.");
            return;
        }

        try
        {
            _replayService.WriteReplayLaunchRequest(row.Capture.File, launchReplayMode: true);
            var map = string.IsNullOrWhiteSpace(row.Capture.MapName) ? "unknown map" : row.Capture.MapName;
            Status = $"Launching replay mode for {row.Capture.File.Name} ({map}).";
            _launchGame();
        }
        catch (Exception ex) { ErrorOccurred?.Invoke("Error", ex.Message); }
    }

    private bool IsMatchReplayRecorderEnabled()
    {
        try
        {
            var configPath = Path.Combine(_replayService.PatchingDir, "experimental-match-replay-recorder-config.json");
            if (!File.Exists(configPath)) return false;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
            return doc.RootElement.TryGetProperty("enabled", out var p) && p.GetBoolean();
        }
        catch { return false; }
    }

    public sealed class ReplayRow(ReplayCaptureInfo capture)
    {
        public ReplayCaptureInfo Capture { get; } = capture;
        public string Date => Capture.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        public string Map => Capture.MapName ?? "Analyze to read";
        public string Duration => Capture.DurationSeconds is null ? "" : $"{Capture.DurationSeconds:0.0}s";
        public string Winner => Capture.Winner ?? "";
        public string Size => Capture.SizeBytes >= 1024 * 1024 ? $"{Capture.SizeBytes / 1024d / 1024d:0.0} MB" : $"{Capture.SizeBytes / 1024d:0.0} KB";
        public string Status => Capture.HasAnalyzedReplay ? "Analyzed" : "Not analyzed";
        public string File => Capture.File.Name;
    }
}
