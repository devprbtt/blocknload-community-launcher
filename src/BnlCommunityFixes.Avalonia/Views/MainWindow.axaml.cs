using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BnlCommunityFixes.Avalonia.ViewModels;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly UpdateCoordinator? _updateCoordinator;

    public MainWindow() : this(null) { }

    public MainWindow(UpdateCoordinator? updateCoordinator = null)
    {
        InitializeComponent();
        _updateCoordinator = updateCoordinator;
    }

    private MainWindowViewModel Vm => (MainWindowViewModel)DataContext!;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        Vm.ErrorOccurred += ShowError;

        if (Vm.ConsumePendingUpdateNotice())
        {
            ShowInfo(
                "Recommended Settings Applied",
                "This launcher update reset the active feature settings to Recommended Settings.\n\n" +
                "Use the Settings profile dropdown on the main screen if you want to restore your Personal Settings.");
        }

        if (_updateCoordinator is not null)
        {
            // Run the update check asynchronously after the window is fully shown
            Dispatcher.UIThread.Post(() => _ = RunUpdateCheckAsync(), DispatcherPriority.Background);
        }
    }

    private async Task RunUpdateCheckAsync()
    {
        if (_updateCoordinator is null) return;

        UpdateProgressWindow? progressWindow = null;

        _updateCoordinator.ConfirmUpdate = async (manifest, forced) =>
        {
            var accepted = await PatchNotesWindow.ShowAsync(this, manifest, forced);
            if (accepted)
            {
                // Show progress window only when user accepted the download
                progressWindow = new UpdateProgressWindow();
                progressWindow.Show(this);
            }
            return accepted;
        };

        _updateCoordinator.ReportProgress = (percent, status) =>
            progressWindow?.SetDownloadProgress(status, percent / 100.0);

        try
        {
            var result = await _updateCoordinator.CheckAndApplyIfAcceptedAsync();

            progressWindow?.Close();
            progressWindow = null;

            if (result.ShouldExitForUpdate)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            progressWindow?.Close();
            ShowError("Update check failed", ex.Message);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Vm.ErrorOccurred -= ShowError;
        base.OnClosed(e);
    }

    // ── Button click handlers ─────────────────────────────────────────────────

    private async void FeatureSettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new FeatureSettingsViewModel(Vm._paths, Vm._installInfo);
        await new FeatureSettingsWindow(vm).ShowDialog(this);
        Vm.Reload();
    }

    private async void ImportExportButton_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new ConfigTransferViewModel(Vm._paths);
        await new ConfigTransferWindow(vm).ShowDialog(this);
        Vm.Reload();
    }

    private async void MoreOptionsButton_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new AdvancedOptionsViewModel(
            Vm._paths, Vm._installInfo, Vm._launcherConfigService,
            Vm.Reload, OpenManageServers, VerifyGameFiles);
        await new AdvancedOptionsWindow(vm).ShowDialog(this);
    }

    private async void AudioReplacerButton_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new AudioReplacerViewModel(Vm._paths, Vm._installInfo);
        await new ReplacerWindow(vm).ShowDialog(this);
    }

    private async void MeshReplacerButton_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new MeshReplacerViewModel(Vm._paths, Vm._installInfo);
        await new ReplacerWindow(vm).ShowDialog(this);
    }

    private async void TextureReplacerButton_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new TextureReplacerViewModel(Vm._paths, Vm._installInfo);
        await new ReplacerWindow(vm).ShowDialog(this);
        Vm.Reload();
    }

    private async void ReplayBrowserButton_Click(object? sender, RoutedEventArgs e)
    {
        var vm = new ReplayBrowserViewModel(Vm._installInfo, Vm._replayLauncherService, LaunchForReplay);
        await new ReplayBrowserWindow(vm).ShowDialog(this);
        Vm.Reload();
    }

    private async void OpenManageServers()
    {
        var vm = new CustomServerManagerViewModel(Vm._logger, Vm._installInfo, Vm._launcherConfigService);
        await new CustomServerManagerWindow(vm).ShowDialog(this);
        Vm.Reload();
    }

    private void VerifyGameFiles()
    {
        try { Vm._launchCoordinator.VerifyGameFiles(); }
        catch (Exception ex) { ShowError("Verify failed", ex.Message); }
    }

    private void LaunchForReplay()
    {
        Vm.LaunchCommand.Execute(null);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void ShowError(string title, string message) =>
        _ = new MessageDialog(title, message, isError: true).ShowDialog(this);

    private void ShowInfo(string title, string message) =>
        _ = new MessageDialog(title, message, isError: false).ShowDialog(this);
}
