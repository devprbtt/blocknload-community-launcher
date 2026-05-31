using Avalonia.Controls;
using Avalonia.Threading;
using BnlCommunityFixes.Avalonia.ViewModels;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class ProgressDialog : Window
{
    public ProgressDialog() { InitializeComponent(); }

     public ProgressDialog(string title)
    {
        InitializeComponent();
        DataContext = new ProgressDialogViewModel(title);
    }

    private ProgressDialogViewModel Vm => (ProgressDialogViewModel)DataContext!;

    public void SetProgress(int percent, string status)
    {
        Dispatcher.UIThread.Post(() => Vm.SetProgress(percent, status));
    }
}
