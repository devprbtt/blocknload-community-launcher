using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BnlCommunityFixes.Core.Features;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed partial class ConfigTransferViewModel : ViewModelBase
{
    private readonly AppPaths _paths;

    public ObservableCollection<FeatureItem> Features { get; }

    public Func<Task<string?>>? PickSaveFile { get; set; }
    public Func<Task<string?>>? PickOpenFile { get; set; }
    public Action<string, string>? Notify { get; set; }
    public Action<string, string>? ErrorOccurred { get; set; }
    public Func<string, Task<bool>>? Confirm { get; set; }

    public ConfigTransferViewModel(AppPaths paths)
    {
        _paths = paths;
        Features = new ObservableCollection<FeatureItem>(
            FeatureConfigCatalog.ConfigTransferFeatures
                .Select(static f => new FeatureItem(f.DisplayName, f.FileName, isChecked: true)));
    }

    [RelayCommand] private void SelectAll() { foreach (var f in Features) f.IsChecked = true; }
    [RelayCommand] private void SelectNone() { foreach (var f in Features) f.IsChecked = false; }

    [RelayCommand]
    private async Task Export()
    {
        var selected = Features.Where(static f => f.IsChecked).ToArray();
        if (selected.Length == 0) { Notify?.Invoke("Export", "No features selected."); return; }

        var fileName = PickSaveFile is not null ? await PickSaveFile() : null;
        if (fileName is null) return;

        try
        {
            if (File.Exists(fileName)) File.Delete(fileName);
            using var zip = ZipFile.Open(fileName, ZipArchiveMode.Create);
            int exported = 0;
            foreach (var f in selected)
            {
                var src = Path.Combine(_paths.PatchingDir, f.FileName);
                if (!File.Exists(src)) continue;
                zip.CreateEntryFromFile(src, f.FileName, CompressionLevel.SmallestSize);
                exported++;
            }
            Notify?.Invoke("Export complete", $"Exported {exported} of {selected.Length} selected feature config(s).\n\n(Features without an existing config file were skipped.)");
        }
        catch (Exception ex) { ErrorOccurred?.Invoke("Export failed", ex.Message); }
    }

    [RelayCommand]
    private async Task Import()
    {
        var selected = Features.Where(static f => f.IsChecked).ToArray();
        if (selected.Length == 0) { Notify?.Invoke("Import", "No features selected."); return; }

        var fileName = PickOpenFile is not null ? await PickOpenFile() : null;
        if (fileName is null) return;

        try
        {
            var selectedNames = new HashSet<string>(selected.Select(static f => f.FileName), StringComparer.OrdinalIgnoreCase);
            using var zip = ZipFile.OpenRead(fileName);
            var matched = zip.Entries.Where(e => selectedNames.Contains(e.Name)).ToList();

            if (matched.Count == 0) { Notify?.Invoke("Import", "The bundle contains no matching configs for the selected features."); return; }

            var validated = new List<(ZipArchiveEntry entry, string json)>();
            foreach (var entry in matched)
            {
                using var stream = entry.Open();
                var json = await new StreamReader(stream).ReadToEndAsync();
                try { JsonDocument.Parse(json); }
                catch (JsonException ex) { ErrorOccurred?.Invoke("Invalid JSON", $"'{entry.Name}' is not valid JSON:\n{ex.Message}"); return; }
                validated.Add((entry, json));
            }

            var names = string.Join("\n", validated.Select(v => "  • " + (Features.FirstOrDefault(f => string.Equals(f.FileName, v.entry.Name, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? v.entry.Name)));
            if (Confirm is not null && !await Confirm($"This will overwrite {validated.Count} config file(s):\n\n{names}\n\nContinue?"))
                return;

            Directory.CreateDirectory(_paths.PatchingDir);
            foreach (var (entry, json) in validated)
                File.WriteAllText(Path.Combine(_paths.PatchingDir, entry.Name), json, new System.Text.UTF8Encoding(false));

            Notify?.Invoke("Import complete", $"Imported {validated.Count} config file(s).\n\nReopen Feature Settings to see the changes.");
        }
        catch (Exception ex) { ErrorOccurred?.Invoke("Import failed", ex.Message); }
    }

    public sealed partial class FeatureItem(string displayName, string fileName, bool isChecked) : ObservableObject
    {
        public string DisplayName { get; } = displayName;
        public string FileName { get; } = fileName;
        [ObservableProperty] private bool _isChecked = isChecked;
    }
}
