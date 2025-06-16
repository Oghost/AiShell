using AiShell.Storage;

namespace AiShell.UI;

public class AutoCompleteEngine
{
    private readonly LocalDataStore _localData;
    private readonly List<string> _builtInCommands;
    private readonly List<string> _commonPhrases;

    public AutoCompleteEngine(LocalDataStore localData)
    {
        _localData = localData;
        _builtInCommands = InitializeBuiltInCommands();
        _commonPhrases = InitializeCommonPhrases();
    }

    public List<string> GetSuggestions(string input, int maxSuggestions = 5)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<string>();

        var suggestions = new List<string>();
        var lowerInput = input.ToLowerInvariant();

        // 1. Check built-in commands
        suggestions.AddRange(_builtInCommands.Where(cmd => 
            cmd.StartsWith(lowerInput, StringComparison.OrdinalIgnoreCase))
            .Take(2));

        // 2. Check user variables
        var variables = _localData.GetAllVariables();
        suggestions.AddRange(variables.Keys.Where(key => 
            key.StartsWith(lowerInput, StringComparison.OrdinalIgnoreCase))
            .Select(key => $"{key} = {variables[key].Value}")
            .Take(2));

        // 3. Check common phrases
        suggestions.AddRange(_commonPhrases.Where(phrase => 
            phrase.Contains(lowerInput, StringComparison.OrdinalIgnoreCase))
            .Take(3));

        // 4. Smart completions based on context
        suggestions.AddRange(GetContextualSuggestions(lowerInput)
            .Take(2));

        return suggestions.Distinct().Take(maxSuggestions).ToList();
    }

    private List<string> InitializeBuiltInCommands()
    {
        return new List<string>
        {
            "help", "exit", "quit", "clear", "version", "status",
            "vars", "plugins", "config", "alias"
        };
    }

    private List<string> InitializeCommonPhrases()
    {
        return new List<string>
        {
            // File operations
            "list all files", "list video files", "list image files", "list pdf files",
            "find files containing", "copy files to", "move files to", "delete files",
            "create new folder", "compress files", "extract archive",
            
            // Network operations
            "ping google", "ping continuously", "check network connection",
            "show ip configuration", "trace route to", "check open ports",
            "download file from", "upload file to",
            
            // System operations
            "show running processes", "kill process", "check disk space",
            "show system information", "check memory usage", "show services",
            "restart service", "stop service", "check windows version",
            
            // Git operations
            "git status", "git add all", "git commit with message", "git push",
            "git pull", "git clone repository", "git create branch",
            "git switch branch", "git merge branch",
            
            // Video operations
            "convert video to mp4", "compress video", "extract audio from video",
            "create gif from video", "resize video", "cut video segment",
            "combine videos", "add subtitles to video"
        };
    }

    private List<string> GetContextualSuggestions(string input)
    {
        var suggestions = new List<string>();

        // Context-aware suggestions based on keywords
        if (input.Contains("list"))
        {
            suggestions.AddRange(new[]
            {
                "list all files in current directory",
                "list video files",
                "list image files",
                "list processes by memory usage"
            });
        }
        else if (input.Contains("git"))
        {
            suggestions.AddRange(new[]
            {
                "git status",
                "git add all files",
                "git commit with message",
                "git push to origin"
            });
        }
        else if (input.Contains("video"))
        {
            suggestions.AddRange(new[]
            {
                "convert video to mp4",
                "compress video file",
                "extract audio from video",
                "create gif from video"
            });
        }
        else if (input.Contains("network") || input.Contains("ping"))
        {
            suggestions.AddRange(new[]
            {
                "ping google.com",
                "check network configuration",
                "trace route to destination",
                "show network connections"
            });
        }
        else if (input.Contains("process"))
        {
            suggestions.AddRange(new[]
            {
                "show all running processes",
                "show processes using most memory",
                "kill process by name",
                "restart windows explorer"
            });
        }

        return suggestions;
    }

    public List<string> GetVariableCompletions(string prefix)
    {
        var variables = _localData.GetAllVariables();
        return variables.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();
    }

    public List<string> GetRecentCommands(int count = 10)
    {
        // This would integrate with command history
        // For now, return empty list
        return new List<string>();
    }

    public void LearnFromInput(string input, bool wasSuccessful)
    {
        // Learn from user input patterns
        if (wasSuccessful && !string.IsNullOrWhiteSpace(input))
        {
            // Add to learned phrases if it's not already common
            if (!_commonPhrases.Contains(input, StringComparer.OrdinalIgnoreCase))
            {
                _commonPhrases.Add(input);
                
                // Keep list manageable
                if (_commonPhrases.Count > 100)
                {
                    _commonPhrases.RemoveAt(0);
                }
            }
        }
    }

    public string GetSmartCompletion(string partialInput)
    {
        // Try to complete based on common patterns
        var lower = partialInput.ToLowerInvariant();
        
        if (lower.StartsWith("list"))
        {
            if (lower.Contains("vid"))
                return "list video files";
            if (lower.Contains("img") || lower.Contains("pic"))
                return "list image files";
            if (lower.Contains("doc"))
                return "list document files";
        }
        else if (lower.StartsWith("git"))
        {
            if (lower.Contains("stat"))
                return "git status";
            if (lower.Contains("add"))
                return "git add all files";
            if (lower.Contains("comm"))
                return "git commit with message";
        }
        else if (lower.StartsWith("ping"))
        {
            return "ping google.com";
        }

        return partialInput;
    }
}