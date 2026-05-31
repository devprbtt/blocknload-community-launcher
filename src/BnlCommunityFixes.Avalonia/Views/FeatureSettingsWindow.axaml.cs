using Avalonia.Controls;
using BnlCommunityFixes.Avalonia.ViewModels;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class FeatureSettingsWindow : Window
{
    public FeatureSettingsWindow() { InitializeComponent(); }

     public FeatureSettingsWindow(FeatureSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.ErrorOccurred += (t, m) => _ = new MessageDialog(t, m, isError: true).ShowDialog(this);
        vm.Saved += Close;
    }
}
