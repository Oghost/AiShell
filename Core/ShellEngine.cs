using AiShell.Learning;
using AiShell.LLM;
using AiShell.Plugins;
using AiShell.Security;
using AiShell.Storage;
using AiShell.UI;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AiShell.Core;

public class ShellEngine
{
    private readonly ILlmProvider _llmProvider;
    private readonly LocalDataStore _localData;
    private readonly SharedKnowledge _sharedKnowledge;
    private readonly ILogger _logger;
    private readonly ConsoleRenderer _renderer;
    private readonly AutoCompleteEngine _autoComplete;
    private readonly PluginManager _pluginManager;
    private readonly CommandLearner _learner;
    private readonly PermissionManager _permissionManager;
    private readonly SessionManager _sessionManager;
    private readonly CommandProcessor _commandProcessor;
    private readonly CloudConfig _cloudConfig;
    private bool _isRunning = true;

    public ShellEngine(ILlmProvider llmProvider, CloudConfig cloudConfig, LocalDataStore localData,
                      SharedKnowledge sharedKnowledge, ILogger logger)
    {
        _llmProvider = llmProvider;
        _cloudConfig = cloudConfig;
        _localData = localData;
        _sharedKnowledge = sharedKnowledge;
        _logger = logger;
        _renderer = new ConsoleRenderer();
        _autoComplete = new AutoCompleteEngine(localData);
        _pluginManager = new PluginManager(logger);
        _learner = new CommandLearner(sharedKnowledge);
        _permissionManager = new PermissionManager();
        _sessionManager = new SessionManager();
        _commandProcessor = new CommandProcessor(llmProvider, _cloudConfig, logger);

        // Load built-in plugins
        _pluginManager.LoadBuiltInPlugins();
    }

    public async Task RunAsync()
    {
        _renderer.ShowWelcome();
        _renderer.ShowWelcomeMessage();

        //_renderer.ShowPrompt(Environment.CurrentDirectory);
        while (_isRunning)
        {
            try
            {
                // Show prompt
                _renderer.ShowPrompt(Environment.CurrentDirectory);

                // Read user input with auto-complete
                var input = await ReadUserInputAsync();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                // Process special command
                if (await ProcessSpecialCommandAsync(input))
                    continue;

                // Process natural language command
               // await ProcessNaturalCommandAsync(input);
               var result = await GenerateNaturalCommandAsync(input);

               Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in shell engine");
                _renderer.ShowError($"Error: {ex.Message}");
            }
        }
    }

    private Task<string> ReadUserInputAsync(string? defaultValue = null)
    {
        if (defaultValue != null)
        {
            // Use Spectre.Console's TextPrompt to allow editing the defaultValue.
            // The prompt message for TextPrompt is set to an empty string,
            // as the context/prompt is already displayed by ShowPrompt and ShowGeneratedCommand.
            //var prompt = new Spectre.Console.TextPrompt<string>("")
            //    .DefaultValue(defaultValue)
            //    .AllowEmpty() // Allows submitting an empty string if the user deletes the default.
            //    .ShowDefaultValue(); // Ensures the default value is visible for editing.

            // AnsiConsole.Prompt is synchronous. Wrap in Task.FromResult for the Task<string> signature.
            // result = AnsiConsole.Write(defaultValue);
            return Task.FromResult(defaultValue); // No need for ?? string.Empty, TextPrompt handles it.
        }
        else
        {
            // Original behavior: Read a new line of input.
            // Console.ReadLine() is synchronous.
            string result = Console.ReadLine() ?? string.Empty;
            return Task.FromResult(result);
        }
    }

    private async Task<bool> ProcessSpecialCommandAsync(string input)
    {
        var trimmed = input.Trim().ToLower();

        return trimmed switch
        {
            "exit" or "quit" => HandleExit(),
            "help" => HandleHelp(),
            "clear" => HandleClear(),
            "version" => HandleVersion(),
            "status" => HandleStatus(),
            var cmd when cmd.StartsWith("config ") => await HandleConfigAsync(cmd),
            var cmd when cmd.StartsWith("alias ") => await HandleAliasAsync(cmd),
            var cmd when cmd.StartsWith("vars") => HandleVarsCommand(),
            var cmd when cmd.StartsWith("plugins") => HandlePluginsCommand(),
            _ => false
        };
    }

    private bool HandleExit()
    {
        _renderer.ShowGoodbye();
        _isRunning = false;
        return true;
    }

    private bool HandleHelp()
    {
        _renderer.ShowHelp();
        return true;
    }

    private bool HandleClear()
    {
        Console.Clear();
        _renderer.ShowWelcomeBanner();
        return true;
    }

    private bool HandleVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        _renderer.ShowInfo($"AiShell v{version?.ToString(3) ?? "1.0.0"}");
        _renderer.ShowInfo($"LLM Provider: {_llmProvider.ProviderName}");
        _renderer.ShowInfo($"Platform: Windows {Environment.OSVersion.Version}");
        _renderer.ShowInfo($"Runtime: .NET {Environment.Version}");
        return true;
    }

    private bool HandleStatus()
    {
        var sessionCommands = _sessionManager.GetAllCommands().Count;
        var successRate = _sessionManager.GetSuccessRate();
        var variables = _localData.GetAllVariables().Count;
        var plugins = _pluginManager.GetAllPlugins().Count;

        _renderer.ShowInfo("=== AiShell Status ===");
        _renderer.ShowInfo($"Session Commands: {sessionCommands}");
        _renderer.ShowInfo($"Success Rate: {successRate:P1}");
        _renderer.ShowInfo($"Variables: {variables}");
        _renderer.ShowInfo($"Plugins Loaded: {plugins}");
        _renderer.ShowInfo($"LLM Available: {(_llmProvider.IsAvailable ? "Yes" : "No")}");
        return true;
    }

    private async Task<bool> HandleConfigAsync(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            _renderer.ShowError("Usage: config <key> [value]");
            return true;
        }

        var key = parts[1];
        if (parts.Length == 2)
        {
            // Get config value
            var value = _localData.GetVariable($"config_{key}");
            if (value != null)
                _renderer.ShowInfo($"{key} = {value}");
            else
                _renderer.ShowError($"Configuration '{key}' not found");
        }
        else
        {
            // Set config value
            var value = string.Join(" ", parts.Skip(2));
            _localData.SetVariable($"config_{key}", value, "config");
            _renderer.ShowSuccess($"Configuration '{key}' set to '{value}'");
        }

        return true;
    }

    private async Task<bool> HandleAliasAsync(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            _renderer.ShowError("Usage: alias <name> [command]");
            return true;
        }

        var aliasName = parts[1];
        if (parts.Length == 2)
        {
            // Show alias
            var aliasValue = _localData.GetAlias(aliasName);
            if (aliasValue != null)
                _renderer.ShowInfo($"{aliasName} = {aliasValue}");
            else
                _renderer.ShowError($"Alias '{aliasName}' not found");
        }
        else
        {
            // Set alias
            var aliasCommand = string.Join(" ", parts.Skip(2));
            _localData.SetAlias(aliasName, aliasCommand);
            _renderer.ShowSuccess($"Alias '{aliasName}' created");
        }

        return true;
    }

    private bool HandleVarsCommand()
    {
        var variables = _localData.GetAllVariables();
        _renderer.ShowVariables(variables);
        return true;
    }

    private bool HandlePluginsCommand()
    {
        var plugins = _pluginManager.GetAllPlugins();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);

        table.AddColumn("[bold]Plugin[/]");
        table.AddColumn("[bold]Keywords[/]");
        table.AddColumn("[bold]Status[/]");

        foreach (var plugin in plugins)
        {
            var status = plugin.RequiresInstallation() ? "[red]Needs Install[/]" : "[green]Ready[/]";
            table.AddRow(
                $"[cyan]{plugin.Name}[/]",
                $"[yellow]{string.Join(", ", plugin.Keywords)}[/]",
                status
            );
        }

        AnsiConsole.Write(table);
        return true;
    }

   
   
async Task<string> GenerateNaturalCommandAsync(string input)
    {
        try
        {
            // Get variables and history
            var variables = _localData.GetAllVariables();
            var recentCommands = _sessionManager.GetRecentCommands();

            // 1. Process the input to determine what kind of command it is
            var processResult = await _commandProcessor.ProcessInputAsync(
                input, variables, recentCommands);

            string command = processResult.Command;
            bool isDirectCommand = processResult.IsDirectCommand;
            bool isCloudLlm = processResult.IsCloudLlm;
            string llmProvider = processResult.LlmProvider;
            string llmModel = processResult.LlmModel;

            // Handle cloud LLM requests specially
            if (isCloudLlm)
            {
                _renderer.ShowInfo($"Using {llmProvider} ({llmModel}) to generate command...");

                // Process with cloud LLM
                var cloudCommandResult = await _commandProcessor.ProcessWithCloudLlmAsync(
                    command, llmProvider, llmModel, variables, recentCommands);

                if (cloudCommandResult == string.Empty)
                {
                    return cloudCommandResult;
                }
                // Show the generated command
                _renderer.ShowPrompt(currentDirectory: Environment.CurrentDirectory, false);
                _renderer.ShowGeneratedCommand(cloudCommandResult);

                // Immediately execute the cloud-generated command
                var cloudExecutionResult = await TryExecuteCommandAsync(cloudCommandResult);

                // Display results
                if (!string.IsNullOrEmpty(cloudExecutionResult.Output))
                    _renderer.ShowCommandOutput(cloudExecutionResult.Output);

                if (!string.IsNullOrEmpty(cloudExecutionResult.Error))
                    _renderer.ShowError(cloudExecutionResult.Error);

                // Record the command
                await _learner.RecordCommandAsync(input, cloudCommandResult, cloudExecutionResult.Success);
                _sessionManager.AddCommand(cloudCommandResult, cloudExecutionResult.Success);

                return cloudCommandResult;
            }

            // 2. For non-cloud commands, execute the command directly
            var executionResult = await TryExecuteCommandAsync(command);

            bool success = executionResult.Success;
            string output = executionResult.Output;
            string error = executionResult.Error;
            int exitCode = executionResult.ExitCode;

            // 3. If command failed with 'not recognized' error, try LLM fallback
            if (!success && IsCommandNotFoundError(error))
            {
                // Generate command with LLM
                var generatedCommand = await _commandProcessor.FallbackToLlmAsync(
                    input, variables, recentCommands);

                generatedCommand = ReadLineWithDefault(generatedCommand);

                string userInputAfterGeneration = await ReadUserInputAsync(generatedCommand); // Pass generatedCommand as default
                string commandToExecuteAfterFallback;

                if (string.IsNullOrWhiteSpace(userInputAfterGeneration))
                {
                    // If user cleared the input or it was empty, use the LLM generated command.
                    // This handles cases where TextPrompt returns "" if AllowEmpty is true and user clears it.
                    commandToExecuteAfterFallback = generatedCommand;
                }
                else
                {
                    // User provided input (either accepted default, edited, or new)
                    commandToExecuteAfterFallback = userInputAfterGeneration;
                }

                // Execute the chosen command (either the original generated one or the user's modification)
                var executionResultFallback = await TryExecuteCommandAsync(commandToExecuteAfterFallback);

                // Display results of the execution
                if (!string.IsNullOrEmpty(executionResultFallback.Output))
                    _renderer.ShowCommandOutput(executionResultFallback.Output);

                if (!string.IsNullOrEmpty(executionResultFallback.Error))
                    _renderer.ShowError(executionResultFallback.Error);

                // Record the command execution
                await _learner.RecordCommandAsync(input, commandToExecuteAfterFallback, executionResultFallback.Success);
                _sessionManager.AddCommand(commandToExecuteAfterFallback, executionResultFallback.Success);

                // Return the command that was actually executed after fallback
                return commandToExecuteAfterFallback;
            }
            else
            {
                // Command was executed directly (or failed for reasons other than 'not found'), show results
                if (!string.IsNullOrEmpty(output))
                    _renderer.ShowCommandOutput(output);

                if (!string.IsNullOrEmpty(error))
                    _renderer.ShowError(error);

                // Update session with direct command
                _sessionManager.AddCommand(command, success);

                return command;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command");
            _renderer.ShowError($"Error processing command: {ex.Message}");
            return string.Empty;
        }
    }

    string ReadLineWithDefault(string defaultValue)
    {        
        _renderer.ShowGeneratedCommand(defaultValue);

        string input = defaultValue;
        int position = defaultValue.Length;

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return input;
            }
            else if (key.Key == ConsoleKey.Backspace && position > 0)
            {
                position--;
                input = input.Remove(position, 1);
                Console.Write("\b \b");
            }
            else if (key.Key == ConsoleKey.Delete && position < input.Length)
            {
                input = input.Remove(position, 1);
                Console.Write(input.Substring(position) + " ");
                Console.SetCursorPosition(Console.CursorLeft - input.Length + position - 1, Console.CursorTop);
            }
            else if (!char.IsControl(key.KeyChar))
            {
                input = input.Insert(position, key.KeyChar.ToString());
                Console.Write(key.KeyChar);
                position++;
            }
        }
    }
    private async Task<(bool Success, string Output, string Error, int ExitCode)> TryExecuteCommandAsync(string command)
    {
        try
        {
            // Before executing a command:
            var assessment = _permissionManager.AssessCommand(command);
            if (assessment.Warnings.Any())
            {
                foreach (var warning in assessment.Warnings)
                    _renderer.ShowWarning(warning);

                // Optionally require confirmation for high/critical risk
                if (assessment.RiskLevel >= RiskLevel.High)
                {
                    _renderer.ShowPrompt("This command is risky. Continue? (y/N): ", false);
                    var confirm = Console.ReadLine();
                    if (!string.Equals(confirm, "y", StringComparison.OrdinalIgnoreCase))
                    {
                        _renderer.ShowInfo("Command cancelled.");
                        return (false, string.Empty, string.Empty, -1);
                    }
                }
            }

            var startTime = DateTime.Now;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                }
            };

            process.Start();

            string output = string.Empty;
            // Asynchronous
            await AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Star)
                .StartAsync("Running...", async ctx =>
                {
                    // Omitted
                    output = await process.StandardOutput.ReadToEndAsync();
                });
                        
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            //var executionTime = DateTime.Now - startTime;
            //_renderer.ShowCommandResult(process.ExitCode == 0, process.ExitCode, executionTime);

            return (process.ExitCode == 0, output, error, process.ExitCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing command: {command}");
            return (false, string.Empty, ex.Message, -1);
        }
    }

    private bool IsCommandNotFoundError(string errorText)
    {
        if (string.IsNullOrEmpty(errorText))
            return false;

        return errorText.Contains("is not recognized as an internal or external command") ||
               errorText.Contains("not found") ||
               errorText.Contains("cannot find the path specified") ||
               errorText.Contains("is not recognized as a cmdlet");
    }
        
}