using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed partial class CustomServerManagerViewModel : ViewModelBase
{
    private readonly Logger _logger;
    private readonly GameInstallInfo _installInfo;
    private readonly LauncherConfigService _configService;
    private LauncherConfig _customConfig;

    [ObservableProperty] private ObservableCollection<ServerItem> _servers = [];
    [ObservableProperty] private ServerItem? _selectedServer;
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port = 28100;
    [ObservableProperty] private string _patch = "default";

    public event Action<string, string>? ErrorOccurred;
    public event Func<string, Task<bool>>? ConfirmDelete;

    public CustomServerManagerViewModel(Logger logger, GameInstallInfo installInfo, LauncherConfigService configService)
    {
        _logger = logger;
        _installInfo = installInfo;
        _configService = configService;
        _customConfig = configService.LoadCustomConfig(installInfo, logger);
        Reload();
    }

    partial void OnSelectedServerChanged(ServerItem? value)
    {
        if (value is null) return;
        Key = value.Key;
        Name = value.Server.Name;
        Host = value.Server.Host;
        Port = Math.Clamp(value.Server.Port, 1, 65535);
        Patch = string.IsNullOrWhiteSpace(value.Server.Patch) ? "default" : value.Server.Patch;
    }

    private void Reload()
    {
        _customConfig = _configService.LoadCustomConfig(_installInfo, _logger);
        Servers.Clear();
        foreach (var entry in _customConfig.Servers.OrderBy(static e => e.Key, StringComparer.OrdinalIgnoreCase))
            Servers.Add(new ServerItem(entry.Key, entry.Value));
    }

    [RelayCommand]
    private void Save()
    {
        var k = Key.Trim(); var n = Name.Trim(); var h = Host.Trim();
        if (string.IsNullOrWhiteSpace(k) || string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(h))
        {
            ErrorOccurred?.Invoke("Validation", "Key, name, and host are required.");
            return;
        }
        _customConfig.Servers[k] = new LauncherServer { Name = n, Host = h, Port = Port, Patch = string.IsNullOrWhiteSpace(Patch) ? "default" : Patch };
        _configService.SaveCustomConfig(_installInfo, _customConfig);
        Reload();
        SelectedServer = Servers.FirstOrDefault(s => string.Equals(s.Key, k, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedServer is not { } item) return;
        if (ConfirmDelete is not null && !await ConfirmDelete($"Delete custom server '{item.Key}'?")) return;
        _customConfig.Servers.Remove(item.Key);
        _configService.SaveCustomConfig(_installInfo, _customConfig);
        Key = Name = Host = string.Empty; Port = 28100; Patch = "default";
        Reload();
    }

    public sealed record ServerItem(string Key, LauncherServer Server)
    {
        public override string ToString() => $"{Key} [{Server.Host}:{Server.Port}] ({Server.Patch})";
    }
}
