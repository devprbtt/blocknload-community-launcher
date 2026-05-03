using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class ManifestService
{
    private readonly HttpClient httpClient;

    public ManifestService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<UpdateManifest> FetchAsync(string manifestUrl, string expectedProduct, CancellationToken cancellationToken = default)
    {
        var json = await ReadManifestJsonAsync(manifestUrl, cancellationToken);
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (manifest is null)
        {
            throw new InvalidOperationException("Manifest could not be parsed.");
        }

        Validate(manifest, expectedProduct);
        return manifest;
    }

    private static void Validate(UpdateManifest manifest, string expectedProduct)
    {
        if (!string.Equals(manifest.Product, expectedProduct, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Manifest product is invalid.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("Manifest version is missing.");
        }

        ValidateAsset(manifest, "launcher_exe");
        ValidateAsset(manifest, "updater_exe");
    }

    private static void ValidateAsset(UpdateManifest manifest, string key)
    {
        if (!manifest.Assets.TryGetValue(key, out var asset))
        {
            throw new InvalidOperationException($"Manifest asset '{key}' is missing.");
        }

        if (string.IsNullOrWhiteSpace(asset.Url) ||
            string.IsNullOrWhiteSpace(asset.FileName) ||
            string.IsNullOrWhiteSpace(asset.Sha256))
        {
            throw new InvalidOperationException($"Manifest asset '{key}' is incomplete.");
        }
    }

    private async Task<string> ReadManifestJsonAsync(string manifestUrl, CancellationToken cancellationToken)
    {
        if (TryResolveLocalSource(manifestUrl, out var localPath))
        {
            return await File.ReadAllTextAsync(localPath, cancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, AppendCacheBustingQuery(manifestUrl));
        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        request.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
        request.Headers.UserAgent.ParseAdd("BnlCommunityFixes/2.x");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        return TryExtractEmbeddedManifestJson(responseText, out var embeddedManifestJson)
            ? embeddedManifestJson
            : responseText;
    }

    private static bool TryResolveLocalSource(string url, out string localPath)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            localPath = uri.LocalPath;
            return true;
        }

        if (Path.IsPathRooted(url))
        {
            localPath = url;
            return true;
        }

        localPath = string.Empty;
        return false;
    }

    private static string AppendCacheBustingQuery(string manifestUrl)
    {
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri))
        {
            return manifestUrl;
        }

        var builder = new UriBuilder(uri);
        var cacheBuster = $"bnlcb={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        builder.Query = string.IsNullOrWhiteSpace(builder.Query)
            ? cacheBuster
            : $"{builder.Query.TrimStart('?')}&{cacheBuster}";

        return builder.Uri.ToString();
    }

    private static bool TryExtractEmbeddedManifestJson(string responseText, out string manifestJson)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("content", out var contentElement) ||
                !root.TryGetProperty("encoding", out var encodingElement) ||
                !string.Equals(encodingElement.GetString(), "base64", StringComparison.OrdinalIgnoreCase))
            {
                manifestJson = string.Empty;
                return false;
            }

            var base64 = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(base64))
            {
                manifestJson = string.Empty;
                return false;
            }

            var normalizedBase64 = base64.Replace("\n", string.Empty).Replace("\r", string.Empty);
            var bytes = Convert.FromBase64String(normalizedBase64);
            manifestJson = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            return true;
        }
        catch (JsonException)
        {
            manifestJson = string.Empty;
            return false;
        }
        catch (FormatException)
        {
            manifestJson = string.Empty;
            return false;
        }
    }
}
