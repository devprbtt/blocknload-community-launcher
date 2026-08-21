using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace BnlCommunityFixes.Core.Services;

public sealed class ReloadedBuildService
{
    private const string DownloadUrl = "https://bnl-reloaded.prbtthome.loan/api/v1/build/download.php";
    private readonly AppPaths paths;
    private readonly HttpClient httpClient;
    private readonly Logger logger;

    public ReloadedBuildService(AppPaths paths, HttpClient httpClient, Logger logger)
    {
        this.paths = paths;
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<bool> EnsureInstalledAsync(
        JsonElement? manifestElement,
        string accessToken,
        IProgress<ReloadedBuildProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var build = ParseManifest(manifestElement);
        var statePath = Path.Combine(paths.ReloadedCurrentDir, ".bnl-reloaded-build.json");
        if (File.Exists(paths.ReloadedExecutablePath) && InstalledBuildMatches(statePath, build))
        {
            return false;
        }

        var downloads = Path.Combine(paths.ReloadedDir, "downloads");
        Directory.CreateDirectory(downloads);
        var archivePath = Path.Combine(downloads, build.Name);
        var partialPath = archivePath + ".partial";
        await DownloadAsync(partialPath, build, accessToken, progress, cancellationToken);

        progress?.Report(new ReloadedBuildProgress("Verifying BNL Reloaded download...", null));
        string hash;
        await using (var hashStream = File.OpenRead(partialPath))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken));
        }
        if (!hash.Equals(build.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(partialPath);
            throw new InvalidDataException("The downloaded BNL Reloaded archive failed SHA-256 verification.");
        }
        File.Move(partialPath, archivePath, true);

        progress?.Report(new ReloadedBuildProgress("Preparing BNL Reloaded installation...", null));
        await Task.Run(
            () => InstallArchive(archivePath, build, statePath, progress),
            cancellationToken);
        progress?.Report(new ReloadedBuildProgress("BNL Reloaded installation complete.", 100));
        logger.Info($"Installed BNL Reloaded {build.Version} ({build.Sha256}).");
        return true;
    }

    private async Task DownloadAsync(
        string partialPath,
        ReloadedBuildManifest build,
        string accessToken,
        IProgress<ReloadedBuildProgress>? progress,
        CancellationToken cancellationToken)
    {
        var existing = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
        if (existing > build.Size)
        {
            File.Delete(partialPath);
            existing = 0;
        }
        if (existing == build.Size)
        {
            progress?.Report(CreateDownloadProgress(build.Version, existing, build.Size));
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, DownloadUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (existing > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existing, null);
        }
        using var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Closed-beta authorization expired or was revoked.");
        }
        response.EnsureSuccessStatusCode();

        var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!append) { existing = 0; }
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            partialPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
        var buffer = new byte[1024 * 1024];
        var lastReportedPercent = -1;
        progress?.Report(CreateDownloadProgress(build.Version, destination.Length, build.Size));
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            var percent = (int)(destination.Length * 100L / build.Size);
            if (percent != lastReportedPercent)
            {
                lastReportedPercent = percent;
                progress?.Report(CreateDownloadProgress(build.Version, destination.Length, build.Size));
            }
        }
        if (destination.Length != build.Size)
        {
            throw new InvalidDataException("The BNL Reloaded download size did not match its manifest.");
        }
        progress?.Report(CreateDownloadProgress(build.Version, destination.Length, build.Size));
    }

    private void InstallArchive(
        string archivePath,
        ReloadedBuildManifest build,
        string statePath,
        IProgress<ReloadedBuildProgress>? progress)
    {
        var staging = Path.Combine(paths.ReloadedDir, "staging-" + Guid.NewGuid().ToString("N"));
        var previous = Path.Combine(paths.ReloadedDir, "previous");
        Directory.CreateDirectory(staging);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var totalBytes = archive.Entries.Sum(static entry => entry.Length);
            long extractedBytes = 0;
            var lastReportedPercent = -1;
            var stagingRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            foreach (var entry in archive.Entries)
            {
                var destination = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                if (!destination.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The BNL Reloaded archive contains an unsafe path.");
                }
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
                extractedBytes += entry.Length;
                var percent = totalBytes == 0 ? 100 : (int)(extractedBytes * 100L / totalBytes);
                if (percent != lastReportedPercent)
                {
                    lastReportedPercent = percent;
                    progress?.Report(new ReloadedBuildProgress($"Installing BNL Reloaded... {percent}%", percent));
                }
            }
            if (!File.Exists(Path.Combine(staging, "Windows", "BlockNLoad.exe")))
            {
                throw new InvalidDataException("The BNL Reloaded archive does not contain Windows/BlockNLoad.exe.");
            }
            if (!File.Exists(Path.Combine(staging, "assetbundles", "assetbundles")))
            {
                throw new InvalidDataException("The BNL Reloaded archive does not contain the AssetBundle manifest.");
            }
            File.WriteAllText(
                Path.Combine(staging, ".bnl-reloaded-build.json"),
                JsonSerializer.Serialize(new { version = build.Version, sha256 = build.Sha256 }));

            if (Directory.Exists(previous))
            {
                progress?.Report(new ReloadedBuildProgress("Finalizing BNL Reloaded installation...", null));
                Directory.Delete(previous, true);
            }
            if (Directory.Exists(paths.ReloadedCurrentDir))
            {
                Directory.Move(paths.ReloadedCurrentDir, previous);
            }
            Directory.Move(staging, paths.ReloadedCurrentDir);
            if (Directory.Exists(previous)) { Directory.Delete(previous, true); }
        }
        catch
        {
            if (!Directory.Exists(paths.ReloadedCurrentDir) && Directory.Exists(previous))
            {
                Directory.Move(previous, paths.ReloadedCurrentDir);
            }
            throw;
        }
        finally
        {
            if (Directory.Exists(staging)) { Directory.Delete(staging, true); }
        }
    }

    private static ReloadedBuildProgress CreateDownloadProgress(string version, long completed, long total)
    {
        var percentage = total == 0 ? 100d : completed * 100d / total;
        var completedMiB = completed / (1024d * 1024d);
        var totalMiB = total / (1024d * 1024d);
        return new ReloadedBuildProgress(
            $"Downloading BNL Reloaded {version}... {percentage:0.0}% ({completedMiB:0} / {totalMiB:0} MiB)",
            percentage);
    }

    private static bool InstalledBuildMatches(string statePath, ReloadedBuildManifest build)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(statePath));
            return document.RootElement.GetProperty("version").GetString() == build.Version &&
                document.RootElement.GetProperty("sha256").GetString()?.Equals(
                    build.Sha256, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch { return false; }
    }

    private static ReloadedBuildManifest ParseManifest(JsonElement? element)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty("available", out var available) || !available.GetBoolean() ||
            !element.Value.TryGetProperty("version", out var version) ||
            !element.Value.TryGetProperty("files", out var files) || files.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No BNL Reloaded build is currently available.");
        }
        var file = files[0];
        return new ReloadedBuildManifest(
            version.GetString() ?? throw new InvalidDataException("Build version is missing."),
            file.GetProperty("name").GetString() ?? throw new InvalidDataException("Build name is missing."),
            file.GetProperty("size").GetInt64(),
            file.GetProperty("sha256").GetString() ?? throw new InvalidDataException("Build hash is missing."));
    }

    private sealed record ReloadedBuildManifest(string Version, string Name, long Size, string Sha256);
}

public sealed record ReloadedBuildProgress(string Message, double? Percentage);
