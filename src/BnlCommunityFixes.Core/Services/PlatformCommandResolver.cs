namespace BnlCommunityFixes.Core.Services;

public static class PlatformCommandResolver
{
    public static string? ResolveFromPath(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var resolved = ResolveExecutableFromPath(candidate);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    public static string? ResolveExecutableFromPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        if (Path.IsPathRooted(fileName))
        {
            return File.Exists(fileName) ? fileName : null;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in GetExecutableExtensions())
            {
                var candidatePath = Path.Combine(directory.Trim(), fileName);
                if (!candidatePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    candidatePath += extension;
                }

                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetExecutableExtensions()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield return string.Empty;
            yield break;
        }

        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        if (!string.IsNullOrWhiteSpace(pathExt))
        {
            foreach (var extension in pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                yield return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
            }
        }

        yield return string.Empty;
        yield return ".exe";
        yield return ".cmd";
        yield return ".bat";
    }
}
