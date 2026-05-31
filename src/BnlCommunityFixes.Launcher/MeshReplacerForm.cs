using System.Data;
using System.Text.Json;
using System.Threading;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class MeshReplacerForm : Form
{
    private readonly string configPath;
    private readonly string customMeshFolder;
    private readonly DataGridView grid;
    private readonly Button addButton;
    private readonly Button removeButton;
    private readonly Button browseButton;
    private readonly Button importFolderButton;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public MeshReplacerForm(AppPaths paths, GameInstallInfo? installInfo = null)
    {
        configPath = Path.Combine(paths.PatchingDir, "experimental-mesh-replacer-config.json");

        customMeshFolder = installInfo?.IsDetected == true
            ? installInfo.CustomMeshesDirectoryPath
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        Text = "Mesh Replacer";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(620, 380);

        using var iconStream = typeof(MeshReplacerForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new System.Drawing.Icon(iconStream);

        var infoLabel = new Label
        {
            Text = "Map in-game mesh names to custom .obj files placed in the CustomMeshes folder. Changes apply on next launch.",
            AutoSize = false,
            Size = new Size(590, 32),
            Location = new Point(12, 12)
        };

        grid = new DataGridView
        {
            Location = new Point(12, 52),
            Size = new Size(592, 240),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText
        };

        var meshNameCol = new DataGridViewTextBoxColumn
        {
            Name = "MeshName",
            HeaderText = "In-game mesh name",
            MinimumWidth = 220
        };
        var fileCol = new DataGridViewTextBoxColumn
        {
            Name = "FileName",
            HeaderText = "Replacement .obj file",
            MinimumWidth = 320
        };
        grid.Columns.Add(meshNameCol);
        grid.Columns.Add(fileCol);

        addButton = new Button { Text = "Add", Location = new Point(12, 302), Size = new Size(80, 28) };
        removeButton = new Button { Text = "Remove", Location = new Point(98, 302), Size = new Size(80, 28) };
        browseButton = new Button { Text = "Browse .obj...", Location = new Point(184, 302), Size = new Size(110, 28) };
        importFolderButton = new Button { Text = "Import folder...", Location = new Point(300, 302), Size = new Size(120, 28) };

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
                    Title = "Select OBJ Mesh File",
                    Filter = "OBJ Mesh Files (*.obj)|*.obj|All Files (*.*)|*.*",
                    InitialDirectory = Directory.Exists(customMeshFolder) ? customMeshFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                    picked = dialog.FileName;
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (picked == null) return;

            Directory.CreateDirectory(customMeshFolder);
            string fileName = Path.GetFileName(picked);
            string destPath = Path.Combine(customMeshFolder, fileName);

            // Compute relative path from CustomMeshes root (supports subfolders)
            string pickedFull = Path.GetFullPath(picked);
            string customFull = Path.GetFullPath(customMeshFolder) + Path.DirectorySeparatorChar;
            string displayValue;
            if (pickedFull.StartsWith(customFull, StringComparison.OrdinalIgnoreCase))
            {
                // File is already inside CustomMeshes — use relative path, skip copy
                displayValue = pickedFull.Substring(customFull.Length);
            }
            else
            {
                // File is outside CustomMeshes — copy it to the root and use bare filename
                try
                {
                    File.Copy(picked, destPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to copy file to CustomMeshes folder:\n" + ex.Message,
                        "Mesh Replacer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                displayValue = fileName;
            }

            if (grid.SelectedRows.Count > 0)
                grid.SelectedRows[0].Cells[1].Value = displayValue;
            else if (grid.Rows.Count > 0)
                grid.Rows[grid.Rows.Count - 1].Cells[1].Value = displayValue;
        };

        importFolderButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder with OBJ meshes to import",
                SelectedPath = Directory.Exists(customMeshFolder)
                    ? customMeshFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                return;

            try
            {
                ImportFolderMappings(dialog.SelectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to import mesh folder:\n" + ex.Message,
                    "Mesh Replacer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(430, 338),
            Size = new Size(80, 28),
            DialogResult = DialogResult.OK
        };
        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(516, 338),
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel
        };

        okButton.Click += (_, _) => SaveConfig();

        Controls.AddRange([
            infoLabel, grid,
            addButton, removeButton, browseButton, importFolderButton,
            okButton, cancelButton
        ]);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        LoadConfig();
        AutoImportNewFiles();
    }

    private void AutoImportNewFiles()
    {
        if (!Directory.Exists(customMeshFolder)) return;

        var rows = GetCurrentMappings();
        bool added = false;

        string customFull = Path.GetFullPath(customMeshFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var filePath in Directory.EnumerateFiles(customMeshFolder, "*.obj", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(filePath);
            string relativePath = fullPath.StartsWith(customFull, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(customFull.Length)
                : Path.GetFileName(fullPath);

            string meshName = Path.GetFileNameWithoutExtension(relativePath);
            if (string.IsNullOrWhiteSpace(meshName)) continue;
            if (rows.ContainsKey(meshName)) continue;

            rows[meshName] = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            added = true;
        }

        if (added)
            ApplyMappings(rows);
    }

    private void LoadConfig()
    {
        try
        {
            if (!File.Exists(configPath)) return;

            string json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            grid.Rows.Clear();

            if (root.TryGetProperty("meshes", out var meshes))
            {
                foreach (var kvp in meshes.EnumerateObject())
                    grid.Rows.Add(kvp.Name, kvp.Value.GetString() ?? "");
            }
        }
        catch
        {
            grid.Rows.Clear();
        }
    }

    private void SaveConfig()
    {
        var meshes = new Dictionary<string, string>();

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            string meshName = (row.Cells[0].Value as string)?.Trim() ?? "";
            string fileName = (row.Cells[1].Value as string)?.Trim() ?? "";
            if (!string.IsNullOrEmpty(meshName) && !string.IsNullOrEmpty(fileName))
                meshes[meshName] = fileName;
        }

        var config = new Dictionary<string, object>
        {
            ["enabled"] = meshes.Count > 0,
            ["meshes"] = meshes
        };

        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, json);
    }

    private void ImportFolderMappings(string selectedFolder)
    {
        Directory.CreateDirectory(customMeshFolder);

        string selectedFull = Path.GetFullPath(selectedFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string customFull = Path.GetFullPath(customMeshFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string customPrefix = customFull + Path.DirectorySeparatorChar;
        var rows = GetCurrentMappings();

        foreach (var filePath in Directory.EnumerateFiles(selectedFull, "*.obj", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(filePath);
            string relativePath;
            if (fullPath.StartsWith(customPrefix, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = fullPath.Substring(customPrefix.Length);
            }
            else
            {
                string relativeFromSelected = Path.GetRelativePath(selectedFull, fullPath);
                string destinationPath = Path.Combine(customMeshFolder, relativeFromSelected);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(fullPath, destinationPath, true);
                relativePath = relativeFromSelected;
            }

            string meshName = Path.GetFileNameWithoutExtension(relativePath);
            if (string.IsNullOrWhiteSpace(meshName))
                continue;

            rows[meshName] = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        ApplyMappings(rows);
    }

    private Dictionary<string, string> GetCurrentMappings()
    {
        var rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;

            string meshName = (row.Cells[0].Value as string)?.Trim() ?? "";
            string fileName = (row.Cells[1].Value as string)?.Trim() ?? "";
            if (!string.IsNullOrEmpty(meshName) && !string.IsNullOrEmpty(fileName))
                rows[meshName] = fileName;
        }

        return rows;
    }

    private void ApplyMappings(Dictionary<string, string> rows)
    {
        grid.Rows.Clear();
        foreach (var kvp in rows.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            grid.Rows.Add(kvp.Key, kvp.Value);
    }
}
