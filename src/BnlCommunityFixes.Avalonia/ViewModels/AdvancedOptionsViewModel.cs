using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed partial class AdvancedOptionsViewModel : ViewModelBase
{
    private readonly AppPaths _paths;
    private readonly GameInstallInfo _installInfo;
    private readonly LauncherConfigService _configService;
    private readonly Action _reloadConfig;
    private readonly Action _manageServers;
    private readonly Action _verifyFiles;

    public bool GameDetected => _installInfo.IsDetected;

    public AdvancedOptionsViewModel(
        AppPaths paths,
        GameInstallInfo installInfo,
        LauncherConfigService configService,
        Action reloadConfig,
        Action manageServers,
        Action verifyFiles)
    {
        _paths = paths;
        _installInfo = installInfo;
        _configService = configService;
        _reloadConfig = reloadConfig;
        _manageServers = manageServers;
        _verifyFiles = verifyFiles;
    }

    [RelayCommand] private void VerifyFiles() => _verifyFiles();
    [RelayCommand] private void Refresh() => _reloadConfig();
    [RelayCommand] private void ManageServers() => _manageServers();
    [RelayCommand] private void OpenConfigJson() => OpenPath(_installInfo.IsDetected ? _configService.GetContext(_installInfo).ConfigPath : null);
    [RelayCommand] private void OpenCustomJson() => OpenPath(_installInfo.IsDetected ? _configService.GetContext(_installInfo).CustomConfigPath : null);
    [RelayCommand] private void OpenPatchingFolder() => OpenPath(_paths.PatchingDir);
    [RelayCommand] private void OpenSettings() => OpenPath(Path.Combine(_paths.DataDir, "launcher-settings.json"));
    [RelayCommand] private void OpenLog() => OpenPath(_paths.LauncherLogPath);

    public event Action<string>? OpenPathRequested;

    private void OpenPath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
            OpenPathRequested?.Invoke(path);
    }
}
