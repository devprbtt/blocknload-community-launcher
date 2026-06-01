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
    public Func<string, Task<string[]?>>? PickFolder { get; set; }
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
        var files = await PickFolder(FileFilter);
        if (files is null) return;
        foreach (var f in files)
            Rows.Add(new ReplacerRow(Path.GetFileNameWithoutExtension(f), f));
        Save();
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

    public sealed partial class ReplacerRow : ObservableObject
    {
        public ReplacerRow(string sourceKey, string targetFile) { SourceKey = sourceKey; TargetFile = targetFile; }
        [ObservableProperty] private string _sourceKey;
        [ObservableProperty] private string _targetFile;
    }
}
