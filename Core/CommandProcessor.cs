using AiShell.LLM;
using AiShell.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AiShell.Core
{
    public class CommandProcessor
    {
        private readonly ILlmProvider _localLlmProvider;
        private readonly ILogger _logger;
        private readonly CloudConfig _cloudConfig;
        
        // Dictionary to cache created providers - only created once when needed
        private readonly Dictionary<string, ILlmProvider> _cloudProviders = new(StringComparer.OrdinalIgnoreCase);

        public CommandProcessor(ILlmProvider localLlmProvider, CloudConfig cloudConfig, ILogger logger)
        {
            _localLlmProvider = localLlmProvider ?? throw new ArgumentNullException(nameof(localLlmProvider));
            _cloudConfig = cloudConfig ?? throw new ArgumentNullException(nameof(cloudConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes input as a direct command by default.
        /// </summary>
        public Task<(string Command, bool IsDirectCommand, bool IsCloudLlm, string LlmProvider, string LlmModel)> ProcessInputAsync(
            string userInput,
            Dictionary<string, VariableEntry> variables,
            List<CommandSession> recentCommands)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return Task.FromResult((string.Empty, true, false, string.Empty, string.Empty));
            }

            // Check for cloud LLM requests first
            var cloudLlmResult = DetectCloudLlm(userInput);
            if (cloudLlmResult.isCloudLlm)
            {
                _logger.LogDebug($"Detected cloud LLM request: Provider={cloudLlmResult.provider}, Model={cloudLlmResult.model}");
                return Task.FromResult((
                    cloudLlmResult.cleanInput,
                    false,
                    true,
                    cloudLlmResult.provider,
                    cloudLlmResult.model
                ));
            }

            // Check if this is a directory navigation command
            if (IsDirectoryCommand(userInput))
            {
                // Process directory command and return a special marker
                var processedCommand = ProcessDirectoryCommand(userInput);
                _logger.LogDebug($"Processed directory command '{userInput}' to '{processedCommand}'");
                return Task.FromResult((processedCommand, true, false, string.Empty, string.Empty));
            }

            // Always treat other inputs as direct commands
            _logger.LogDebug($"Treating input as direct command: '{userInput}'");
            return Task.FromResult((userInput, true, false, string.Empty, string.Empty));
        }

        /// <summary>
        /// Detect cloud LLM provider commands
        /// </summary>
        private (bool isCloudLlm, string cleanInput, string provider, string model) DetectCloudLlm(string input)
        {
            var trimmed = input.Trim();

            // GPT with model specification
                       
            // Default GPT (no model specified)
            
            if (trimmed.StartsWith("gpt ", StringComparison.OrdinalIgnoreCase))
            {
                return (true, trimmed.Substring(4), "OpenAI", "gpt-4-turbo"); // Default to GPT-4 Turbo
            }
            
            // Other providers
            if (trimmed.StartsWith("claude ", StringComparison.OrdinalIgnoreCase))
                return (true, trimmed.Substring(7), "Anthropic", "claude-3-opus");
                
            if (trimmed.StartsWith("gemini ", StringComparison.OrdinalIgnoreCase))
                return (true, trimmed.Substring(7), "Google", "gemini-pro");
                
            return (false, trimmed, "", "");
        }

        /// <summary>
        /// Checks if the command is a directory navigation command
        /// </summary>
        private bool IsDirectoryCommand(string command)
        {
            command = command.Trim().ToLower();

            // Check for drive letter changes (e.g., "c:", "d:")
            if (IsDriveLetterChange(command))
                return true;

            return command == "cd.." ||
                   command == "cd/" ||
                   command == "cd .." ||
                   command == ".." ||
                   command.StartsWith("cd ") ||
                   command.StartsWith("chdir ");
        }
        // <summary>
        /// Checks if the command is a drive letter change (e.g., "c:", "d:")
        /// </summary>
        private bool IsDriveLetterChange(string command)
        {
            // Match patterns like "c:", "d:", etc.
            if (command.Length == 2 &&
                char.IsLetter(command[0]) &&
                command[1] == ':')
            {
                return true;
            }

            // Also match drive changes with slash (e.g., "c:\", "d:\")
            if (command.Length == 3 &&
                char.IsLetter(command[0]) &&
                command[1] == ':' &&
                (command[2] == '\\' || command[2] == '/'))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Processes a directory navigation command by actually changing the directory
        /// </summary>
        private string ProcessDirectoryCommand(string command)
        {
            string cmd = command.Trim();
            string oldDirectory = Environment.CurrentDirectory;

            try
            {
                // Handle drive letter changes
                if (IsDriveLetterChange(cmd.ToLower()))
                {
                    string driveLetter = cmd.Substring(0, 1).ToUpper();
                    bool includesRoot = cmd.Length > 2 && (cmd[2] == '\\' || cmd[2] == '/');

                    // Check if drive exists
                    if (!Directory.GetLogicalDrives().Contains($"{driveLetter}:\\"))
                    {
                        return $"The drive {driveLetter}: does not exist or is not ready.";
                    }

                    try
                    {
                        if (includesRoot)
                        {
                            // If the command includes a backslash (like "C:\"), go to root
                            Environment.CurrentDirectory = $"{driveLetter}:\\";
                        }
                        else
                        {
                            // For just "C:", use current directory on that drive or root if none
                            // This is critical - in Windows, "C:" means "current directory on C:"
                            string targetDir = $"{driveLetter}:\\";

                            // Try to change to the drive
                            Directory.SetCurrentDirectory(targetDir);
                            Environment.CurrentDirectory = targetDir;
                        }

                        return $"Changed to: {Environment.CurrentDirectory}";
                    }
                    catch (Exception driveEx)
                    {
                        _logger.LogError(driveEx, $"Error changing to drive {driveLetter}:");
                        return $"Error accessing drive {driveLetter}: - {driveEx.Message}";
                    }
                }

                // Handle existing directory commands
                if (cmd.ToLower() == "cd.." || cmd.ToLower() == "cd .." || cmd.ToLower() == "..")
                {
                    if (cmd.ToLower() == "cd..")
                    {
                        cmd = "cd ..";
                    }
                    // Handle cd.. - move up one directory
                    var dirInfo = new DirectoryInfo(Environment.CurrentDirectory);
                    if (dirInfo.Parent != null)
                    {
                        Environment.CurrentDirectory = dirInfo.Parent.FullName;                        
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else if (cmd.ToLower().StartsWith("cd ") || cmd.ToLower().StartsWith("chdir "))
                {
                    // Handle cd <path> command
                    string path = cmd.Substring(cmd.IndexOf(' ') + 1).Trim();

                    // Handle cd /d D:\path syntax - special case for drive changes
                    if (path.StartsWith("/d ") || path.StartsWith("/D "))
                    {
                        path = path.Substring(3).Trim();

                        // Check if this is a drive change
                        if (path.Length >= 2 && path[1] == ':')
                        {
                            // This is a cross-drive path change with /d switch
                            char driveLetter = path[0];
                            if (!Directory.GetLogicalDrives().Contains($"{driveLetter}:\\"))
                            {
                                return $"The drive {driveLetter}: does not exist or is not ready.";
                            }
                        }
                    }

                    // Handle quoted paths
                    if (path.StartsWith("\"") && path.EndsWith("\""))
                        path = path.Substring(1, path.Length - 2);

                    // Handle cd with no arguments (go to home directory)
                    if (string.IsNullOrEmpty(path))
                    {
                        Environment.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        //return $"Changed directory to: {Environment.CurrentDirectory}";
                        return string.Empty;
                    }

                    // Try to change directory
                    if (Directory.Exists(path))
                    {
                        Environment.CurrentDirectory = Path.GetFullPath(path);
                    }
                    else if (Path.IsPathRooted(path))
                    {
                        // Absolute path that doesn't exist
                        return $"The system cannot find the path specified: {path}";
                    }
                    else
                    {
                        // Relative path
                        string combinedPath = Path.Combine(Environment.CurrentDirectory, path);
                        if (Directory.Exists(combinedPath))
                        {
                            Environment.CurrentDirectory = Path.GetFullPath(combinedPath);
                        }
                        else
                        {
                            return $"The system cannot find the path specified: {path}";
                        }
                    }

                    //return $"Changed directory to: {Environment.CurrentDirectory}";
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing directory with command: {command}");
                return $"Error changing directory: {ex.Message}";
            }

            //return $"Directory command not processed correctly: {command}";
            return string.Empty;
        }

        /// <summary>
        /// Falls back to LLM processing when a direct command fails.
        /// </summary>
        public async Task<string> FallbackToLlmAsync(
            string failedCommand,
            Dictionary<string, VariableEntry> variables,
            List<CommandSession> recentCommands)
        {
            _logger.LogDebug($"Command failed, falling back to LLM: '{failedCommand}'");
            return await _localLlmProvider.GenerateCommandAsync(failedCommand, variables, recentCommands);
        }

        /// <summary>
        /// Gets or creates a cloud LLM provider based on the requested provider name.
        /// Providers are created only once and cached for future use.
        /// </summary>
        private ILlmProvider GetCloudProvider(string providerName)
        {
            // If provider already exists in cache, return it
            if (_cloudProviders.TryGetValue(providerName, out var existingProvider))
            {
                _logger.LogDebug($"Using cached {providerName} provider");
                return existingProvider;
            }
                
            // Check if cloud LLM is enabled
            if (!_cloudConfig.EnableCloudLLM)
                throw new InvalidOperationException("Cloud LLM functionality is disabled in configuration");

            //_logger.LogInformation($"Creating new {providerName} provider");
            
            // Create appropriate provider based on name (only first time)
            ILlmProvider newProvider;
            switch (providerName.ToLowerInvariant())
            {
                case "openai":
                    if (string.IsNullOrEmpty(_cloudConfig.OpenaiApiKey))
                        throw new InvalidOperationException("OpenAI API key not configured");
                        
                    newProvider = new OpenAIProvider(_cloudConfig.OpenaiApiKey, _logger);
                    break;
                    
                case "anthropic":
                    if (string.IsNullOrEmpty(_cloudConfig.ClaudeApiKey))
                        throw new InvalidOperationException("Claude API key not configured");
                        
                    // Replace this when you implement the Claude provider
                    throw new NotImplementedException("Claude provider not yet implemented");
                    
                case "google":
                    if (string.IsNullOrEmpty(_cloudConfig.GeminiApiKey))
                        throw new InvalidOperationException("Gemini API key not configured");

                    newProvider = new GeminiProvider(_cloudConfig.GeminiApiKey, _logger);
                    break;

                default:
                    throw new ArgumentException($"Unsupported cloud provider: {providerName}");
            }

            // Initialize the provider
            newProvider.InitializeAsync().Wait();
            
            // Cache the provider for future use
            _cloudProviders[providerName] = newProvider;
            
            return newProvider;
        }

        /// <summary>
        /// Process input with a specified cloud LLM provider
        /// </summary>
        public async Task<string> ProcessWithCloudLlmAsync(
            string input,
            string provider,
            string model,
            Dictionary<string, VariableEntry> variables,
            List<CommandSession> recentCommands)
        {
            //_logger.LogInformation($"Processing with cloud LLM: {provider} {model}");

            if (!_cloudConfig.EnableCloudLLM)
                return $"Cloud LLM functionality is disabled in configuration. Enable it in settings.";

            try
            {
                // Get the cached provider or create a new one if it doesn't exist
                var cloudProvider = GetCloudProvider(provider);
                
                // Use the provider to generate the command
                return await cloudProvider.GenerateCommandAsync(input, variables, recentCommands);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, $"Configuration error for {provider}");
                return $"{ex.Message}";
            }
            catch (NotImplementedException)
            {
                return $"Cloud LLM integration not implemented yet for {provider}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing with cloud LLM {provider}");
                return $"Error with {provider}: {ex.Message}";
            }
        }
    }
}