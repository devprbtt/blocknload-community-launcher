using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public enum ServerListRefreshOutcome
{
    Updated,
    Failed
}

/// <summary>
/// Fetches the community server list from GitHub so it can be updated without
/// shipping a new launcher release. On success the remote content overwrites the
/// local <c>launcher/cache/main.json</c> file, which the existing
/// <see cref="LauncherConfigService"/> merge pipeline then consumes as usual.
/// </summary>
public sealed class ServerListService
{
    /// <summary>
    /// Hardcoded raw sources, tried in order until one succeeds. Uses the branch
    /// ref (not a tag/commit) so a merged PR to <c>main</c> takes effect
    /// immediately. <c>raw.githubusercontent.com</c> is blocked by the Great
    /// Firewall for many players in China, so GitHub proxy services are used as fallback.
    /// </summary>
    public static readonly string[] ServerListUrls =
    {
        "https://raw.githubusercontent.com/devprbtt/blocknload-community-launcher/refs/heads/main/updates/servers.json",
        "https://ghproxy.net/raw.githubusercontent.com/devprbtt/blocknload-community-launcher/refs/heads/main/updates/servers.json",
        "https://gh-proxy.com/raw.githubusercontent.com/devprbtt/blocknload-community-launcher/refs/heads/main/updates/servers.json"
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly Logger _logger;

    public ServerListService(HttpClient httpClient, Logger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the remote server list and, on success, updates the servers in the
    /// local <paramref name="mainCachePath"/> cache. Only the server list is taken
    /// from GitHub — the cache's existing <c>patch_configurations</c> are preserved,
    /// since patch definitions are tied to game-binary hashes and ship with the
    /// launcher. Never throws: any failure (offline, non-success status, malformed
    /// or empty JSON, or an unreadable cache) returns
    /// <see cref="ServerListRefreshOutcome.Failed"/> and leaves the existing cache
    /// untouched so the launcher keeps working offline.
    /// </summary>
    public async Task<ServerListRefreshOutcome> RefreshMainCacheAsync(string mainCachePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var (json, sourceUrl) = await FetchAsync(cancellationToken);
            if (json is null)
            {
                _logger.Warning("Could not reach any server list source (tried all fallback links); keeping cached copy.");
                return ServerListRefreshOutcome.Failed;
            }

            // Validate the remote payload before touching the cache so a bad
            // response can never wipe out a working local server list.
            var remote = JsonSerializer.Deserialize<LauncherConfig>(json, ReadOptions);
            if (remote is null || remote.Servers.Count == 0)
            {
                _logger.Warning("Remote server list was empty or invalid; keeping cached copy.");
                return ServerListRefreshOutcome.Failed;
            }

            // Load the existing cache so we can preserve its patch_configurations.
            // If it can't be read we skip rather than risk writing a cache with no
            // patch definitions (which would break launching).
            var cache = ReadExistingCache(mainCachePath);
            if (cache is null)
            {
                _logger.Warning("Existing server cache missing or unreadable; skipping refresh to preserve patch configurations.");
                return ServerListRefreshOutcome.Failed;
            }

            cache.Servers = remote.Servers;

            Directory.CreateDirectory(Path.GetDirectoryName(mainCachePath)!);
            var merged = JsonSerializer.Serialize(cache, WriteOptions);
            await File.WriteAllTextAsync(mainCachePath, merged, new UTF8Encoding(false), cancellationToken);
            _logger.Info($"Updated server list cache from {sourceUrl} ({remote.Servers.Count} servers, patch configurations preserved).");
            return ServerListRefreshOutcome.Updated;
        }
        catch (Exception exception)
        {
            _logger.Exception(exception, "Failed to refresh server list from GitHub");
            return ServerListRefreshOutcome.Failed;
        }
    }

    private LauncherConfig? ReadExistingCache(string mainCachePath)
    {
        try
        {
            if (!File.Exists(mainCachePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(mainCachePath, Encoding.UTF8), ReadOptions);
        }
        catch (Exception exception)
        {
            _logger.Warning($"Could not read existing server cache '{mainCachePath}': {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Tries each URL in <see cref="ServerListUrls"/> in order, falling through to
    /// the next on any failure (network error, timeout, non-success status). This
    /// is what lets players behind the Great Firewall (where
    /// raw.githubusercontent.com is commonly blocked) still get updates via a
    /// GitHub proxy mirror without any user-visible difference.
    /// </summary>
    private async Task<(string? Json, string? SourceUrl)> FetchAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < ServerListUrls.Length; i++)
        {
            var url = ServerListUrls[i];
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, AppendCacheBustingQuery(url));
                request.Headers.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };
                request.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
                request.Headers.UserAgent.ParseAdd("BnlCommunityFixes/2.x");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadAsStringAsync(cancellationToken), url);
            }
            catch (Exception exception) when (i < ServerListUrls.Length - 1)
            {
                _logger.Warning($"Server list source '{url}' failed ({exception.Message}); trying fallback.");
            }
        }

        return (null, null);
    }

    private static string AppendCacheBustingQuery(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var builder = new UriBuilder(uri);
        var cacheBuster = $"bnlcb={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        builder.Query = string.IsNullOrWhiteSpace(builder.Query)
            ? cacheBuster
            : $"{builder.Query.TrimStart('?')}&{cacheBuster}";

        return builder.Uri.ToString();
    }
}
