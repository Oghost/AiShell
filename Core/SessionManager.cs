using AiShell.Storage;
using System.Collections.Concurrent;

namespace AiShell.Core;

public class SessionManager
{
    private readonly List<CommandSession> _commands = new();
    private readonly object _lock = new();
    private DateTime _sessionStart;

    public SessionManager()
    {
        _sessionStart = DateTime.Now;
    }

    public void AddCommand(string command, bool success)
    {
        lock (_lock)
        {
            _commands.Add(new CommandSession
            {
                Command = command,
                Success = success,
                Timestamp = DateTime.Now,
                ExecutionTime = TimeSpan.Zero // Will be set by caller
            });
        }
    }

    public List<CommandSession> GetAllCommands()
    {
        lock (_lock)
        {
            return _commands.ToList();
        }
    }

    public List<CommandSession> GetRecentCommands(int count = 10)
    {
        lock (_lock)
        {
            return _commands
                .OrderByDescending(c => c.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    public double GetSuccessRate()
    {
        lock (_lock)
        {
            if (_commands.Count == 0) return 0.0;
            
            var successCount = _commands.Count(c => c.Success);
            return (double)successCount / _commands.Count;
        }
    }

    public TimeSpan GetSessionDuration()
    {
        return DateTime.Now - _sessionStart;
    }

    public void ClearSession()
    {
        lock (_lock)
        {
            _commands.Clear();
            _sessionStart = DateTime.Now;
        }
    }
}

public class CommandSession
{
    public string Command { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}