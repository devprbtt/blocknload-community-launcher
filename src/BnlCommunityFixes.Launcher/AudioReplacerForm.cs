using System.Data;
using System.Text.Json;
using System.Threading;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class AudioReplacerForm : Form
{
    private readonly AppPaths paths;
    private readonly string configPath;
    private readonly string customAudioFolder;
    private readonly DataGridView grid;
    private readonly CheckBox logAllCheckBox;
    private readonly TrackBar volumeTrackBar;
    private readonly Label volumeLabel;
    private readonly Dictionary<string, int> perEventVolumes = new();
    private bool updatingSliderFromSelection;
    private readonly Button addButton;
    private readonly Button removeButton;
    private readonly Button browseButton;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AudioReplacerForm(AppPaths paths, GameInstallInfo? installInfo = null)
    {
        this.paths = paths;
        configPath = Path.Combine(paths.PatchingDir, "experimental-audio-replacer-config.json");

        // Resolve the CustomAudio folder from the game install path
        if (installInfo?.IsDetected == true)
        {
            customAudioFolder = Path.Combine(installInfo.GameRoot, "Win64", "BlockNLoad_Data", "CustomAudio");
        }
        else
        {
            customAudioFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        Text = "Audio Replacer";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 500);

        using var iconStream = typeof(AudioReplacerForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new Icon(iconStream);

        var infoLabel = new Label
        {
            Text = "Map Wwise game events to other events or custom .wav/.mp3 files. Changes apply on next launch.",
            AutoSize = false,
            Size = new Size(600, 20),
            Location = new Point(12, 12)
        };

        grid = new DataGridView
        {
            Location = new Point(12, 40),
            Size = new Size(616, 280),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        var eventCol = new DataGridViewTextBoxColumn
        {
            Name = "EventName",
            HeaderText = "Game Event (Wwise)",
            MinimumWidth = 220
        };
        var replCol = new DataGridViewTextBoxColumn
        {
            Name = "Replacement",
            HeaderText = "Replacement (event name or file.wav)",
            MinimumWidth = 320
        };
        grid.Columns.Add(eventCol);
        grid.Columns.Add(replCol);

        addButton = new Button { Text = "Add", Location = new Point(12, 330), Size = new Size(80, 28) };
        removeButton = new Button { Text = "Remove", Location = new Point(98, 330), Size = new Size(80, 28) };
        browseButton = new Button { Text = "Browse .wav...", Location = new Point(184, 330), Size = new Size(110, 28) };

        addButton.Click += (_, _) =>
        {
            grid.Rows.Add("", "");
            grid.CurrentCell = grid.Rows[grid.Rows.Count - 1].Cells[0];
            grid.BeginEdit(true);
        };

        removeButton.Click += (_, _) =>
        {
            if (grid.SelectedRows.Count > 0)
                grid.Rows.Remove(grid.SelectedRows[0]);
        };

        browseButton.Click += (_, _) =>
        {
            string? picked = null;
            var t = new Thread(() =>
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Select Audio File",
                    Filter = "Audio Files (*.wav;*.mp3;*.ogg)|*.wav;*.mp3;*.ogg|All Files (*.*)|*.*",
                    InitialDirectory = Directory.Exists(customAudioFolder) ? customAudioFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                    picked = dialog.FileName;
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (picked != null)
            {
                // Copy the file into the CustomAudio folder so the game can load it
                Directory.CreateDirectory(customAudioFolder);
                string fileName = Path.GetFileName(picked);
                string destPath = Path.Combine(customAudioFolder, fileName);
                try
                {
                    File.Copy(picked, destPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to copy file to CustomAudio folder:\n" + ex.Message,
                        "Audio Replacer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (grid.SelectedRows.Count > 0)
                {
                    grid.SelectedRows[0].Cells[1].Value = fileName;
                }
                else if (grid.Rows.Count > 0)
                {
                    grid.Rows[grid.Rows.Count - 1].Cells[1].Value = fileName;
                }
            }
        };

        logAllCheckBox = new CheckBox
        {
            Text = "Log all audio events to output_log.txt (for discovering event names)",
            AutoSize = true,
            Location = new Point(12, 410),
            Checked = true
        };

        volumeLabel = new Label
        {
            Text = "Volume: 100%",
            AutoSize = true,
            Location = new Point(12, 438)
        };

        volumeTrackBar = new TrackBar
        {
            Minimum = 0,
            Maximum = 200,
            Value = 100,
            TickFrequency = 25,
            Location = new Point(12, 454),
            Size = new Size(350, 28)
        };
        volumeTrackBar.ValueChanged += (_, _) =>
        {
            if (updatingSliderFromSelection) return;
            if (grid.SelectedRows.Count > 0)
            {
                var row = grid.SelectedRows[0];
                string eventName = (row.Cells[0].Value as string)?.Trim() ?? "";
                if (!string.IsNullOrEmpty(eventName))
                {
                    perEventVolumes[eventName] = volumeTrackBar.Value;
                }
            }
            volumeLabel.Text = "Volume: " + volumeTrackBar.Value + "%";
        };

        grid.SelectionChanged += (_, _) =>
        {
            if (grid.SelectedRows.Count > 0)
            {
                var row = grid.SelectedRows[0];
                string eventName = (row.Cells[0].Value as string)?.Trim() ?? "";
                int vol = perEventVolumes.TryGetValue(eventName, out var v) ? v : 100;
                updatingSliderFromSelection = true;
                volumeTrackBar.Value = vol;
                updatingSliderFromSelection = false;
                volumeLabel.Text = string.IsNullOrEmpty(eventName)
                    ? "Volume: 100%"
                    : "Volume for \"" + eventName + "\": " + vol + "%";
            }
        };

        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(460, 462),
            Size = new Size(80, 28),
            DialogResult = DialogResult.OK
        };
        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(546, 462),
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel
        };

        okButton.Click += (_, _) => SaveConfig();

        Controls.AddRange([
            infoLabel, grid,
            addButton, removeButton, browseButton,
            logAllCheckBox, volumeLabel, volumeTrackBar,
            okButton, cancelButton
        ]);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        LoadConfig();
    }

    private void LoadConfig()
    {
        logAllCheckBox.Checked = true;
        volumeTrackBar.Value = 100;

        try
        {
            if (!File.Exists(configPath))
            {
                return;
            }

            string json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("log_all_events", out var logProp))
                logAllCheckBox.Checked = logProp.GetBoolean();

            if (root.TryGetProperty("volume", out var volProp) && volProp.TryGetDouble(out var vol))
                volumeTrackBar.Value = (int)Math.Clamp(vol * 100.0, 0, 200);

            perEventVolumes.Clear();
            if (root.TryGetProperty("volumes", out var volMap))
            {
                foreach (var kvp in volMap.EnumerateObject())
                {
                    if (kvp.Value.TryGetInt32(out var v))
                        perEventVolumes[kvp.Name] = Math.Clamp(v, 0, 200);
                }
            }

            grid.Rows.Clear();

            if (root.TryGetProperty("replacements", out var repls))
            {
                foreach (var kvp in repls.EnumerateObject())
                {
                    grid.Rows.Add(kvp.Name, kvp.Value.GetString() ?? "");
                }
            }

            if (root.TryGetProperty("custom_audio", out var custom))
            {
                foreach (var kvp in custom.EnumerateObject())
                {
                    grid.Rows.Add(kvp.Name, kvp.Value.GetString() ?? "");
                }
            }
        }
        catch
        {
            // If config is corrupted, start fresh
            grid.Rows.Clear();
        }
    }

    private void SaveConfig()
    {
        var replacements = new Dictionary<string, string>();
        var customAudio = new Dictionary<string, string>();

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;

            string eventName = (row.Cells[0].Value as string)?.Trim() ?? "";
            string replacement = (row.Cells[1].Value as string)?.Trim() ?? "";

            if (string.IsNullOrEmpty(eventName) || string.IsNullOrEmpty(replacement))
                continue;

            // Auto-detect: if replacement has a file extension, treat as custom audio
            string ext = Path.GetExtension(replacement).ToLowerInvariant();
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg")
            {
                customAudio[eventName] = replacement;
            }
            else
            {
                replacements[eventName] = replacement;
            }
        }

        var config = new Dictionary<string, object>
        {
            ["enabled"] = true,
            ["log_all_events"] = logAllCheckBox.Checked,
            ["volume"] = volumeTrackBar.Value / 100.0,
            ["replacements"] = replacements,
            ["custom_audio"] = customAudio,
            ["volumes"] = perEventVolumes,
        };

        // Preserve ignored_events if they exist
        try
        {
            if (File.Exists(configPath))
            {
                string oldJson = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(oldJson);
                if (doc.RootElement.TryGetProperty("ignored_events", out var ignored))
                {
                    var ignoredList = new List<string>();
                    foreach (var item in ignored.EnumerateArray())
                        ignoredList.Add(item.GetString() ?? "");
                    config["ignored_events"] = ignoredList;
                }
            }
        }
        catch { }

        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, json);
    }
}
