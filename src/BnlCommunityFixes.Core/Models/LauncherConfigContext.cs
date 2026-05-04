namespace BnlCommunityFixes.Core.Models;

public sealed class LauncherConfigContext
{
    public required string LauncherDirectoryPath { get; init; }

    public required string ConfigPath { get; init; }

    public required string CustomConfigPath { get; init; }

    public required string CacheDirectoryPath { get; init; }

    public required string MainCachePath { get; init; }
}
