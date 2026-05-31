namespace BnlCommunityFixes.Core.Services;

public static class PlatformPathNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var path = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        path = ExpandHome(path);

        if (OperatingSystem.IsWindows())
        {
            path = path.Replace('/', Path.DirectorySeparatorChar);
        }
        else
        {
            path = path.Replace('\\', Path.DirectorySeparatorChar);
        }

        return Path.GetFullPath(path);
    }

    private static string ExpandHome(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path[0] != '~')
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return path;
        }

        if (path.Length == 1)
        {
            return home;
        }

        var next = path[1];
        if (next != '/' && next != '\\')
        {
            return path;
        }

        return Path.Combine(home, path[2..]);
    }
}
