using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiShell.Security;

public class PermissionManager
{
    private readonly HashSet<string> _trustedCommands;
    private readonly HashSet<string> _dangerousCommands;
    private readonly string _trustedSoftwarePath;

    public PermissionManager()
    {
        _trustedSoftwarePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "trusted_software.json");
        _trustedCommands = LoadTrustedCommands();
        _dangerousCommands = LoadDangerousCommands();
    }

    public bool RequiresElevation(string command)
    {
        var lowerCommand = command.ToLowerInvariant().Trim();

        // Commands that definitely require elevation
        var elevationKeywords = new[]
        {
            "runas", "sudo", "reg add", "reg delete", "net user", "net localgroup",
            "sc create", "sc delete", "sc config", "schtasks /create", "diskpart",
            "format", "chkdsk", "sfc", "dism", "bcdedit", "powercfg", "netsh",
            "takeown", "icacls", "cacls", "attrib +s", "attrib +h"
        };

        return elevationKeywords.Any(keyword => lowerCommand.Contains(keyword));
    }

    public bool IsDangerousCommand(string command)
    {
        var lowerCommand = command.ToLowerInvariant().Trim();

        // Check against known dangerous patterns
        return _dangerousCommands.Any(dangerous => lowerCommand.Contains(dangerous));
    }

    public bool IsTrustedSoftware(string executable)
    {
        var lowerExe = Path.GetFileName(executable).ToLowerInvariant();
        return _trustedCommands.Contains(lowerExe);
    }

    public SecurityAssessment AssessCommand(string command)
    {
        var assessment = new SecurityAssessment
        {
            Command = command,
            IsElevationRequired = RequiresElevation(command),
            IsDangerous = IsDangerousCommand(command),
            RiskLevel = DetermineRiskLevel(command)
        };

        // Extract executable and check if trusted
        var executable = ExtractExecutable(command);
        if (!string.IsNullOrEmpty(executable))
        {
            assessment.IsTrustedSoftware = IsTrustedSoftware(executable);
            assessment.Executable = executable;
        }

        // Add warnings based on assessment
        if (assessment.IsDangerous)
        {
            assessment.Warnings.Add("This command may perform dangerous operations");
        }

        if (assessment.IsElevationRequired)
        {
            assessment.Warnings.Add("This command requires administrator privileges");
        }

        //Not check trusted software
        //if (!assessment.IsTrustedSoftware && !string.IsNullOrEmpty(assessment.Executable))
        //{
        //    assessment.Warnings.Add($"'{assessment.Executable}' is not in the trusted software list");
        //}

        return assessment;
    }

    private RiskLevel DetermineRiskLevel(string command)
    {
        var lowerCommand = command.ToLowerInvariant();

        // Critical risk patterns
        var criticalPatterns = new[]
        {
            "format", "del /s", "rmdir /s", "rd /s", "deltree", "shutdown /s",
            "reg delete", "sc delete", "net user", "takeown", "cipher /w"
        };

        if (criticalPatterns.Any(pattern => lowerCommand.Contains(pattern)))
            return RiskLevel.Critical;

        // High risk patterns
        var highRiskPatterns = new[]
        {
            "taskkill", "net stop", "sc stop", "reg add", "schtasks",
            "netsh", "powercfg", "diskpart", "bcdedit"
        };

        if (highRiskPatterns.Any(pattern => lowerCommand.Contains(pattern)))
            return RiskLevel.High;

        // Medium risk patterns
        var mediumRiskPatterns = new[]
        {
            "copy", "move", "xcopy", "robocopy", "attrib", "icacls",
            "net", "ping", "telnet", "ftp"
        };

        if (mediumRiskPatterns.Any(pattern => lowerCommand.Contains(pattern)))
            return RiskLevel.Medium;

        return RiskLevel.Low;
    }

    private string ExtractExecutable(string command)
    {
        try
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var executable = parts[0];
                
                // Remove quotes if present
                executable = executable.Trim('"', '\'');
                
                // Get just the filename if it's a full path
                return Path.GetFileName(executable);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return string.Empty;
    }

    private HashSet<string> LoadTrustedCommands()
    {
        try
        {
            if (File.Exists(_trustedSoftwarePath))
            {
                var json = File.ReadAllText(_trustedSoftwarePath);
                var config = JsonSerializer.Deserialize<TrustedSoftwareConfig>(json);
                return new HashSet<string>(config?.TrustedExecutables ?? new List<string>(), 
                                         StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Fall back to defaults
        }

        return GetDefaultTrustedCommands();
    }

    private HashSet<string> GetDefaultTrustedCommands()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Windows built-in commands
            "dir", "cd", "md", "mkdir", "rd", "rmdir", "copy", "move", "del", "type",
            "echo", "cls", "date", "time", "ver", "vol", "path", "set", "prompt",
            "title", "color", "mode", "more", "find", "findstr", "sort", "fc",
            
            // Network tools
            "ping", "tracert", "nslookup", "netstat", "ipconfig", "arp", "route",
            
            // System info
            "tasklist", "systeminfo", "whoami", "hostname", "getmac",
            
            // File operations
            "attrib", "tree", "where", "which", "robocopy", "xcopy",
            
            // Git (if available)
            "git.exe", "git",
            
            // Common tools
            "curl.exe", "curl", "wget.exe", "wget", "7z.exe", "winrar.exe",
            
            // Media tools
            "ffmpeg.exe", "ffmpeg", "ffprobe.exe", "ffprobe"
        };
    }

    private HashSet<string> LoadDangerousCommands()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "format", "fdisk", "deltree", "cipher /w", "sdelete",
            "reg delete", "sc delete", "net user /delete", "rmdir /s",
            "del /s", "rd /s", "takeown", "shutdown /s", "shutdown /r",
            "powershell -encodedcommand", "cmd /c rd", "cmd /c del"
        };
    }

    public void AddTrustedSoftware(string executable)
    {
        _trustedCommands.Add(executable.ToLowerInvariant());
        SaveTrustedSoftware();
    }

    public void RemoveTrustedSoftware(string executable)
    {
        _trustedCommands.Remove(executable.ToLowerInvariant());
        SaveTrustedSoftware();
    }

    private void SaveTrustedSoftware()
    {
        try
        {
            var config = new TrustedSoftwareConfig
            {
                TrustedExecutables = _trustedCommands.ToList(),
                LastUpdated = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            
            var directory = Path.GetDirectoryName(_trustedSoftwarePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(_trustedSoftwarePath, json);
        }
        catch
        {
            // Fail silently
        }
    }
}

public class SecurityAssessment
{
    public string Command { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
    public bool IsElevationRequired { get; set; }
    public bool IsDangerous { get; set; }
    public bool IsTrustedSoftware { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public class TrustedSoftwareConfig
{
    public List<string> TrustedExecutables { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}