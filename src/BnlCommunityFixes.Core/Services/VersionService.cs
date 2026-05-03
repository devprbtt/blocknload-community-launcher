namespace BnlCommunityFixes.Core.Services;

public static class VersionService
{
    public static Version Parse(string? value)
    {
        var normalized = Normalize(value);
        if (!string.IsNullOrWhiteSpace(normalized) && Version.TryParse(normalized, out var version))
        {
            return version;
        }

        return new Version(0, 0, 0, 0);
    }

    public static bool IsRemoteNewer(string localVersion, string remoteVersion)
    {
        return Parse(remoteVersion) > Parse(localVersion);
    }

    public static bool IsBelowMinimum(string localVersion, string minimumVersion)
    {
        return Parse(localVersion) < Parse(minimumVersion);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        var plusIndex = normalized.IndexOf('+');
        if (plusIndex >= 0)
        {
            normalized = normalized[..plusIndex];
        }

        var dashIndex = normalized.IndexOf('-');
        if (dashIndex >= 0)
        {
            normalized = normalized[..dashIndex];
        }

        return normalized;
    }
}
