namespace BnlCommunityFixes.Updater;

public sealed class UpdaterArguments
{
    public string TargetPath { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string? UpdaterTargetPath { get; set; }
    public string? UpdaterSourcePath { get; set; }
    public int ProcessId { get; set; }
    public bool Restart { get; set; }
    public string LogPath { get; set; } = "";
    public List<string> RestartArguments { get; } = [];
}
