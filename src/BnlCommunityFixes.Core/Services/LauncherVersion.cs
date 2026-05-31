using System.Diagnostics;
using System.Reflection;

namespace BnlCommunityFixes.Core.Services;

public static class LauncherVersion
{
    public static string GetCurrentVersion()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(processPath);
            if (!string.IsNullOrWhiteSpace(fileVersion.ProductVersion))
            {
                return fileVersion.ProductVersion;
            }

            if (!string.IsNullOrWhiteSpace(fileVersion.FileVersion))
            {
                return fileVersion.FileVersion;
            }
        }

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
