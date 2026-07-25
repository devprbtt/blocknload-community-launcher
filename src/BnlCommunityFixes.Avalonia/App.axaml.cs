using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BnlCommunityFixes.Avalonia.ViewModels;
using BnlCommunityFixes.Avalonia.Views;
using BnlCommunityFixes.Core.Models;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia;

public partial class App : Application
{
    public sealed class StartupContext
    {
        public required MainWindowViewModel MainVm { get; init; }
        public UpdateCoordinator? UpdateCoordinator { get; init; }

        // Set when game was not found at startup; GameSetupWindow runs first.
        public GameSetupArgs? GameSetup { get; init; }
    }

    public sealed class GameSetupArgs
    {
        public required AppPaths Paths { get; init; }
        public required Logger Logger { get; init; }
        public required LauncherSettings Settings { get; init; }
        public required SettingsService SettingsService { get; init; }
        public required System.Net.Http.HttpClient HttpClient { get; init; }
        public required UpdateCoordinator? UpdateCoordinator { get; init; }
    }

    public static StartupContext? Startup { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ChineseLocalization.Start(desktop);
            var ctx = Startup;

            if (ctx?.GameSetup is { } setup)
            {
                var setupWindow = new GameSetupWindow(
                    setup.Paths, setup.Logger, setup.Settings,
                    setup.SettingsService, setup.HttpClient);

                setupWindow.Closed += (_, _) =>
                {
                    if (setupWindow.ResultInstallInfo is not { IsDetected: true } installInfo)
                    {
                        desktop.Shutdown();
                        return;
                    }

                    var launcherConfigService = new LauncherConfigService();
                    var launcherConfig = launcherConfigService.LoadOrCreate(installInfo, setup.Logger);
                    var reloadedSettings = setup.SettingsService.Load();
                    var mainVm = new MainWindowViewModel(
                        setup.Paths, setup.Logger, reloadedSettings,
                        installInfo, launcherConfig, setup.HttpClient);

                    var mainWindow = new MainWindow(setup.UpdateCoordinator) { DataContext = mainVm };
                    mainWindow.Closed += (_, _) => mainVm.StopManagedServices();
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                };

                desktop.MainWindow = setupWindow;
            }
            else
            {
                var vm = ctx?.MainVm ?? (desktop.Args is { } args
                    ? MainWindowViewModel.Create(args)
                    : new MainWindowViewModel());

                var mainWindow = new MainWindow(ctx?.UpdateCoordinator) { DataContext = vm };
                mainWindow.Closed += (_, _) => vm.StopManagedServices();
                desktop.MainWindow = mainWindow;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
