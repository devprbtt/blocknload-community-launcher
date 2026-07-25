using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Sockets;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

/// <summary>
/// Manages the embedded bnlReloaded server used by Bot / Offline Practice Mode.
/// When the bot-mode feature is enabled, the launcher starts this server locally and
/// points the game at 127.0.0.1 instead of a community server. The server binaries
/// live under app/offline-server/bin and run from app/offline-server/run.
/// </summary>
public sealed class OfflineBotServerService
{
    public const string LocalHost = "127.0.0.1";
    public const int MasterPort = 28100;

    private static Process? serverProcess;
    private static readonly object StartLock = new();
    private static readonly HttpClient DownloadClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    private readonly string serverRoot;
    private readonly string serverBinDir;
    private readonly string serverRunDir;

    public OfflineBotServerService(AppPaths paths)
    {
        serverRoot = Path.Combine(paths.AppDir, "offline-server");
        serverBinDir = Path.Combine(serverRoot, "bin");
        serverRunDir = Path.Combine(serverRoot, "run");
    }

    public string ServerExecutablePath => Path.Combine(
        serverBinDir,
        OperatingSystem.IsWindows() ? "BNLReloadedServer.exe" : "BNLReloadedServer");

    /// <summary>Starts the embedded server if it is not already reachable on the master port.</summary>
    public void EnsureStarted(GameInstallInfo installInfo, Logger logger, BotModeSettings? botSettings = null)
    {
        lock (StartLock)
        {
            var configChanged = WriteServerConfig(botSettings, logger);

            if (IsMasterPortOpen())
            {
                if (!configChanged)
                {
                    logger.Info("Offline server already running on 127.0.0.1:28100 — reusing it.");
                    return;
                }

                // Bot settings changed — restart our instance so the server picks them up.
                if (serverProcess is { HasExited: false })
                {
                    logger.Info("Bot settings changed — restarting the embedded offline server.");
                    Stop(logger);
                }
                else
                {
                    logger.Warning("Offline server on 28100 was not started by this launcher; new bot settings apply after it restarts.");
                    return;
                }
            }

            if (!File.Exists(ServerExecutablePath))
            {
                DownloadOfflineServer(logger);
            }

            PrepareRunDirectory(installInfo, logger);

            var startInfo = new ProcessStartInfo
            {
                FileName = ServerExecutablePath,
                WorkingDirectory = serverRunDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            logger.Info("Starting embedded offline server (bnlReloaded)...");
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the embedded offline server process.");
            serverProcess = process;

            // Drain output to a log file so the process never blocks on full pipes.
            var logPath = Path.Combine(serverRunDir, "server.log");
            var logWriter = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) TryWriteLine(logWriter, e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) TryWriteLine(logWriter, e.Data); };
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => { TryWriteLine(logWriter, "[launcher] server process exited."); try { logWriter.Dispose(); } catch { } };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!WaitForMasterPort(TimeSpan.FromSeconds(20)))
            {
                var exited = process.HasExited ? $" Process exited with code {process.ExitCode}." : string.Empty;
                throw new InvalidOperationException(
                    "The embedded offline server did not start listening on port 28100 within 20 seconds." +
                    exited + " See " + logPath + " for details.");
            }

            logger.Info("Embedded offline server is up on 127.0.0.1:28100.");
        }
    }

    private void DownloadOfflineServer(Logger logger)
    {
        var isWindows = OperatingSystem.IsWindows();
        if (!isWindows && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Offline Bot Mode currently supports Windows and Linux x64.");

        var rid = isWindows ? "win-x64" : "linux-x64";
        var extension = isWindows ? ".zip" : ".tar.gz";
        var assetName = $"offline-server-{rid}{extension}";
        var version = LauncherVersion.GetDisplayVersion();
        var url = $"https://github.com/devprbtt/blocknload-community-launcher/releases/download/v{version}/{assetName}";

        Directory.CreateDirectory(serverRoot);
        Directory.CreateDirectory(serverBinDir);
        var archivePath = Path.Combine(serverRoot, $".{assetName}.{Guid.NewGuid():N}.download");

        logger.Info($"Offline server is missing; downloading {assetName} for launcher v{version}...");
        try
        {
            using (var response = DownloadClient
                       .GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                       .GetAwaiter()
                       .GetResult())
            {
                response.EnsureSuccessStatusCode();
                using var source = response.Content.ReadAsStream();
                using var destination = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                source.CopyTo(destination);
            }

            if (isWindows)
            {
                ZipFile.ExtractToDirectory(archivePath, serverBinDir, overwriteFiles: true);
            }
            else
            {
                using var archive = File.OpenRead(archivePath);
                using var gzip = new GZipStream(archive, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gzip, serverBinDir, overwriteFiles: true);
                File.SetUnixFileMode(
                    ServerExecutablePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            if (!File.Exists(ServerExecutablePath))
                throw new InvalidDataException($"{assetName} did not contain {Path.GetFileName(ServerExecutablePath)}.");

            logger.Info($"Offline server installed to {serverBinDir}.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not download the offline server from {url}. Check your connection and try again.", ex);
        }
        finally
        {
            try { File.Delete(archivePath); } catch { }
        }
    }

    public void Stop(Logger logger)
    {
        lock (StartLock)
        {
            if (serverProcess is { HasExited: false })
            {
                try
                {
                    serverProcess.Kill(entireProcessTree: true);
                    logger.Info("Embedded offline server stopped.");
                }
                catch (Exception ex)
                {
                    logger.Warning("Failed to stop embedded offline server: " + ex.Message);
                }
            }

            serverProcess = null;
        }
    }

    /// <summary>
    /// Keeps the embedded server tied to the exact game process started by the launcher.
    /// </summary>
    public void StopWhenGameExits(Process gameProcess, Logger logger)
    {
        ArgumentNullException.ThrowIfNull(gameProcess);

        void OnGameExited(object? sender, EventArgs args)
        {
            logger.Info("Block N Load exited — stopping the embedded offline server.");
            Stop(logger);
            try { gameProcess.Dispose(); } catch { }
        }

        gameProcess.EnableRaisingEvents = true;
        gameProcess.Exited += OnGameExited;

        // Cover the race where the process exits between Process.Start and attaching
        // the handler. Exited may also run; Stop is locked and safely idempotent.
        if (gameProcess.HasExited)
        {
            OnGameExited(gameProcess, EventArgs.Empty);
        }
    }

    /// <summary>Writes configs.json with current bot settings. Returns true if it changed.</summary>
    private bool WriteServerConfig(BotModeSettings? botSettings, Logger logger)
    {
        Directory.CreateDirectory(Path.Combine(serverRunDir, "Configs"));

        var botCount = Math.Clamp(botSettings?.BotCount ?? 3, 0, 9);
        var difficulty = string.IsNullOrWhiteSpace(botSettings?.Difficulty) ? "medium" : botSettings.Difficulty;
        var configPath = Path.Combine(serverRunDir, "Configs", "configs.json");
        var content =
            $$"""
            {
              "is_master": true,
              "run_server": true,
              "use_master_cdb": false,
              "cdb_name": "cdb",
              "master_host": "127.0.0.1",
              "master_public_host": "127.0.0.1",
              "region_host": "127.0.0.1",
              "region_public_host": "127.0.0.1",
              "region_name": "Offline",
              "region_icon": "eu",
              "to_json": false,
              "from_json": false,
              "use_couch_db": false,
              "debug_mode": false,
              "do_readline": false,
              "bot_count": {{botCount}},
              "bot_difficulty": "{{difficulty}}"
            }
            """;

        if (File.Exists(configPath) && File.ReadAllText(configPath) == content)
        {
            return false;
        }

        File.WriteAllText(configPath, content);
        logger.Info($"Wrote embedded offline server config (bots: {botCount}, difficulty: {difficulty}).");
        return true;
    }

    private void PrepareRunDirectory(GameInstallInfo installInfo, Logger logger)
    {
        Directory.CreateDirectory(serverRunDir);
        Directory.CreateDirectory(Path.Combine(serverRunDir, "Cache"));
        Directory.CreateDirectory(Path.Combine(serverRunDir, "PlayerData"));
        SyncDirectory(Path.Combine(serverBinDir, "Maps"), Path.Combine(serverRunDir, "Maps"));

        // Catalogue (cdb) source priority:
        //  1. cdb-override next to the server (user-pinned catalogue — drop a file at
        //     app\offline-server\cdb-override to force a specific cdb),
        //  2. the catalogue bundled with the server binaries (canonical for bnlReloaded),
        //  3. the game's cache (whatever catalogue the last online server replicated —
        //     can drift, e.g. to the Chinese-default v3.10 variant, so it's last).
        var overrideCdb = Path.Combine(Path.GetDirectoryName(serverBinDir)!, "cdb-override");
        var bundledCdb = Path.Combine(serverBinDir, "Cache", "cdb");
        var gameCdb = Path.Combine(installInfo.GameRoot, "Cache", "cdb");
        var serverCdb = Path.Combine(serverRunDir, "Cache", "cdb");

        string? source = null;
        string? sourceName = null;
        if (File.Exists(overrideCdb)) { source = overrideCdb; sourceName = "cdb-override"; }
        else if (File.Exists(bundledCdb)) { source = bundledCdb; sourceName = "bundled server catalogue"; }
        else if (File.Exists(gameCdb)) { source = gameCdb; sourceName = "game cache"; }

        if (source != null)
        {
            var needsCopy = !File.Exists(serverCdb)
                || new FileInfo(source).Length != new FileInfo(serverCdb).Length
                || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(serverCdb);
            if (needsCopy)
            {
                File.Copy(source, serverCdb, overwrite: true);
                logger.Info($"Offline server catalogue updated from {sourceName}.");
            }
        }
        else if (!File.Exists(serverCdb))
        {
            throw new InvalidOperationException(
                "No catalogue (cdb) available for the offline server — expected one at " + bundledCdb + " or " + gameCdb + ".");
        }
    }

    private static void SyncDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            return;

        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            var source = new FileInfo(sourcePath);
            var destination = new FileInfo(destinationPath);
            if (!destination.Exists || destination.Length != source.Length ||
                destination.LastWriteTimeUtc < source.LastWriteTimeUtc)
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
        }
    }

    private static bool IsMasterPortOpen()
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(LocalHost, MasterPort);
            return task.Wait(TimeSpan.FromMilliseconds(500)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static bool WaitForMasterPort(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsMasterPortOpen())
                return true;
            Thread.Sleep(250);
        }

        return false;
    }

    private static void TryWriteLine(StreamWriter writer, string line)
    {
        try { writer.WriteLine(line); }
        catch { }
    }
}
