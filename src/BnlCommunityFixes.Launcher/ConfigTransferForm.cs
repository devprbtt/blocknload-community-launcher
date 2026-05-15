using System.IO.Compression;
using System.Text.Json;
using System.Windows.Forms;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

// Modal dialog for importing or exporting feature config bundles.
// A bundle is a zip archive containing one JSON file per feature.
public sealed class ConfigTransferForm : Form
{
    private readonly AppPaths paths;

    private static readonly FeatureEntry[] Features =
    [
        new("Crosshair",         "crosshair-config.json"),
        new("FOV / ADS",         "fov-config.json"),
        new("Team Colors",       "experimental-team-color-config.json"),
        new("Damage / Healing",  "damage-healing-indicator-config.json"),
        new("Heal Alerts",       "heal-alert-indicator-config.json"),
        new("Objective Beam",    "experimental-base-objective-beam-config.json"),
        new("Shield Timer",      "experimental-enemy-shield-buffbar-config.json"),
        new("Build Preview",     "experimental-local-build-preview-config.json"),
        new("Aim Healthbar",     "aim-healthbar-config.json"),
        new("Death Cam HP",      "deathcam-healthbar-config.json"),
        new("Auto Queue",        "experimental-auto-casual-queue-config.json"),
        new("Low HP Alert",      "friendly-low-health-config.json"),
        new("Misc",              "experimental-debug-menu-config.json"),
    ];

    private readonly CheckedListBox featureList;
    private readonly Button importButton;
    private readonly Button exportButton;
    private readonly Button closeButton;
    private readonly Button selectAllButton;
    private readonly Button selectNoneButton;

    public ConfigTransferForm(AppPaths paths)
    {
        this.paths = paths;

        Text = "Import / Export Configs";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new System.Drawing.Size(420, 460);

        using var iconStream = typeof(FeatureSettingsForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new System.Drawing.Icon(iconStream);

        var headerLabel = new Label
        {
            Text = "Select which features to include in the import or export:",
            AutoSize = false,
            Size = new System.Drawing.Size(390, 32),
            Location = new System.Drawing.Point(14, 10)
        };

        selectAllButton = new Button
        {
            Text = "Select All",
            Location = new System.Drawing.Point(14, 44),
            Size = new System.Drawing.Size(88, 23)
        };
        selectAllButton.Click += (_, _) => SetAllChecked(true);

        selectNoneButton = new Button
        {
            Text = "Select None",
            Location = new System.Drawing.Point(108, 44),
            Size = new System.Drawing.Size(88, 23)
        };
        selectNoneButton.Click += (_, _) => SetAllChecked(false);

        featureList = new CheckedListBox
        {
            Location = new System.Drawing.Point(14, 74),
            Size = new System.Drawing.Size(390, 310),
            CheckOnClick = true
        };
        foreach (var f in Features)
            featureList.Items.Add(f.DisplayName, isChecked: true);

        importButton = new Button
        {
            Text = "Import...",
            Location = new System.Drawing.Point(14, 400),
            Size = new System.Drawing.Size(110, 32)
        };
        importButton.Click += (_, _) => DoImport();

        exportButton = new Button
        {
            Text = "Export...",
            Location = new System.Drawing.Point(132, 400),
            Size = new System.Drawing.Size(110, 32)
        };
        exportButton.Click += (_, _) => DoExport();

        closeButton = new Button
        {
            Text = "Close",
            Location = new System.Drawing.Point(294, 400),
            Size = new System.Drawing.Size(110, 32)
        };
        closeButton.Click += (_, _) => Close();

        Controls.AddRange([headerLabel, selectAllButton, selectNoneButton, featureList, importButton, exportButton, closeButton]);
    }

    private void SetAllChecked(bool value)
    {
        for (int i = 0; i < featureList.Items.Count; i++)
            featureList.SetItemChecked(i, value);
    }

    private FeatureEntry[] GetSelectedFeatures()
    {
        var result = new List<FeatureEntry>();
        for (int i = 0; i < featureList.Items.Count; i++)
            if (featureList.GetItemChecked(i))
                result.Add(Features[i]);
        return [.. result];
    }

    private static string? PickSaveFile()
    {
        string? result = null;
        var t = new Thread(() =>
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Export config bundle",
                Filter = "Config bundle (*.zip)|*.zip",
                DefaultExt = "zip",
                FileName = "bnl-config-bundle.zip"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                result = dlg.FileName;
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        return result;
    }

    private static string? PickOpenFile()
    {
        string? result = null;
        var t = new Thread(() =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Import config bundle",
                Filter = "Config bundle (*.zip)|*.zip"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                result = dlg.FileName;
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        return result;
    }

    private void DoExport()
    {
        var selected = GetSelectedFeatures();
        if (selected.Length == 0)
        {
            MessageBox.Show("No features selected.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var fileName = PickSaveFile();
        if (fileName == null) return;

        try
        {
            if (File.Exists(fileName)) File.Delete(fileName);
            using var zip = ZipFile.Open(fileName, ZipArchiveMode.Create);
            int exported = 0;
            foreach (var f in selected)
            {
                var src = Path.Combine(paths.PatchingDir, f.FileName);
                if (!File.Exists(src)) continue;
                zip.CreateEntryFromFile(src, f.FileName, CompressionLevel.SmallestSize);
                exported++;
            }
            MessageBox.Show(
                $"Exported {exported} of {selected.Length} selected feature config(s).\n\n" +
                $"(Features without an existing config file were skipped.)",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DoImport()
    {
        var selected = GetSelectedFeatures();
        if (selected.Length == 0)
        {
            MessageBox.Show("No features selected.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var fileName = PickOpenFile();
        if (fileName == null) return;

        try
        {
            var selectedFileNames = new HashSet<string>(selected.Select(f => f.FileName), StringComparer.OrdinalIgnoreCase);

            using var zip = ZipFile.OpenRead(fileName);
            var matched = zip.Entries
                .Where(e => selectedFileNames.Contains(e.Name))
                .ToList();

            if (matched.Count == 0)
            {
                MessageBox.Show(
                    "The selected bundle contains no matching configs for the features you chose.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate JSON before writing anything
            var validated = new List<(ZipArchiveEntry entry, string json)>();
            foreach (var entry in matched)
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                try { JsonDocument.Parse(json); }
                catch (JsonException ex)
                {
                    MessageBox.Show(
                        $"The file '{entry.Name}' in the bundle is not valid JSON:\n{ex.Message}",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                validated.Add((entry, json));
            }

            var confirm = MessageBox.Show(
                $"This will overwrite {validated.Count} config file(s):\n\n" +
                string.Join("\n", validated.Select(v => "  • " + FeatureNameFor(v.entry.Name))) +
                "\n\nContinue?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            Directory.CreateDirectory(paths.PatchingDir);
            foreach (var (entry, json) in validated)
            {
                var dest = Path.Combine(paths.PatchingDir, entry.Name);
                File.WriteAllText(dest, json, new System.Text.UTF8Encoding(false));
            }

            MessageBox.Show(
                $"Imported {validated.Count} config file(s) successfully.\n\nReopen Feature Settings to see the changes.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string FeatureNameFor(string fileName)
    {
        var feature = Features.FirstOrDefault(f => string.Equals(f.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return feature?.DisplayName ?? fileName;
    }

    private sealed record FeatureEntry(string DisplayName, string FileName);
}
