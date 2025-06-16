using System.Text.Json;

namespace AiShell.Storage;

public class SharedKnowledge
{
    private readonly string _knowledgeFilePath;
    private KnowledgeData _knowledge;

    public SharedKnowledge()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AiShell");
        Directory.CreateDirectory(appDataPath);
        
        _knowledgeFilePath = Path.Combine(appDataPath, "shared_knowledge.json");
        LoadKnowledge();
    }

    private void LoadKnowledge()
    {
        try
        {
            if (File.Exists(_knowledgeFilePath))
            {
                var json = File.ReadAllText(_knowledgeFilePath);
                _knowledge = JsonSerializer.Deserialize<KnowledgeData>(json) ?? new KnowledgeData();
            }
            else
            {
                _knowledge = new KnowledgeData();
                SaveKnowledge();
            }
        }
        catch
        {
            _knowledge = new KnowledgeData();
        }
    }

    private void SaveKnowledge()
    {
        try
        {
            var json = JsonSerializer.Serialize(_knowledge, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_knowledgeFilePath, json);
        }
        catch
        {
            // Fail silently for now
        }
    }

    public void RecordSuccessfulCommand(string naturalInput, string command)
    {
        var pattern = new CommandPattern
        {
            NaturalInput = naturalInput.ToLower(),
            Command = command,
            SuccessCount = 1,
            LastUsed = DateTime.Now
        };

        var existing = _knowledge.CommandPatterns.FirstOrDefault(p => 
            p.NaturalInput.Equals(naturalInput, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.SuccessCount++;
            existing.LastUsed = DateTime.Now;
        }
        else
        {
            _knowledge.CommandPatterns.Add(pattern);
        }

        SaveKnowledge();
    }

    public string? GetBestCommand(string naturalInput)
    {
        return _knowledge.CommandPatterns
            .Where(p => p.NaturalInput.Contains(naturalInput.ToLower()) || 
                       naturalInput.ToLower().Contains(p.NaturalInput))
            .OrderByDescending(p => p.SuccessCount)
            .ThenByDescending(p => p.LastUsed)
            .FirstOrDefault()?.Command;
    }

    public List<CommandPattern> GetTopPatterns(int count = 10)
    {
        return _knowledge.CommandPatterns
            .OrderByDescending(p => p.SuccessCount)
            .Take(count)
            .ToList();
    }
}

public class KnowledgeData
{
    public List<CommandPattern> CommandPatterns { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

public class CommandPattern
{
    public string NaturalInput { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int SuccessCount { get; set; }
    public DateTime LastUsed { get; set; }
}