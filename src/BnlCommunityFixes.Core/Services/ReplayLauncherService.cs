using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Core.Services;

public sealed class ReplayLauncherService
{
    private static readonly TimeSpan MinimumCaptureAgeBeforeCompression = TimeSpan.FromMinutes(2);

    private readonly AppPaths paths;
    private readonly Logger logger;
    private readonly LauncherSettings settings;

    public ReplayLauncherService(AppPaths paths, Logger logger, LauncherSettings settings, HttpClient httpClient)
    {
        this.paths = paths;
        this.logger = logger;
        this.settings = settings;
    }

    public string GetReplayDirectory(GameInstallInfo installInfo) =>
        installInfo.ReplaysDirectoryPath;

    public string PatchingDir => paths.PatchingDir;
    public string LatestAnalysisDirectory => Path.Combine(paths.DataDir, "replay-analysis", "latest");

    public string ReplayLaunchRequestPath => Path.Combine(paths.DataDir, "replay-launch-request.json");
    public string ReplayLaunchRequestTextPath => Path.Combine(paths.DataDir, "replay-launch-request.path");

    public IReadOnlyList<ReplayCaptureInfo> ListCaptures(GameInstallInfo installInfo)
    {
        var replayDirectory = GetReplayDirectory(installInfo);
        if (!Directory.Exists(replayDirectory))
        {
            return [];
        }

        CompressCompletedCaptures(replayDirectory);

        return Directory
            .EnumerateFiles(replayDirectory, "zone-capture-*.jsonl.gz", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(replayDirectory, "zone-capture-*.jsonl", SearchOption.TopDirectoryOnly))
            .Select(file => CreateCaptureInfo(new FileInfo(file)))
            .Where(static capture => capture.SizeBytes > 0)
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

        CompressCompletedCaptures(replayDirectory);

        return Directory
            .EnumerateFiles(replayDirectory, "zone-capture-*.jsonl.gz", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(replayDirectory, "zone-capture-*.jsonl", SearchOption.TopDirectoryOnly))
            .Select(static file => new FileInfo(file))
            .Where(static file => file.Length > 0)
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    public void OpenReplayDirectory(GameInstallInfo installInfo)
    {
        var replayDirectory = GetReplayDirectory(installInfo);
        Directory.CreateDirectory(replayDirectory);
        OpenPath(replayDirectory);
    }

    public string GetAnalysisDirectory(FileInfo capture) =>
        Path.Combine(paths.DataDir, "replay-analysis", GetCaptureBaseName(capture.Name));

    public string GetValidationReportPath(FileInfo capture) =>
        Path.Combine(GetAnalysisDirectory(capture), "validation.txt");

    public string GetNormalizedReplayPath(FileInfo capture) =>
        Path.Combine(GetAnalysisDirectory(capture), "replay.normalized.json");

    public void OpenValidationReport(FileInfo capture)
    {
        var validationPath = GetValidationReportPath(capture);
        if (!File.Exists(validationPath))
        {
            throw new FileNotFoundException("No validation report exists for this capture yet. Analyze it first.", validationPath);
        }

        OpenPath(validationPath);
    }

    public bool HasAnalyzedReplay(FileInfo capture) =>
        File.Exists(GetNormalizedReplayPath(capture));

    public void WriteReplayLaunchRequest(FileInfo capture, bool launchReplayMode)
    {
        if (!capture.Exists)
        {
            throw new FileNotFoundException("Replay capture not found.", capture.FullName);
        }

        var analysisDirectory = GetAnalysisDirectory(capture);
        var normalizedPath = GetNormalizedReplayPath(capture);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("No analyzed replay exists for this capture yet. Analyze it first.", normalizedPath);
        }

        var metadata = TryReadMetadata(normalizedPath);
        Directory.CreateDirectory(paths.DataDir);
        var request = new ReplayLaunchRequest(
            capture.FullName,
            analysisDirectory,
            normalizedPath,
            Path.Combine(analysisDirectory, "map_blocks.csv"),
            Path.Combine(analysisDirectory, "map_state_timeline.csv"),
            metadata.MapName,
            metadata.DurationSeconds,
            DateTimeOffset.UtcNow,
            launchReplayMode);

        File.WriteAllText(ReplayLaunchRequestPath, JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(ReplayLaunchRequestTextPath, normalizedPath);
        logger.Info($"Wrote replay launch request for {capture.FullName} to {ReplayLaunchRequestPath}. Replay mode launch: {launchReplayMode}");
    }

    public void OpenCaptureLocation(FileInfo capture)
    {
        if (!capture.Exists)
        {
            throw new FileNotFoundException("Replay capture not found.", capture.FullName);
        }

        PlatformShell.RevealPath(capture.FullName);
    }

    public void DeleteCapture(FileInfo capture)
    {
        if (capture.Exists)
        {
            capture.Delete();
        }
    }

    private void CompressCompletedCaptures(string replayDirectory)
    {
        foreach (var path in Directory.EnumerateFiles(replayDirectory, "zone-capture-*.jsonl", SearchOption.TopDirectoryOnly))
        {
            TryCompressCompletedCapture(new FileInfo(path));
        }
    }

    private void TryCompressCompletedCapture(FileInfo capture)
    {
        if (!capture.Exists || capture.Length == 0)
        {
            return;
        }

        if (DateTime.UtcNow - capture.LastWriteTimeUtc < MinimumCaptureAgeBeforeCompression)
        {
            return;
        }

        var compressedPath = capture.FullName + ".gz";
        var tempPath = compressedPath + ".tmp";

        try
        {
            if (File.Exists(compressedPath) && new FileInfo(compressedPath).Length > 0)
            {
                capture.Delete();
                return;
            }

            File.Delete(tempPath);

            using (var source = new FileStream(capture.FullName, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var gzip = new GZipStream(destination, CompressionLevel.Optimal))
            {
                source.CopyTo(gzip);
            }

            if (new FileInfo(tempPath).Length == 0)
            {
                File.Delete(tempPath);
                return;
            }

            File.Move(tempPath, compressedPath);
            capture.Delete();
            logger.Info($"Compressed replay capture {capture.FullName} to {compressedPath}.");
        }
        catch (IOException)
        {
            File.Delete(tempPath);
        }
        catch (UnauthorizedAccessException)
        {
            File.Delete(tempPath);
        }
        catch (Exception exception)
        {
            File.Delete(tempPath);
            logger.Warning($"Failed to compress replay capture {capture.FullName}: {exception.Message}");
        }
    }

    private static string GetCaptureBaseName(string fileName)
    {
        if (fileName.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^".jsonl.gz".Length];
        }

        if (fileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^".jsonl".Length];
        }

        return Path.GetFileNameWithoutExtension(fileName);
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

        var analyzerPath = ResolveAnalyzerHostPath();

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
        process.StartInfo.ArgumentList.Add("--analyze-replay");
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

        return new ReplayAnalysisLaunchResult(latestCapture.FullName, LatestAnalysisDirectory, output);
    }

    public async Task<ReplayAnalysisLaunchResult> AnalyzeCaptureAsync(FileInfo capture, CancellationToken cancellationToken)
    {
        if (!capture.Exists)
        {
            throw new FileNotFoundException("Replay capture not found.", capture.FullName);
        }

        var analyzerPath = ResolveAnalyzerHostPath();
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
        process.StartInfo.ArgumentList.Add("--analyze-replay");
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

        return new ReplayAnalysisLaunchResult(capture.FullName, outputDirectory, output);
    }

    private static void OpenPath(string path)
    {
        PlatformShell.OpenPath(path);
    }

    private string ResolveAnalyzerHostPath()
    {
        var candidates = new[]
        {
            Environment.ProcessPath,
            paths.LauncherPath
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not resolve the launcher executable to run embedded replay analysis.");
    }

    private ReplayCaptureInfo CreateCaptureInfo(FileInfo capture)
    {
        var normalizedPath = Path.Combine(GetAnalysisDirectory(capture), "replay.normalized.json");
        var metadata = TryReadMetadata(normalizedPath);
        return new ReplayCaptureInfo(
            capture,
            capture.LastWriteTime,
            capture.Length,
            File.Exists(normalizedPath),
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

public sealed record ReplayAnalysisLaunchResult(string CapturePath, string OutputDirectory, string AnalyzerOutput);
public sealed record ReplayLaunchRequest(
    string CapturePath,
    string AnalysisDirectory,
    string NormalizedPath,
    string MapBlocksPath,
    string MapStateTimelinePath,
    string? MapName,
    double? DurationSeconds,
    DateTimeOffset RequestedUtc,
    bool LaunchReplayMode);
public sealed record ReplayCaptureInfo(
    FileInfo File,
    DateTime LastWriteTime,
    long SizeBytes,
    bool HasAnalyzedReplay,
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
