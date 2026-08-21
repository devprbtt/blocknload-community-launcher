using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Services;

public sealed class ReloadedBetaService
{
    private const string DefaultOrigin = "https://bnl-reloaded.prbtthome.loan";
    private const string OriginEnvironmentVariable = "BNL_RELOADED_API_URL";
    private readonly HttpClient httpClient;
    private readonly Uri origin;

    public ReloadedBetaService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
        var configured = Environment.GetEnvironmentVariable(OriginEnvironmentVariable);
        origin = new Uri((string.IsNullOrWhiteSpace(configured) ? DefaultOrigin : configured).TrimEnd('/') + "/");
    }

    public async Task<ReloadedServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<ReloadedServiceStatus>("api/v1/status.php", cancellationToken);
    }

    public async Task<ReloadedAuthorizationStart> StartAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(new Uri(origin, "api/v1/auth/start.php"), null, cancellationToken);
        return await ReadJsonAsync<ReloadedAuthorizationStart>(response, cancellationToken);
    }

    public async Task<ReloadedAuthorizationPoll> PollAuthorizationAsync(
        string requestId,
        string pollToken,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            new Uri(origin, "api/v1/auth/poll.php"),
            new { request_id = requestId, poll_token = pollToken },
            cancellationToken);
        return await ReadJsonAsync<ReloadedAuthorizationPoll>(response, cancellationToken);
    }

    public async Task<ReloadedSessionResult> ValidateSessionAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            new Uri(origin, "api/v1/session.php"),
            new { access_token = accessToken },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new ReloadedSessionResult(false, 0, null);
        }

        return await ReadJsonAsync<ReloadedSessionResult>(response, cancellationToken);
    }

    private async Task<T> GetJsonAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(new Uri(origin, relativePath), cancellationToken);
        return await ReadJsonAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return value ?? throw new InvalidDataException("BNL Reloaded returned an empty or invalid response.");
    }
}

public sealed record ReloadedServiceStatus(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("api_version")] int ApiVersion,
    [property: JsonPropertyName("steam_openid")] bool SteamOpenId,
    [property: JsonPropertyName("build_available")] bool BuildAvailable);

public sealed record ReloadedAuthorizationStart(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("poll_token")] string PollToken,
    [property: JsonPropertyName("verification_uri")] string VerificationUri,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("poll_interval")] int PollInterval);

public sealed record ReloadedAuthorizationPoll(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("manifest")] JsonElement? Manifest);

public sealed record ReloadedSessionResult(
    [property: JsonPropertyName("authorized")] bool Authorized,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("manifest")] JsonElement? Manifest);
