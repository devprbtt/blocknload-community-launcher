using System.Diagnostics;

namespace BnlCommunityFixes.Core.Services;

public static class PlatformShell
{
    private static readonly string[] LinuxOpenCandidates =
    [
        "xdg-open",
        "gio",
        "sensible-open",
        "gvfs-open",
        "kde-open",
        "kioclient5",
        "kioclient",
        "exo-open"
    ];

    public static void OpenPath(string path)
    {
        Process.Start(CreateOpenPathStartInfo(path));
    }

    public static void RevealPath(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { $"/select,{path}" },
                UseShellExecute = true
            });
            return;
        }

        var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
        OpenPath(directory);
    }

    private static ProcessStartInfo CreateOpenPathStartInfo(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo { FileName = path, UseShellExecute = true };
        }

        if (OperatingSystem.IsMacOS())
        {
            return new ProcessStartInfo { FileName = "open", ArgumentList = { path }, UseShellExecute = false };
        }

        var opener = PlatformCommandResolver.ResolveFromPath(LinuxOpenCandidates)
            ?? throw new InvalidOperationException("No supported Linux opener found. Install xdg-open or equivalent.");

        var openerName = Path.GetFileName(opener);
        if (string.Equals(openerName, "gio", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(openerName, "kioclient5", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(openerName, "kioclient", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo { FileName = opener, ArgumentList = { "open", path }, UseShellExecute = false };
        }

        return new ProcessStartInfo { FileName = opener, ArgumentList = { path }, UseShellExecute = false };
    }
}
