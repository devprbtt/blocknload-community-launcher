using System.Reflection;

namespace BnlCommunityFixes.Core.Services;

public static class LauncherVersion
{
    public static string GetCurrentVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }

    public static string GetDisplayVersion()
    {
        var version = GetCurrentVersion();
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        var dashIndex = version.IndexOf('-');
        if (dashIndex >= 0)
        {
            version = version[..dashIndex];
        }

        return version;
    }
}
