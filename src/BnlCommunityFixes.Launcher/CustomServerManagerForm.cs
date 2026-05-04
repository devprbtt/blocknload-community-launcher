using System.Windows.Forms;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class CustomServerManagerForm : Form
{
    private readonly Logger logger;
    private readonly GameInstallInfo installInfo;
    private readonly LauncherConfigService launcherConfigService;

    private readonly ListBox serverListBox;
    private readonly TextBox keyTextBox;
    private readonly TextBox nameTextBox;
    private readonly TextBox hostTextBox;
    private readonly NumericUpDown portNumeric;
    private readonly TextBox patchTextBox;
    private readonly Button saveButton;
    private readonly Button deleteButton;

    private LauncherConfig customConfig;

    public CustomServerManagerForm(Logger logger, GameInstallInfo installInfo, LauncherConfigService launcherConfigService)
    {
        this.logger = logger;
        this.installInfo = installInfo;
        this.launcherConfigService = launcherConfigService;
        customConfig = launcherConfigService.LoadCustomConfig(installInfo, logger);

        Text = "Custom Servers";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new System.Drawing.Size(720, 360);

        serverListBox = new ListBox
        {
            Location = new System.Drawing.Point(20, 20),
            Size = new System.Drawing.Size(260, 280)
        };
        serverListBox.SelectedIndexChanged += (_, _) => LoadSelectedServer();

        var keyLabel = NewLabel("Key", 310, 24);
        keyTextBox = NewTextBox(310, 44, 380);

        var nameLabel = NewLabel("Name", 310, 78);
        nameTextBox = NewTextBox(310, 98, 380);

        var hostLabel = NewLabel("Host", 310, 132);
        hostTextBox = NewTextBox(310, 152, 380);

        var portLabel = NewLabel("Port", 310, 186);
        portNumeric = new NumericUpDown
        {
            Location = new System.Drawing.Point(310, 206),
            Size = new System.Drawing.Size(120, 23),
            Minimum = 1,
            Maximum = 65535,
            Value = 28100
        };

        var patchLabel = NewLabel("Patch", 450, 186);
        patchTextBox = NewTextBox(450, 206, 240);
        patchTextBox.Text = "default";

        saveButton = new Button
        {
            Text = "Save",
            Location = new System.Drawing.Point(310, 260),
            Size = new System.Drawing.Size(120, 32)
        };
        saveButton.Click += (_, _) => SaveServer();

        deleteButton = new Button
        {
            Text = "Delete",
            Location = new System.Drawing.Point(440, 260),
            Size = new System.Drawing.Size(120, 32)
        };
        deleteButton.Click += (_, _) => DeleteServer();

        var closeButton = new Button
        {
            Text = "Close",
            Location = new System.Drawing.Point(570, 260),
            Size = new System.Drawing.Size(120, 32)
        };
        closeButton.Click += (_, _) => DialogResult = DialogResult.OK;

        Controls.AddRange(
        [
            serverListBox,
            keyLabel,
            keyTextBox,
            nameLabel,
            nameTextBox,
            hostLabel,
            hostTextBox,
            portLabel,
            portNumeric,
            patchLabel,
            patchTextBox,
            saveButton,
            deleteButton,
            closeButton
        ]);

        ReloadList();
    }

    private void ReloadList()
    {
        customConfig = launcherConfigService.LoadCustomConfig(installInfo, logger);
        serverListBox.Items.Clear();

        foreach (var serverEntry in customConfig.Servers.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            serverListBox.Items.Add(new ServerItem(serverEntry.Key, serverEntry.Value));
        }

        deleteButton.Enabled = serverListBox.Items.Count > 0;
    }

    private void LoadSelectedServer()
    {
        if (serverListBox.SelectedItem is not ServerItem item)
        {
            return;
        }

        keyTextBox.Text = item.Key;
        nameTextBox.Text = item.Server.Name;
        hostTextBox.Text = item.Server.Host;
        portNumeric.Value = Math.Clamp(item.Server.Port, 1, 65535);
        patchTextBox.Text = string.IsNullOrWhiteSpace(item.Server.Patch) ? "default" : item.Server.Patch;
    }

    private void SaveServer()
    {
        var key = keyTextBox.Text.Trim();
        var name = nameTextBox.Text.Trim();
        var host = hostTextBox.Text.Trim();
        var patch = patchTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(key) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(host))
        {
            MessageBox.Show(
                "Key, name, and host are required.",
                "Custom Servers",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        customConfig.Servers[key] = new LauncherServer
        {
            Name = name,
            Host = host,
            Port = (int)portNumeric.Value,
            Patch = string.IsNullOrWhiteSpace(patch) ? "default" : patch
        };

        launcherConfigService.SaveCustomConfig(installInfo, customConfig);
        ReloadList();

        for (var i = 0; i < serverListBox.Items.Count; i++)
        {
            if (serverListBox.Items[i] is ServerItem item &&
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                serverListBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void DeleteServer()
    {
        if (serverListBox.SelectedItem is not ServerItem item)
        {
            return;
        }

        if (MessageBox.Show(
                $"Delete custom server '{item.Key}'?",
                "Custom Servers",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        customConfig.Servers.Remove(item.Key);
        launcherConfigService.SaveCustomConfig(installInfo, customConfig);
        ClearInputs();
        ReloadList();
    }

    private void ClearInputs()
    {
        keyTextBox.Clear();
        nameTextBox.Clear();
        hostTextBox.Clear();
        portNumeric.Value = 28100;
        patchTextBox.Text = "default";
    }

    private static Label NewLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Location = new System.Drawing.Point(x, y)
        };
    }

    private static TextBox NewTextBox(int x, int y, int width)
    {
        return new TextBox
        {
            Location = new System.Drawing.Point(x, y),
            Size = new System.Drawing.Size(width, 23)
        };
    }

    private sealed record ServerItem(string Key, LauncherServer Server)
    {
        public override string ToString()
        {
            return $"{Key} [{Server.Host}:{Server.Port}] ({Server.Patch})";
        }
    }
}
