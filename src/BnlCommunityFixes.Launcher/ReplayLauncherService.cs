using System.Diagnostics;
using System.Text.Json;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class ReplayLauncherService
{
    private readonly AppPaths paths;
    private readonly Logger logger;
    private readonly LauncherSettings settings;
    private readonly DownloadService downloadService;
    private readonly ManifestService manifestService;

    public ReplayLauncherService(AppPaths paths, Logger logger, LauncherSettings settings, HttpClient httpClient)
    {
        this.paths = paths;
        this.logger = logger;
        this.settings = settings;
        downloadService = new DownloadService(httpClient);
        manifestService = new ManifestService(httpClient);
    }

    public string GetReplayDirectory(GameInstallInfo installInfo) =>
        Path.Combine(installInfo.GameRoot, "Win64", "BlockNLoad_Data", "bnl-match-replays");

    public string LatestAnalysisDirectory => Path.Combine(paths.DataDir, "replay-analysis", "latest");

    public string LatestViewerPath => Path.Combine(LatestAnalysisDirectory, "viewer.html");

    public string LatestMapStateViewerPath => Path.Combine(LatestAnalysisDirectory, "map_state_viewer.html");

    public IReadOnlyList<ReplayCaptureInfo> ListCaptures(GameInstallInfo installInfo)
    {
        var replayDirectory = GetReplayDirectory(installInfo);
        if (!Directory.Exists(replayDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(replayDirectory, "zone-capture-*.jsonl", SearchOption.TopDirectoryOnly)
            .Select(file => CreateCaptureInfo(new FileInfo(file)))
            .OrderByDescending(static capture => capture.LastWriteTime)
            .ToArray();
    }

    public FileInfo? GetLatestCapture(GameInstallInfo installInfo)
    {
        var replayDirectory = GetReplayDirectory(installInfo);
        if (!Directory.Exists(replayDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(replayDirectory, "zone-capture-*.jsonl", SearchOption.TopDirectoryOnly)
            .Select(static file => new FileInfo(file))
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    public void OpenReplayDirectory(GameInstallInfo installInfo)
    {
        var replayDirectory = GetReplayDirectory(installInfo);
        Directory.CreateDirectory(replayDirectory);
        OpenPath(replayDirectory);
    }

    public void OpenLatestViewer()
    {
        if (!File.Exists(LatestViewerPath))
        {
            throw new FileNotFoundException("No analyzed replay viewer exists yet. Analyze the latest replay first.", LatestViewerPath);
        }

        OpenPath(LatestViewerPath);
    }

    public void OpenLatestMapStateViewer()
    {
        if (!File.Exists(LatestMapStateViewerPath))
        {
            throw new FileNotFoundException("No analyzed map-state viewer exists yet. Analyze the latest replay first.", LatestMapStateViewerPath);
        }

        OpenPath(LatestMapStateViewerPath);
    }

    public string GetAnalysisDirectory(FileInfo capture) =>
        Path.Combine(paths.DataDir, "replay-analysis", Path.GetFileNameWithoutExtension(capture.Name));

    public string GetViewerPath(FileInfo capture) =>
        Path.Combine(GetAnalysisDirectory(capture), "viewer.html");

    public string GetMapStateViewerPath(FileInfo capture) =>
        Path.Combine(GetAnalysisDirectory(capture), "map_state_viewer.html");

    public string GetValidationReportPath(FileInfo capture) =>
        Path.Combine(GetAnalysisDirectory(capture), "validation.txt");

    public void OpenViewer(FileInfo capture)
    {
        var viewerPath = GetViewerPath(capture);
        if (!File.Exists(viewerPath))
        {
            throw new FileNotFoundException("No analyzed replay viewer exists for this capture yet. Analyze it first.", viewerPath);
        }

        OpenPath(viewerPath);
    }

    public void OpenMapStateViewer(FileInfo capture)
    {
        var viewerPath = GetMapStateViewerPath(capture);
        if (!File.Exists(viewerPath))
        {
            throw new FileNotFoundException("No analyzed map-state viewer exists for this capture yet. Analyze it first.", viewerPath);
        }

        OpenPath(viewerPath);
    }

    public void OpenValidationReport(FileInfo capture)
    {
        var validationPath = GetValidationReportPath(capture);
        if (!File.Exists(validationPath))
        {
            throw new FileNotFoundException("No validation report exists for this capture yet. Analyze it first.", validationPath);
        }

        OpenPath(validationPath);
    }

    public void OpenCaptureLocation(FileInfo capture)
    {
        if (!capture.Exists)
        {
            throw new FileNotFoundException("Replay capture not found.", capture.FullName);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { $"/select,{capture.FullName}" },
            UseShellExecute = true
        });
    }

    public void DeleteCapture(FileInfo capture)
    {
        if (capture.Exists)
        {
            capture.Delete();
        }
    }

    public async Task<ReplayAnalysisLaunchResult> AnalyzeLatestAsync(GameInstallInfo installInfo, CancellationToken cancellationToken)
    {
        var replayDirectory = GetReplayDirectory(installInfo);
        if (!Directory.Exists(replayDirectory))
        {
            throw new DirectoryNotFoundException($"Replay folder not found: {replayDirectory}");
        }

        var latestCapture = GetLatestCapture(installInfo);
        if (latestCapture is null)
        {
            throw new FileNotFoundException($"No replay captures were found in {replayDirectory}");
        }

        var analyzerPath = await EnsureAnalyzerAvailableAsync(cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(LatestAnalysisDirectory);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = analyzerPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add(replayDirectory);
        process.StartInfo.ArgumentList.Add(LatestAnalysisDirectory);

        logger.Info($"Analyzing latest replay with {analyzerPath}");

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Replay analyzer failed with exit code {process.ExitCode}.{Environment.NewLine}{error}{Environment.NewLine}{output}");
        }

        return new ReplayAnalysisLaunchResult(latestCapture.FullName, LatestAnalysisDirectory, LatestViewerPath, output);
    }

    public async Task<ReplayAnalysisLaunchResult> AnalyzeCaptureAsync(FileInfo capture, CancellationToken cancellationToken)
    {
        if (!capture.Exists)
        {
            throw new FileNotFoundException("Replay capture not found.", capture.FullName);
        }

        var analyzerPath = await EnsureAnalyzerAvailableAsync(cancellationToken).ConfigureAwait(false);
        var outputDirectory = GetAnalysisDirectory(capture);
        Directory.CreateDirectory(outputDirectory);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = analyzerPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add(capture.FullName);
        process.StartInfo.ArgumentList.Add(outputDirectory);

        logger.Info($"Analyzing replay capture {capture.FullName} with {analyzerPath}");

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Replay analyzer failed with exit code {process.ExitCode}.{Environment.NewLine}{error}{Environment.NewLine}{output}");
        }

        return new ReplayAnalysisLaunchResult(capture.FullName, outputDirectory, GetViewerPath(capture), output);
    }

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private string? ResolveAnalyzerPath()
    {
        var appDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDirectory, "BnlCommunityFixes.ReplayAnalyzer.exe"),
            paths.ReplayAnalyzerPath,
            Path.Combine(Environment.CurrentDirectory, "release", "replay-analyzer-test", "BnlCommunityFixes.ReplayAnalyzer.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task<string> EnsureAnalyzerAvailableAsync(CancellationToken cancellationToken)
    {
        var analyzerPath = ResolveAnalyzerPath();
        if (analyzerPath is not null)
        {
            return analyzerPath;
        }

        logger.Info("Replay analyzer executable was not found locally. Checking manifest for replay_analyzer_exe.");
        var manifest = await manifestService.FetchAsync(settings.ManifestUrl, settings.Product, cancellationToken).ConfigureAwait(false);
        if (!manifest.Assets.TryGetValue("replay_analyzer_exe", out var asset))
        {
            throw new FileNotFoundException(
                "Replay analyzer executable was not found locally, and the current update manifest does not include replay_analyzer_exe.");
        }

        await downloadService.DownloadFileAsync(asset.Url, paths.ReplayAnalyzerTempPath, null, cancellationToken).ConfigureAwait(false);
        if (!await HashService.VerifySha256Async(paths.ReplayAnalyzerTempPath, asset.Sha256, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Downloaded replay analyzer hash verification failed.");
        }

        File.Move(paths.ReplayAnalyzerTempPath, paths.ReplayAnalyzerPath, true);
        logger.Info($"Downloaded replay analyzer to {paths.ReplayAnalyzerPath}.");
        return paths.ReplayAnalyzerPath;
    }

    private ReplayCaptureInfo CreateCaptureInfo(FileInfo capture)
    {
        var viewerPath = GetViewerPath(capture);
        var mapStateViewerPath = GetMapStateViewerPath(capture);
        var normalizedPath = Path.Combine(GetAnalysisDirectory(capture), "replay.normalized.json");
        var metadata = TryReadMetadata(normalizedPath);
        return new ReplayCaptureInfo(
            capture,
            capture.LastWriteTime,
            capture.Length,
            File.Exists(viewerPath),
            File.Exists(mapStateViewerPath),
            File.Exists(GetValidationReportPath(capture)),
            metadata.MapName,
            metadata.DurationSeconds,
            metadata.Winner,
            metadata.Units,
            metadata.UsableForReplay,
            metadata.Quality,
            metadata.RequiredPassed,
            metadata.RequiredTotal,
            metadata.WarningCount);
    }

    private static ReplayCaptureMetadata TryReadMetadata(string normalizedPath)
    {
        if (!File.Exists(normalizedPath))
        {
            return ReplayCaptureMetadata.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(normalizedPath));
            var root = document.RootElement;
            var match = root.GetProperty("match");
            var stats = root.GetProperty("stats");
            var mapName = match.TryGetProperty("mapName", out var mapElement) ? mapElement.GetString() : null;
            var winner = match.TryGetProperty("winner", out var winnerElement) ? winnerElement.GetString() : null;
            var duration = match.TryGetProperty("duration", out var durationElement) ? durationElement.GetDouble() : (double?)null;
            var units = stats.TryGetProperty("units", out var unitsElement) ? unitsElement.GetInt32() : (int?)null;
            bool? usableForReplay = null;
            string? quality = null;
            int? requiredPassed = null;
            int? requiredTotal = null;
            int? warningCount = null;

            if (root.TryGetProperty("validation", out var validation))
            {
                usableForReplay = validation.TryGetProperty("UsableForReplay", out var usableElement)
                    ? usableElement.GetBoolean()
                    : null;
                quality = validation.TryGetProperty("Quality", out var qualityElement)
                    ? qualityElement.GetString()
                    : null;
                requiredPassed = validation.TryGetProperty("RequiredPassed", out var passedElement)
                    ? passedElement.GetInt32()
                    : null;
                requiredTotal = validation.TryGetProperty("RequiredTotal", out var totalElement)
                    ? totalElement.GetInt32()
                    : null;
                warningCount = validation.TryGetProperty("Warnings", out var warningsElement) && warningsElement.ValueKind == JsonValueKind.Array
                    ? warningsElement.GetArrayLength()
                    : null;
            }

            return new ReplayCaptureMetadata(mapName, duration, winner, units, usableForReplay, quality, requiredPassed, requiredTotal, warningCount);
        }
        catch
        {
            return ReplayCaptureMetadata.Empty;
        }
    }
}

public sealed record ReplayAnalysisLaunchResult(string CapturePath, string OutputDirectory, string ViewerPath, string AnalyzerOutput);
public sealed record ReplayCaptureInfo(
    FileInfo File,
    DateTime LastWriteTime,
    long SizeBytes,
    bool HasViewer,
    bool HasMapStateViewer,
    bool HasValidationReport,
    string? MapName,
    double? DurationSeconds,
    string? Winner,
    int? Units,
    bool? UsableForReplay,
    string? Quality,
    int? RequiredPassed,
    int? RequiredTotal,
    int? WarningCount);

internal sealed record ReplayCaptureMetadata(
    string? MapName,
    double? DurationSeconds,
    string? Winner,
    int? Units,
    bool? UsableForReplay,
    string? Quality,
    int? RequiredPassed,
    int? RequiredTotal,
    int? WarningCount)
{
    public static ReplayCaptureMetadata Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
