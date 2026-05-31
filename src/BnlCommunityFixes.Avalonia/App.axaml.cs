using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BnlCommunityFixes.Avalonia.ViewModels;
using BnlCommunityFixes.Avalonia.Views;
using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Avalonia;

public partial class App : Application
{
    /// <summary>
    /// Startup context built by Program.cs before Avalonia initialises.
    /// Passed to MainWindow so the update check can run with full UI.
    /// </summary>
    public sealed class StartupContext
    {
        public required MainWindowViewModel MainVm { get; init; }
        public UpdateCoordinator? UpdateCoordinator { get; init; }
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
            var ctx = Startup;
            var vm = ctx?.MainVm ?? (desktop.Args is { } args
                ? MainWindowViewModel.Create(args)
                : new MainWindowViewModel());

            desktop.MainWindow = new MainWindow(ctx?.UpdateCoordinator) { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}