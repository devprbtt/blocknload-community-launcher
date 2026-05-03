namespace BnlCommunityFixes.Launcher;

public sealed class UpdateCheckResult
{
    public bool ShouldExitForUpdate { get; set; }
    public bool UpdateAvailable { get; set; }
    public bool ForcedUpdate { get; set; }
    public string? RemoteVersion { get; set; }
}
