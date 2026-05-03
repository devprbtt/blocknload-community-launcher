namespace BnlCommunityFixes.Core.Services;

public static class VersionService
{
    public static Version Parse(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Version.TryParse(value, out var version))
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
}
