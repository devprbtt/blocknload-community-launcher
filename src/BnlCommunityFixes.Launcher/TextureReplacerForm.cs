using System.Text;
using System.Text.Json;
using System.Threading;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class TextureReplacerForm : Form
{
    private readonly string configPath;
    private readonly string mappingPath;
    private readonly string customTextureFolder;
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

    public TextureReplacerForm(AppPaths paths, GameInstallInfo? installInfo = null)
    {
        configPath = Path.Combine(paths.PatchingDir, "experimental-texture-replacer-config.json");
        mappingPath = Path.Combine(paths.PatchingDir, "texture-replacements.txt");

        customTextureFolder = installInfo?.IsDetected == true
            ? Path.Combine(installInfo.GameRoot, "Win64", "BlockNLoad_Data", "CustomTextures")
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        Text = "Texture Replacer";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(620, 380);

        using var iconStream = typeof(TextureReplacerForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new Icon(iconStream);

        var infoLabel = new Label
        {
            Text = "Map in-game texture or sprite names to custom image files placed in the CustomTextures folder. Changes apply on next launch.",
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

        var textureNameCol = new DataGridViewTextBoxColumn
        {
            Name = "TextureName",
            HeaderText = "In-game texture / sprite name",
            MinimumWidth = 220
        };
        var fileCol = new DataGridViewTextBoxColumn
        {
            Name = "FileName",
            HeaderText = "Replacement image file",
            MinimumWidth = 320
        };
        grid.Columns.Add(textureNameCol);
        grid.Columns.Add(fileCol);

        addButton = new Button { Text = "Add", Location = new Point(12, 302), Size = new Size(80, 28) };
        removeButton = new Button { Text = "Remove", Location = new Point(98, 302), Size = new Size(80, 28) };
        browseButton = new Button { Text = "Browse image...", Location = new Point(184, 302), Size = new Size(110, 28) };
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
                    Title = "Select Texture Image",
                    Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All Files (*.*)|*.*",
                    InitialDirectory = Directory.Exists(customTextureFolder) ? customTextureFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                    picked = dialog.FileName;
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (picked == null) return;

            Directory.CreateDirectory(customTextureFolder);
            string fileName = Path.GetFileName(picked);
            string destPath = Path.Combine(customTextureFolder, fileName);

            string pickedFull = Path.GetFullPath(picked);
            string customFull = Path.GetFullPath(customTextureFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string displayValue;
            if (pickedFull.StartsWith(customFull, StringComparison.OrdinalIgnoreCase))
            {
                displayValue = pickedFull.Substring(customFull.Length);
            }
            else
            {
                CopyTextureFileToCustomFolder(picked, destPath);
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
                Description = "Select a folder with texture images to import",
                SelectedPath = Directory.Exists(customTextureFolder)
                    ? customTextureFolder
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
                MessageBox.Show("Failed to import texture folder:\n" + ex.Message,
                    "Texture Replacer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            if (root.TryGetProperty("textures", out var textures))
            {
                foreach (var kvp in textures.EnumerateObject())
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
        var textures = new Dictionary<string, string>();

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            string textureName = (row.Cells[0].Value as string)?.Trim() ?? "";
            string fileName = (row.Cells[1].Value as string)?.Trim() ?? "";
            if (!string.IsNullOrEmpty(textureName) && !string.IsNullOrEmpty(fileName))
                textures[textureName] = fileName;
        }

        var config = new Dictionary<string, object>
        {
            ["enabled"] = textures.Count > 0,
            ["textures"] = textures
        };

        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, json);

        var lines = textures
            .OrderBy(static kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static kvp => kvp.Key + "=" + kvp.Value)
            .ToArray();

        if (lines.Length == 0)
        {
            File.Delete(mappingPath);
        }
        else
        {
            File.WriteAllLines(mappingPath, lines, new UTF8Encoding(false));
        }
    }

    private static void CopyTextureFileToCustomFolder(string sourcePath, string destinationPath)
    {
        string normalizedSource = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedDestination = Path.GetFullPath(destinationPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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
                using FileStream source = new FileStream(normalizedSource, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using FileStream destination = new FileStream(normalizedDestination, FileMode.Create, FileAccess.Write, FileShare.None);
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

    private void ImportFolderMappings(string selectedFolder)
    {
        Directory.CreateDirectory(customTextureFolder);

        string selectedFull = Path.GetFullPath(selectedFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string customFull = Path.GetFullPath(customTextureFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string customPrefix = customFull + Path.DirectorySeparatorChar;
        var rows = GetCurrentMappings();

        foreach (var filePath in Directory.EnumerateFiles(selectedFull, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(filePath);
            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(filePath);
            string relativePath;
            if (fullPath.StartsWith(customPrefix, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = fullPath.Substring(customPrefix.Length);
            }
            else
            {
                string relativeFromSelected = Path.GetRelativePath(selectedFull, fullPath);
                string destinationPath = Path.Combine(customTextureFolder, relativeFromSelected);
                CopyTextureFileToCustomFolder(fullPath, destinationPath);
                relativePath = relativeFromSelected;
            }

            string textureName = Path.GetFileNameWithoutExtension(relativePath);
            if (string.IsNullOrWhiteSpace(textureName))
                continue;

            rows[textureName] = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        ApplyMappings(rows);
    }

    private Dictionary<string, string> GetCurrentMappings()
    {
        var rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;

            string textureName = (row.Cells[0].Value as string)?.Trim() ?? "";
            string fileName = (row.Cells[1].Value as string)?.Trim() ?? "";
            if (!string.IsNullOrEmpty(textureName) && !string.IsNullOrEmpty(fileName))
                rows[textureName] = fileName;
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
