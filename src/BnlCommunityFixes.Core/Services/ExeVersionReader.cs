using System.Diagnostics;
using System.Reflection;

namespace BnlCommunityFixes.Core.Services;

/// <summary>
/// Reads the version of an executable in a cross-platform way.
/// FileVersionInfo.GetVersionInfo works on Windows PE files; on Linux ELF binaries
/// it returns null. Falls back to the embedded AssemblyInformationalVersion attribute.
/// </summary>
public static class ExeVersionReader
{
    public static string? GetVersion(string exePath)
    {
        // FileVersionInfo works on Windows for PE files and on Linux for managed .NET assemblies
        // but returns null for self-contained single-file exes on Linux.
        var fvi = FileVersionInfo.GetVersionInfo(exePath);
        if (!string.IsNullOrWhiteSpace(fvi.FileVersion))
            return fvi.FileVersion;

        // Fallback: try loading as a .NET assembly and reading the informational version attribute
        try
        {
            var asm = Assembly.LoadFrom(exePath);
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!string.IsNullOrWhiteSpace(attr?.InformationalVersion))
            {
                // Strip any commit hash suffix (e.g. "2.5.2+abc123" → "2.5.2")
                var version = attr.InformationalVersion;
                var plus = version.IndexOf('+');
                return plus > 0 ? version[..plus] : version;
            }
        }
        catch { }

        return null;
    }
}
