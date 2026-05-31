using System.Globalization;
using System.Windows.Forms;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Launcher;

public sealed class ReplayBrowserForm : Form
{
    private readonly GameInstallInfo installInfo;
    private readonly ReplayLauncherService replayService;
    private readonly Action launchGame;
    private readonly DataGridView replayGrid;
    private readonly Button analyzeButton;
    private readonly Button openValidationButton;
    private readonly Button openLocationButton;
    private readonly Button deleteButton;
    private readonly Button launchReplayModeButton;
    private readonly Button refreshButton;
    private readonly ProgressBar analyzeProgressBar;
    private readonly Label statusLabel;
    private IReadOnlyList<ReplayCaptureInfo> captures = [];

    public ReplayBrowserForm(GameInstallInfo installInfo, ReplayLauncherService replayService, Action launchGame)
    {
        this.installInfo = installInfo;
        this.replayService = replayService;
        this.launchGame = launchGame;

        Text = "Match Replays";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new System.Drawing.Size(760, 420);
        ClientSize = new System.Drawing.Size(900, 520);

        using var iconStream = typeof(ReplayBrowserForm).Assembly.GetManifestResourceStream("BnlCommunityFixes.Launcher.launcher-icon.ico");
        if (iconStream != null) Icon = new System.Drawing.Icon(iconStream);

        replayGrid = new DataGridView
        {
            Location = new System.Drawing.Point(14, 14),
            Size = new System.Drawing.Size(872, 406),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            MultiSelect = true,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        replayGrid.Columns.Add(CreateTextColumn("Date", "Date", 135));
        replayGrid.Columns.Add(CreateTextColumn("Map", "Map", 210));
        replayGrid.Columns.Add(CreateTextColumn("Duration", "Duration", 80));
        replayGrid.Columns.Add(CreateTextColumn("Winner", "Winner", 80));
        replayGrid.Columns.Add(CreateTextColumn("Units", "Units", 60));
        replayGrid.Columns.Add(CreateTextColumn("Size", "Size", 80));
        replayGrid.Columns.Add(CreateTextColumn("Status", "Status", 95));
        replayGrid.Columns.Add(CreateTextColumn("Replay", "ReplayStatus", 80));
        replayGrid.Columns.Add(CreateTextColumn("Quality", "Quality", 70));
        replayGrid.Columns.Add(CreateTextColumn("Checks", "Checks", 65));
        replayGrid.Columns.Add(CreateTextColumn("Warn", "Warnings", 50));
        replayGrid.Columns.Add(CreateTextColumn("File", "File", 160));
        replayGrid.SelectionChanged += (_, _) => UpdateButtonState();
        replayGrid.CellDoubleClick += async (_, _) => await AnalyzeSelectedAsync().ConfigureAwait(true);

        analyzeButton = new Button
        {
            Text = "Analyze",
            Location = new System.Drawing.Point(14, 432),
            Size = new System.Drawing.Size(104, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        analyzeButton.Click += async (_, _) => await AnalyzeSelectedAsync().ConfigureAwait(true);

        openValidationButton = new Button
        {
            Text = "Validation",
            Location = new System.Drawing.Point(124, 432),
            Size = new System.Drawing.Size(98, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        openValidationButton.Click += (_, _) => OpenSelectedValidation();

        openLocationButton = new Button
        {
            Text = "Open Location",
            Location = new System.Drawing.Point(228, 432),
            Size = new System.Drawing.Size(112, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        openLocationButton.Click += (_, _) => OpenSelectedLocation();

        deleteButton = new Button
        {
            Text = "Delete Selected",
            Location = new System.Drawing.Point(346, 432),
            Size = new System.Drawing.Size(104, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        deleteButton.Click += (_, _) => DeleteSelected();

        refreshButton = new Button
        {
            Text = "Refresh",
            Location = new System.Drawing.Point(456, 432),
            Size = new System.Drawing.Size(86, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        refreshButton.Click += (_, _) => ReloadCaptures();

        launchReplayModeButton = new Button
        {
            Text = "Launch Replay Mode",
            Location = new System.Drawing.Point(548, 432),
            Size = new System.Drawing.Size(136, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            BackColor = System.Drawing.Color.FromArgb(198, 92, 0),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
        launchReplayModeButton.FlatAppearance.BorderSize = 0;
        launchReplayModeButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(176, 80, 0);
        launchReplayModeButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(145, 66, 0);
        launchReplayModeButton.Click += (_, _) => LaunchSelectedReplayMode();

        analyzeProgressBar = new ProgressBar
        {
            Location = new System.Drawing.Point(690, 436),
            Size = new System.Drawing.Size(196, 22),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Visible = false
        };

        statusLabel = new Label
        {
            AutoSize = false,
            Location = new System.Drawing.Point(14, 472),
            Size = new System.Drawing.Size(872, 24),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = System.Drawing.SystemColors.GrayText
        };

        Controls.AddRange([replayGrid, analyzeButton, openValidationButton, openLocationButton, deleteButton, refreshButton, launchReplayModeButton, analyzeProgressBar, statusLabel]);
        ReloadCaptures();
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, string propertyName, int width) =>
        new()
        {
            HeaderText = header,
            DataPropertyName = propertyName,
            FillWeight = width,
            SortMode = DataGridViewColumnSortMode.Automatic
        };

    private void ReloadCaptures()
    {
        captures = replayService.ListCaptures(installInfo);
        replayGrid.DataSource = captures.Select(static capture => new ReplayCaptureRow(capture)).ToArray();
        statusLabel.Text = captures.Count == 0
            ? "No replay captures found. Launch the game, finish a match, then refresh this window."
            : $"{captures.Count} replay capture(s) found.";
        UpdateButtonState();
    }

    private ReplayCaptureInfo? GetSelectedCapture()
    {
        if (replayGrid.CurrentRow?.DataBoundItem is not ReplayCaptureRow row)
        {
            return null;
        }

        return row.Capture;
    }

    private IReadOnlyList<ReplayCaptureInfo> GetSelectedCaptures()
    {
        return replayGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(static row => row.DataBoundItem)
            .OfType<ReplayCaptureRow>()
            .Select(static row => row.Capture)
            .ToArray();
    }

    private void UpdateButtonState()
    {
        var selected = GetSelectedCapture();
        var selectedCount = replayGrid.SelectedRows.Count;
        analyzeButton.Enabled = selected is not null && selectedCount == 1;
        openValidationButton.Enabled = selected is not null && selectedCount == 1 && selected.HasValidationReport;
        openLocationButton.Enabled = selected is not null && selectedCount == 1;
        deleteButton.Enabled = selectedCount > 0;
        launchReplayModeButton.Enabled = selected is not null && selectedCount == 1 && replayService.HasAnalyzedReplay(selected.File);
    }

    private async Task AnalyzeSelectedAsync()
    {
        var selected = GetSelectedCapture();
        if (selected is null)
        {
            return;
        }

        SetBusy(true, $"Analyzing {selected.File.Name}...");

        try
        {
            var result = await replayService.AnalyzeCaptureAsync(selected.File, CancellationToken.None).ConfigureAwait(true);
            statusLabel.Text = $"Analyzed {Path.GetFileName(result.CapturePath)}.";
            ReloadCaptures();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Match Replays",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            statusLabel.Text = "Replay analysis failed.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenSelectedValidation()
    {
        var selected = GetSelectedCapture();
        if (selected is null)
        {
            return;
        }

        try
        {
            replayService.OpenValidationReport(selected.File);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Match Replays", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSelectedLocation()
    {
        var selected = GetSelectedCapture();
        if (selected is null)
        {
            return;
        }

        try
        {
            replayService.OpenCaptureLocation(selected.File);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Match Replays", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LaunchSelectedReplayMode()
    {
        var selected = GetSelectedCapture();
        if (selected is null)
        {
            return;
        }

        try
        {
            replayService.WriteReplayLaunchRequest(selected.File, launchReplayMode: true);
            var map = string.IsNullOrWhiteSpace(selected.MapName) ? "unknown map" : selected.MapName;
            statusLabel.Text = $"Launching replay mode for {selected.File.Name} ({map}).";
            launchGame();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Match Replays", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteSelected()
    {
        var selected = GetSelectedCaptures();
        if (selected.Count == 0)
        {
            return;
        }

        var message = selected.Count == 1
            ? $"Delete this replay capture?{Environment.NewLine}{selected[0].File.Name}"
            : $"Delete {selected.Count} selected replay captures?";

        var result = MessageBox.Show(
            message,
            "Match Replays",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            foreach (var capture in selected)
            {
                replayService.DeleteCapture(capture.File);
            }

            ReloadCaptures();
            statusLabel.Text = selected.Count == 1
                ? $"Deleted {selected[0].File.Name}."
                : $"Deleted {selected.Count} replay captures.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Match Replays", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        replayGrid.Enabled = !busy;
        if (busy)
        {
            analyzeButton.Enabled = false;
            openValidationButton.Enabled = false;
            openLocationButton.Enabled = false;
            deleteButton.Enabled = false;
            launchReplayModeButton.Enabled = false;
        }
        else
        {
            UpdateButtonState();
        }

        refreshButton.Enabled = !busy;
        analyzeProgressBar.Visible = busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            statusLabel.Text = message;
        }
        UseWaitCursor = busy;
    }

    private sealed class ReplayCaptureRow
    {
        public ReplayCaptureRow(ReplayCaptureInfo capture)
        {
            Capture = capture;
        }

        public ReplayCaptureInfo Capture { get; }
        public string Date => Capture.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        public string Map => Capture.MapName ?? "Analyze to read";
        public string Duration => Capture.DurationSeconds is null ? "" : $"{Capture.DurationSeconds:0.0}s";
        public string Winner => Capture.Winner ?? "";
        public string Units => Capture.Units?.ToString(CultureInfo.InvariantCulture) ?? "";
        public string Size => FormatSize(Capture.SizeBytes);
        public string Status => Capture.HasAnalyzedReplay ? "Analyzed" : "Not analyzed";
        public string ReplayStatus => Capture.UsableForReplay is null ? "" : Capture.UsableForReplay.Value ? "Usable" : "Not usable";
        public string Quality => Capture.Quality ?? "";
        public string Checks => Capture.RequiredPassed is null || Capture.RequiredTotal is null
            ? ""
            : $"{Capture.RequiredPassed}/{Capture.RequiredTotal}";
        public string Warnings => Capture.WarningCount?.ToString(CultureInfo.InvariantCulture) ?? "";
        public string File => Capture.File.Name;

        private static string FormatSize(long bytes)
        {
            var mb = bytes / 1024d / 1024d;
            return mb >= 1 ? $"{mb:0.0} MB" : $"{bytes / 1024d:0.0} KB";
        }
    }
}
