using CommunityToolkit.Mvvm.ComponentModel;

namespace BnlCommunityFixes.Avalonia.ViewModels;

public sealed partial class ProgressDialogViewModel : ViewModelBase
{
    [ObservableProperty] private int _percent;
    [ObservableProperty] private string _status = string.Empty;

    public ProgressDialogViewModel(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public void SetProgress(int percent, string status)
    {
        Percent = Math.Clamp(percent, 0, 100);
        Status = status;
    }
}
