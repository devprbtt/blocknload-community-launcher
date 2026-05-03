using System.Reflection;

namespace BnlCommunityFixes.Launcher;

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
}
