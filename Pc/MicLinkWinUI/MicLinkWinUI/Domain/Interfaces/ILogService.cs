namespace MicLinkWinUI.Domain.Interfaces;

public interface ILogService
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
    IReadOnlyList<string> GetEntries();
    event EventHandler? EntryAdded;
}
