namespace MicLinkWinUI.Infrastructure.Logging;

using MicLinkWinUI.Domain.Interfaces;
using System.Collections.ObjectModel;

public sealed class LogService : ILogService
{
    private const int MaxEntries = 500;
    private readonly List<string> _entries = [];
    private readonly object _lock = new();

    public event EventHandler? EntryAdded;

    public void Info(string message) => Add("INFO", message);

    public void Warning(string message) => Add("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var text = exception is null ? message : $"{message}: {exception.Message}";
        Add("ERROR", text);
    }

    public IReadOnlyList<string> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    private void Add(string level, string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {level}  {message}";
        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }
        }

        EntryAdded?.Invoke(this, EventArgs.Empty);
    }
}
