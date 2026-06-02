using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BnlCommunityFixes.Avalonia.ViewModels;

/// <summary>
/// Generic ViewModel for Audio/Mesh/Texture replacer windows.
/// Each has a DataGrid of (SourceKey → TargetFile) mappings and Add/Remove/Browse buttons.
/// </summary>
public abstract partial class ReplacerViewModel : ViewModelBase
{
    protected readonly string ConfigPath;
    protected readonly string CustomFolder;
    private bool _suppressAutoSave = true;

    [ObservableProperty] private ObservableCollection<ReplacerRow> _rows = [];
    [ObservableProperty] private ReplacerRow? _selectedRow;
    [ObservableProperty] private bool _isFeatureEnabled;

    public Func<string, Task<string?>>? PickFile { get; set; }
    public Func<string, Task<string?>>? PickFolder { get; set; }
    public Action<string, string>? ErrorOccurred { get; set; }

    protected ReplacerViewModel(string configPath, string customFolder)
    {
        ConfigPath = configPath;
        CustomFolder = customFolder;
        Load();
        _suppressAutoSave = false;
    }

    public abstract string Title { get; }
    public abstract string InfoText { get; }
    public abstract string SourceColumnHeader { get; }
    public abstract string TargetColumnHeader { get; }
    public abstract string FileFilter { get; }
    protected virtual IReadOnlyList<string> AllowedExtensions => ParseAllowedExtensions(FileFilter);
    protected abstract void SaveRows(List<ReplacerRow> rows);

    protected virtual void Load() { }

    partial void OnIsFeatureEnabledChanged(bool value)
    {
        if (_suppressAutoSave)
        {
            return;
        }

        Save();
    }

    [RelayCommand]
    private void Add()
    {
        var row = new ReplacerRow("", "");
        Rows.Add(row);
        SelectedRow = row;
    }

    [RelayCommand]
    private void Remove()
    {
        if (SelectedRow is not null)
            Rows.Remove(SelectedRow);
        Save();
    }

    [RelayCommand]
    private async Task Browse()
    {
        if (SelectedRow is null || PickFile is null) return;
        var file = await PickFile(FileFilter);
        if (file is not null)
        {
            SelectedRow.TargetFile = file;
            Save();
        }
    }

    [RelayCommand]
    private async Task ImportFolder()
    {
        if (PickFolder is null) return;
        var selectedFolder = await PickFolder(FileFilter);
        if (string.IsNullOrWhiteSpace(selectedFolder) || !Directory.Exists(selectedFolder)) return;

        try
        {
            Directory.CreateDirectory(CustomFolder);

            string selectedFull = Path.GetFullPath(selectedFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string customFull = Path.GetFullPath(CustomFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string customPrefix = customFull + Path.DirectorySeparatorChar;
            var rows = GetCurrentMappings();

            foreach (var filePath in Directory.EnumerateFiles(selectedFull, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(filePath);
                if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    continue;

                string fullPath = Path.GetFullPath(filePath);
                string relativePath;
                if (fullPath.StartsWith(customPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = fullPath.Substring(customPrefix.Length);
                }
                else
                {
                    string relativeFromSelected = Path.GetRelativePath(selectedFull, fullPath);
                    string destinationPath = Path.Combine(CustomFolder, relativeFromSelected);
                    CopyFile(fullPath, destinationPath);
                    relativePath = relativeFromSelected;
                }

                string sourceKey = Path.GetFileNameWithoutExtension(relativePath);
                if (string.IsNullOrWhiteSpace(sourceKey))
                    continue;

                rows[sourceKey] = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            ApplyMappings(rows);
            Save();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke("Import folder failed", ex.Message);
        }
    }

    public void Save()
    {
        try { SaveRows(Rows.ToList()); }
        catch (Exception ex) { ErrorOccurred?.Invoke("Save failed", ex.Message); }
    }

    protected bool ReadEnabledFlag(JsonElement root, bool defaultValue)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("enabled", out var prop) &&
            (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False))
        {
            return prop.GetBoolean();
        }

        return defaultValue;
    }

    protected static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private Dictionary<string, string> GetCurrentMappings()
    {
        var rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows)
        {
            var sourceKey = row.SourceKey?.Trim() ?? string.Empty;
            var targetFile = row.TargetFile?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(sourceKey) && !string.IsNullOrEmpty(targetFile))
            {
                rows[sourceKey] = targetFile;
            }
        }

        return rows;
    }

    private void ApplyMappings(Dictionary<string, string> rows)
    {
        Rows.Clear();
        foreach (var kvp in rows.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            Rows.Add(new ReplacerRow(kvp.Key, kvp.Value));
        }
    }

    private static IReadOnlyList<string> ParseAllowedExtensions(string filter)
    {
        var parts = filter.Split('|');
        if (parts.Length < 2)
            return [];

        return parts[1]
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static pattern => pattern.Trim())
            .Where(static pattern => pattern.StartsWith("*.", StringComparison.Ordinal))
            .Select(static pattern => pattern[1..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void CopyFile(string sourcePath, string destinationPath)
    {
        string normalizedSource = Path.GetFullPath(sourcePath);
        string normalizedDestination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedDestination)!);

        if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using FileStream source = new(normalizedSource, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using FileStream destination = new(normalizedDestination, FileMode.Create, FileAccess.Write, FileShare.None);
                source.CopyTo(destination);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
        }

        File.Copy(normalizedSource, normalizedDestination, true);
    }

    public sealed partial class ReplacerRow : ObservableObject
    {
        public ReplacerRow(string sourceKey, string targetFile) { SourceKey = sourceKey; TargetFile = targetFile; }
        [ObservableProperty] private string _sourceKey;
        [ObservableProperty] private string _targetFile;
    }
}
