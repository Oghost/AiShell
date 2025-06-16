using AiShell.Storage;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AiShell.LLM;

public class PhiOptimizedProvider : ILlmProvider, IDisposable
{
    private readonly RuntimeStrategy _strategy;
    private readonly ILogger _logger;
    private OgaHandle? _ogaHandle;
    private Model? _model;
    private Tokenizer? _tokenizer;
    private bool _disposed = false;

    public string ProviderName => $"Phi-3.5-mini ({_strategy.Quantization})";
    public bool IsAvailable => _model != null && _tokenizer != null;

    public PhiOptimizedProvider(RuntimeStrategy strategy, ILogger logger)
    {
        _strategy = strategy;
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            var modelPath = GetModelPath();
            if (!Directory.Exists(modelPath))
            {
                _logger.LogError($"Model directory not found: {modelPath}");
                return false;
            }

            // Initialize OGA handle
            _ogaHandle = new OgaHandle();

            // Load model and tokenizer
            _model = new Model(modelPath);
            _tokenizer = new Tokenizer(_model);

            _logger.LogInformation($"Phi-3.5 model loaded successfully with OnnxRuntimeGenAI");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Phi model");
            return false;
        }
    }

    public async Task<string> GenerateCommandAsync(string naturalLanguage,
                                                  Dictionary<string, VariableEntry> variables,
                                                  List<Core.CommandSession> recentCommands)
    {
        if (_model == null || _tokenizer == null)
        {
            throw new InvalidOperationException("LLM provider not initialized");
        }

        try
        {
            var prompt = BuildPrompt(naturalLanguage, variables, recentCommands);
            var response = await GenerateResponseAsync(prompt);

            return ExtractCommand(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating command");
            return $"Error: {ex.Message}";
        }
    }

    private string GetModelPath()
    {
        // For OnnxRuntimeGenAI, we need the model directory, not a specific .onnx file
        var modelDirName = _strategy.Quantization switch
        {
            ModelQuantization.FP16 => "phi-3.5-mini-instruct-fp16",
            ModelQuantization.INT8 => "phi-3.5-mini-instruct-int8",
            ModelQuantization.INT4 => "phi-3.5-mini-instruct-int4",
            _ => "phi-3.5-mini-instruct-int8"
        };

       // return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", modelDirName);
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
    }

    private string BuildPrompt(string naturalLanguage,
                              Dictionary<string, VariableEntry> variables,
                              List<Core.CommandSession> recentCommands)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are AiShell, an intelligent Windows terminal assistant.");
        sb.AppendLine("Convert natural language requests to precise Windows commands.");
        sb.AppendLine();

        sb.AppendLine("RULES:");
        sb.AppendLine("1. Only output the WINDOWS COMMAND, no explanations");
        sb.AppendLine("2. Use cmd.exe compatible syntax");
        sb.AppendLine("3. Be conservative with dangerous operations");
        sb.AppendLine("4. Use provided variables when available");
        sb.AppendLine();

        if (variables.Any())
        {
            sb.AppendLine("AVAILABLE VARIABLES:");
            foreach (var var in variables.Take(10))
            {
                sb.AppendLine($"${var.Key} = {var.Value.Value}");
            }
            sb.AppendLine();
        }

        if (recentCommands.Any())
        {
            sb.AppendLine("RECENT COMMANDS (for context):");
            foreach (var cmd in recentCommands.Take(5))
            {
                sb.AppendLine($"- {cmd.Command}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"USER REQUEST: {naturalLanguage}");
        sb.AppendLine();
        sb.AppendLine("WINDOWS COMMAND:▐");

        return sb.ToString();
    }

    private async Task<string> GenerateResponseAsync(string prompt)
    {
        if (_model == null || _tokenizer == null)
        {
            throw new InvalidOperationException("Model or tokenizer not initialized");
        }

        try
        {
            // Format prompt with chat template (based on your example)
            var formattedPrompt = $"<|user|>{prompt}<|end|><|assistant|>";

            // Tokenize the prompt (based on your example)
            var sequences = _tokenizer.Encode(formattedPrompt);

            // Create generator parameters (based on your example)
            using var generatorParams = new GeneratorParams(_model);
            generatorParams.SetSearchOption("min_length", 10);
            generatorParams.SetSearchOption("max_length", 250);

            // FIXED: Use the correct method from your example
            generatorParams.SetInputSequences(sequences);

            // Option 1: Complete generation (based on your example)
            // FIXED: Use the correct method from your example
            var outputSequences = _model.Generate(generatorParams);
            var outputString = _tokenizer.Decode(outputSequences[0]);

            // Clean up the response
            int startCommand = outputString.IndexOf("▐");
            var response = outputString.Remove(0, startCommand).Replace("<|end|>", "").Trim();
            //var response = outputString.Replace("<|end|>", "").Trim();
            
            var totalTokens = outputSequences[0].Length;

            _logger.LogDebug($"Generated response: {response}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during text generation, falling back to streaming");

            try
            {
                // Fallback to streaming generation (based on your example)
                return await GenerateStreamingResponseAsync(prompt);
            }
            catch (Exception streamEx)
            {
                _logger.LogError(streamEx, "Streaming generation also failed");
                return GenerateFallbackCommand(prompt);
            }
        }
    }

    private async Task<string> GenerateStreamingResponseAsync(string prompt)
    {
        if (_model == null || _tokenizer == null)
        {
            throw new InvalidOperationException("Model or tokenizer not initialized");
        }

        // Format prompt with chat template
        var formattedPrompt = $"<|user|>{prompt}<|end|><|assistant|>";

        // Tokenize the prompt
        var sequences = _tokenizer.Encode(formattedPrompt);

        // Create generator parameters
        using var generatorParams = new GeneratorParams(_model);
        generatorParams.SetSearchOption("min_length", 10);
        generatorParams.SetSearchOption("max_length", 200);
        generatorParams.SetInputSequences(sequences);

        // Streaming generation (based exactly on your example)
        var responseBuilder = new StringBuilder();
        using var tokenizerStream = _tokenizer.CreateStream();
        using var generator = new Generator(_model, generatorParams);

        while (!generator.IsDone())
        {
            generator.ComputeLogits();
            generator.GenerateNextToken();
            var newToken = tokenizerStream.Decode(generator.GetSequence(0)[^1]);
            responseBuilder.Append(newToken);

            // Stop if we encounter end token or command seems complete
            if (newToken.Contains("<|end|>") ||
                (responseBuilder.Length > 50 && newToken.Trim().EndsWith('\n')))
            {
                break;
            }
        }

        var response = responseBuilder.ToString();

        // Clean up the response
        response = response.Replace("<|end|>", "").Trim();

        return response;
    }

    private string GenerateFallbackCommand(string prompt)
    {
        var input = prompt.ToLower();

        if (input.Contains("list") && input.Contains("file"))
            return "dir";
        if (input.Contains("ping"))
            return "ping google.com";
        if (input.Contains("network") || input.Contains("ip"))
            return "ipconfig";
        if (input.Contains("process"))
            return "tasklist";
        if (input.Contains("kill") && input.Contains("process"))
            return "taskkill /f /im notepad.exe";
        if (input.Contains("disk") || input.Contains("space"))
            return "dir /s";

        return "Command not recognized. Try: list files, ping, network info, processes";
    }

    private string ExtractCommand(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "No command generated";
        }

        // Split response into lines and find the actual command
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                           .Select(l => l.Trim())
                           .Where(l => !string.IsNullOrWhiteSpace(l))
                           .ToList();

        // Look for lines that look like commands (not explanatory text)
        foreach (var line in lines)
        {
            // Skip lines that are clearly explanatory
            if (line.StartsWith("The command") ||
                line.StartsWith("This will") ||
                line.StartsWith("You can") ||
                line.Contains("WINDOWS COMMANDñ"))
                continue;

            // Return the first line that looks like a command
            if (line.Length > 2 && !line.EndsWith(':'))
            {
                return line;
            }
        }

        // Fallback to last non-empty line
        return lines.LastOrDefault() ?? "No command generated";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _tokenizer?.Dispose();
            _model?.Dispose();
            _ogaHandle?.Dispose();
            _disposed = true;
        }
    }
}