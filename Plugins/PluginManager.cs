using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AiShell.Plugins;

public class PluginManager
{
    private readonly List<IPlugin> _plugins = new();
    private readonly ILogger _logger;

    public PluginManager(ILogger logger)
    {
        _logger = logger;
    }

    public void LoadBuiltInPlugins()
    {
        _plugins.Add(new GitPlugin(_logger));
        _plugins.Add(new FFmpegPlugin(_logger));
        _plugins.Add(new NetworkPlugin(_logger));
        _plugins.Add(new SystemPlugin(_logger));
        _plugins.Add(new FilePlugin(_logger));

        _logger.LogInformation($"Loaded {_plugins.Count} built-in plugins");
    }

    public List<IPlugin> GetAllPlugins()
    {
        return _plugins.ToList();
    }

    public IPlugin? GetPluginForKeyword(string keyword)
    {
        return _plugins.FirstOrDefault(p => 
            p.Keywords.Any(k => k.Equals(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<bool> InstallPluginDependencyAsync(IPlugin plugin)
    {
        if (!plugin.RequiresInstallation())
            return true;

        _logger.LogInformation($"Installing dependencies for {plugin.Name}...");
        return await plugin.InstallAsync();
    }
}

public interface IPlugin
{
    string Name { get; }
    string Description { get; }
    string[] Keywords { get; }
    bool RequiresInstallation();
    Task<bool> InstallAsync();
    Task<string> ProcessAsync(string input, Dictionary<string, string> context);
}

public class GitPlugin : IPlugin
{
    private readonly ILogger _logger;

    public string Name => "Git";
    public string Description => "Git version control operations";
    public string[] Keywords => new[] { "git", "clone", "pull", "push", "commit", "branch", "checkout" };

    public GitPlugin(ILogger logger)
    {
        _logger = logger;
    }

    public bool RequiresInstallation()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit();
            return process?.ExitCode != 0;
        }
        catch
        {
            return true;
        }
    }

    public async Task<bool> InstallAsync()
    {
        _logger.LogInformation("Git installation requires manual download from https://git-scm.com/");
        return false; // Manual installation required
    }

    public async Task<string> ProcessAsync(string input, Dictionary<string, string> context)
    {
        var lowerInput = input.ToLowerInvariant();

        if (lowerInput.Contains("clone"))
            return "git clone <repository-url>";
        if (lowerInput.Contains("status"))
            return "git status";
        if (lowerInput.Contains("add"))
            return "git add .";
        if (lowerInput.Contains("commit"))
            return "git commit -m \"commit message\"";
        if (lowerInput.Contains("push"))
            return "git push origin main";
        if (lowerInput.Contains("pull"))
            return "git pull origin main";

        return "git status";
    }
}

public class FFmpegPlugin : IPlugin
{
    private readonly ILogger _logger;

    public string Name => "FFmpeg";
    public string Description => "Video and audio processing";
    public string[] Keywords => new[] { "ffmpeg", "video", "audio", "convert", "compress", "extract" };

    public FFmpegPlugin(ILogger logger)
    {
        _logger = logger;
    }

    public bool RequiresInstallation()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit();
            return process?.ExitCode != 0;
        }
        catch
        {
            return true;
        }
    }

    public async Task<bool> InstallAsync()
    {
        _logger.LogInformation("FFmpeg installation requires manual download from https://ffmpeg.org/");
        return false; // Manual installation required
    }

    public async Task<string> ProcessAsync(string input, Dictionary<string, string> context)
    {
        var lowerInput = input.ToLowerInvariant();

        if (lowerInput.Contains("convert") && lowerInput.Contains("mp4"))
            return "ffmpeg -i input.avi -c:v libx264 -c:a aac output.mp4";
        if (lowerInput.Contains("compress"))
            return "ffmpeg -i input.mp4 -vf scale=1280:720 -c:v libx264 -crf 23 -c:a aac compressed.mp4";
        if (lowerInput.Contains("extract") && lowerInput.Contains("audio"))
            return "ffmpeg -i input.mp4 -vn -acodec copy output.aac";
        if (lowerInput.Contains("gif"))
            return "ffmpeg -i input.mp4 -vf \"fps=10,scale=320:-1\" output.gif";
        if (lowerInput.Contains("thumbnail"))
            return "ffmpeg -i input.mp4 -ss 00:00:01 -vframes 1 thumbnail.jpg";

        return "ffmpeg -i input.mp4 output.mp4";
    }
}

public class NetworkPlugin : IPlugin
{
    private readonly ILogger _logger;

    public string Name => "Network";
    public string Description => "Network diagnostics and tools";
    public string[] Keywords => new[] { "ping", "tracert", "nslookup", "netstat", "ipconfig", "network" };

    public NetworkPlugin(ILogger logger)
    {
        _logger = logger;
    }

    public bool RequiresInstallation() => false; // Built into Windows

    public async Task<bool> InstallAsync() => true;

    public async Task<string> ProcessAsync(string input, Dictionary<string, string> context)
    {
        var lowerInput = input.ToLowerInvariant();

        if (lowerInput.Contains("ping"))
        {
            if (lowerInput.Contains("continuous") || lowerInput.Contains("forever"))
                return "ping -t google.com";
            return "ping google.com";
        }
        if (lowerInput.Contains("trace"))
            return "tracert google.com";
        if (lowerInput.Contains("dns") || lowerInput.Contains("nslookup"))
            return "nslookup google.com";
        if (lowerInput.Contains("port") || lowerInput.Contains("netstat"))
            return "netstat -an";
        if (lowerInput.Contains("ip") || lowerInput.Contains("config"))
            return "ipconfig /all";

        return "ping google.com";
    }
}

public class SystemPlugin : IPlugin
{
    private readonly ILogger _logger;

    public string Name => "System";
    public string Description => "System information and management";
    public string[] Keywords => new[] { "system", "process", "service", "task", "memory", "cpu" };

    public SystemPlugin(ILogger logger)
    {
        _logger = logger;
    }

    public bool RequiresInstallation() => false;

    public async Task<bool> InstallAsync() => true;

    public async Task<string> ProcessAsync(string input, Dictionary<string, string> context)
    {
        var lowerInput = input.ToLowerInvariant();

        if (lowerInput.Contains("process"))
        {
            if (lowerInput.Contains("memory") || lowerInput.Contains("ram"))
                return "tasklist /fo table";
            if (lowerInput.Contains("kill") || lowerInput.Contains("stop"))
                return "taskkill /f /im notepad.exe";
            return "tasklist";
        }
        if (lowerInput.Contains("service"))
            return "sc query";
        if (lowerInput.Contains("system") && lowerInput.Contains("info"))
            return "systeminfo";
        if (lowerInput.Contains("disk") || lowerInput.Contains("space"))
            return "wmic logicaldisk get size,freespace,caption";
        if (lowerInput.Contains("user"))
            return "whoami";

        return "systeminfo";
    }
}

public class FilePlugin : IPlugin
{
    private readonly ILogger _logger;

    public string Name => "File Operations";
    public string Description => "File and directory operations";
    public string[] Keywords => new[] { "file", "directory", "folder", "list", "copy", "move", "delete" };

    public FilePlugin(ILogger logger)
    {
        _logger = logger;
    }

    public bool RequiresInstallation() => false;

    public async Task<bool> InstallAsync() => true;

    public async Task<string> ProcessAsync(string input, Dictionary<string, string> context)
    {
        var lowerInput = input.ToLowerInvariant();

        if (lowerInput.Contains("list"))
        {
            if (lowerInput.Contains("video"))
                return "dir *.mp4 *.avi *.mkv *.mov *.wmv";
            if (lowerInput.Contains("image") || lowerInput.Contains("photo"))
                return "dir *.jpg *.jpeg *.png *.gif *.bmp";
            if (lowerInput.Contains("document"))
                return "dir *.doc *.docx *.pdf *.txt";
            return "dir";
        }
        if (lowerInput.Contains("copy"))
            return "copy source.txt destination.txt";
        if (lowerInput.Contains("move"))
            return "move source.txt C:\\destination\\";
        if (lowerInput.Contains("delete") || lowerInput.Contains("remove"))
            return "del filename.txt";
        if (lowerInput.Contains("create") && lowerInput.Contains("folder"))
            return "mkdir newfolder";

        return "dir";
    }
}