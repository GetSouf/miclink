namespace MicLinkWinUI.Presentation.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using MicLinkWinUI.Domain.Interfaces;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILogService _logService;

    [ObservableProperty]
    private string _logText = string.Empty;

    public LogsViewModel(ILogService logService)
    {
        _logService = logService;
        Refresh();
        _logService.EntryAdded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        LogText = string.Join(Environment.NewLine, _logService.GetEntries());
    }
}
