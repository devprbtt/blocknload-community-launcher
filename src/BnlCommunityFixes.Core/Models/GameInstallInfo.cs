namespace BnlCommunityFixes.Core.Models;

public sealed class GameInstallInfo
{
    public bool IsDetected { get; init; }

    public string DetectionSource { get; init; } = "";

    public string SteamPath { get; init; } = "";

    public string GameRoot { get; init; } = "";

    public string GameExecutablePath { get; init; } = "";
    public string Win64DirectoryPath { get; init; } = "";
    public string GameDataDirectoryPath { get; init; } = "";

    public string ServersFilePath { get; init; } = "";

    public string ManagedDirectoryPath { get; init; } = "";
    public string ReplaysDirectoryPath { get; init; } = "";
    public string CustomAudioDirectoryPath { get; init; } = "";
    public string CustomMeshesDirectoryPath { get; init; } = "";
    public string CustomTexturesDirectoryPath { get; init; } = "";

    public string FailureReason { get; init; } = "";
}
