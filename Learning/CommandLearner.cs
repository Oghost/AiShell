using AiShell.Storage;
using Microsoft.Extensions.Logging;

namespace AiShell.Learning;

public class CommandLearner
{
    private readonly SharedKnowledge _sharedKnowledge;
    private readonly Dictionary<string, LearningSession> _activeSessions = new();

    public CommandLearner(SharedKnowledge sharedKnowledge)
    {
        _sharedKnowledge = sharedKnowledge;
    }

    public async Task RecordCommandAsync(string naturalInput, string generatedCommand, bool success)
    {
        var sessionKey = GenerateSessionKey(naturalInput);
        
        if (!_activeSessions.ContainsKey(sessionKey))
        {
            _activeSessions[sessionKey] = new LearningSession
            {
                NaturalInput = naturalInput,
                FirstSeen = DateTime.UtcNow
            };
        }

        var session = _activeSessions[sessionKey];
        session.Attempts.Add(new CommandAttempt
        {
            Command = generatedCommand,
            Success = success,
            Timestamp = DateTime.UtcNow
        });

        // If successful, record in shared knowledge
        if (success)
        {
            _sharedKnowledge.RecordSuccessfulCommand(naturalInput, generatedCommand);
            
            // Clean up session
            _activeSessions.Remove(sessionKey);
        }
        
        // Clean up old failed sessions (older than 1 hour)
        await CleanupOldSessionsAsync();
    }

    public async Task<string?> GetLearnedCommandAsync(string naturalInput)
    {
        // First check shared knowledge
        var knownCommand = _sharedKnowledge.GetBestCommand(naturalInput);
        if (!string.IsNullOrEmpty(knownCommand))
        {
            return knownCommand;
        }

        // Check active learning sessions for patterns
        var sessionKey = GenerateSessionKey(naturalInput);
        if (_activeSessions.ContainsKey(sessionKey))
        {
            var session = _activeSessions[sessionKey];
            var successfulAttempt = session.Attempts.FirstOrDefault(a => a.Success);
            return successfulAttempt?.Command;
        }

        return null;
    }

    public async Task<List<LearningInsight>> GetLearningInsightsAsync()
    {
        var insights = new List<LearningInsight>();
        
        // Analyze successful patterns
        var topPatterns = _sharedKnowledge.GetTopPatterns(10);
        foreach (var pattern in topPatterns)
        {
            insights.Add(new LearningInsight
            {
                Type = InsightType.SuccessfulPattern,
                Description = $"'{pattern.NaturalInput}' → '{pattern.Command}' (Used {pattern.SuccessCount} times)",
                Confidence = Math.Min(pattern.SuccessCount * 10, 100),
                LastSeen = pattern.LastUsed
            });
        }

        // Analyze failed sessions
        var failedSessions = _activeSessions.Values.Where(s => 
            s.Attempts.Count > 2 && 
            !s.Attempts.Any(a => a.Success));

        foreach (var session in failedSessions)
        {
            insights.Add(new LearningInsight
            {
                Type = InsightType.FailurePattern,
                Description = $"Struggling with: '{session.NaturalInput}' ({session.Attempts.Count} failed attempts)",
                Confidence = Math.Min(session.Attempts.Count * 20, 100),
                LastSeen = session.Attempts.Max(a => a.Timestamp)
            });
        }

        return insights.OrderByDescending(i => i.Confidence).ToList();
    }

    public async Task<LearningStatistics> GetStatisticsAsync()
    {
        var patterns = _sharedKnowledge.GetTopPatterns(1000);
        var activeSessions = _activeSessions.Values.ToList();

        return new LearningStatistics
        {
            TotalLearnedCommands = patterns.Count,
            TotalSuccessfulExecutions = patterns.Sum(p => p.SuccessCount),
            ActiveLearningSessions = activeSessions.Count,
            AverageAttemptsPerSession = activeSessions.Any() 
                ? activeSessions.Average(s => s.Attempts.Count) 
                : 0,
            MostUsedCommand = patterns.OrderByDescending(p => p.SuccessCount).FirstOrDefault()?.Command ?? "None",
            LearningAccuracy = CalculateLearningAccuracy(patterns)
        };
    }

    private double CalculateLearningAccuracy(List<CommandPattern> patterns)
    {
        if (!patterns.Any()) return 0.0;

        // Simple accuracy calculation based on repeat usage
        var repeatUsage = patterns.Count(p => p.SuccessCount > 1);
        return (double)repeatUsage / patterns.Count * 100;
    }

    private string GenerateSessionKey(string naturalInput)
    {
        // Normalize input for session grouping
        var normalized = naturalInput.ToLowerInvariant()
            .Trim()
            .Replace("  ", " ");
        
        // Simple hash for session key
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(normalized))
            .Replace("+", "").Replace("/", "").Replace("=", "")
            .Substring(0, Math.Min(16, normalized.Length));
    }

    private async Task CleanupOldSessionsAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var oldSessions = _activeSessions.Where(kvp => kvp.Value.FirstSeen < cutoff).ToList();
        
        foreach (var session in oldSessions)
        {
            _activeSessions.Remove(session.Key);
        }
    }

    public async Task ExportLearningDataAsync(string filePath)
    {
        var exportData = new LearningExport
        {
            Patterns = _sharedKnowledge.GetTopPatterns(1000),
            ActiveSessions = _activeSessions.Values.ToList(),
            ExportDate = DateTime.UtcNow,
            Statistics = await GetStatisticsAsync()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(filePath, json);
    }
}

public class LearningSession
{
    public string NaturalInput { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public List<CommandAttempt> Attempts { get; set; } = new();
}

public class CommandAttempt
{
    public string Command { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
}

public class LearningInsight
{
    public InsightType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public DateTime LastSeen { get; set; }
}

public enum InsightType
{
    SuccessfulPattern,
    FailurePattern,
    OptimizationOpportunity,
    NewCapability
}

public class LearningStatistics
{
    public int TotalLearnedCommands { get; set; }
    public int TotalSuccessfulExecutions { get; set; }
    public int ActiveLearningSessions { get; set; }
    public double AverageAttemptsPerSession { get; set; }
    public string MostUsedCommand { get; set; } = string.Empty;
    public double LearningAccuracy { get; set; }
}

public class LearningExport
{
    public List<CommandPattern> Patterns { get; set; } = new();
    public List<LearningSession> ActiveSessions { get; set; } = new();
    public DateTime ExportDate { get; set; }
    public LearningStatistics Statistics { get; set; } = new();
}