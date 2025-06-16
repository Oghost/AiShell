using AiShell.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace AiShell.LLM;

public class OpenAIProvider : ILlmProvider, IDisposable
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private bool _disposed = false;
    
    // OpenAI API endpoint constant
    private const string API_URL = "https://api.openai.com/v1/chat/completions";

    public string ProviderName => "OpenAI GPT";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public OpenAIProvider(string apiKey, ILogger logger)
    {
        _apiKey = apiKey;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> InitializeAsync()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("OpenAI API key not provided");
            return false;
        }
        return true;
    }

    public async Task<string> GenerateCommandAsync(string naturalLanguage,
                                                 Dictionary<string, VariableEntry> variables,
                                                 List<Core.CommandSession> recentCommands)
    {
        try
        {
            var prompt = BuildPrompt(naturalLanguage, variables, recentCommands);
            
            var model = "gpt-3.5-turbo"; // safest default

            if (naturalLanguage.StartsWith("gpt4 ", StringComparison.OrdinalIgnoreCase) ||
                naturalLanguage.StartsWith("gpt-4 ", StringComparison.OrdinalIgnoreCase))
            {
                model = "gpt-4"; // or "gpt-4-turbo" or "gpt-4o" if you have access
            }

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "You are AiShell, an intelligent Windows terminal assistant." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 150,
                temperature = 0.7
            };

            // Create options with explicit TypeInfoResolver to fix the serialization issue
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, options);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(API_URL, content);
            
            // Log response details for debugging
            var responseBody = await response.Content.ReadAsStringAsync();
            //_logger.LogDebug($"OpenAI API response: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError($"OpenAI API error {response.StatusCode}");
                return string.Empty;
            }

            using (JsonDocument document = JsonDocument.Parse(responseBody))
            {
                var choices = document.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message");
                    var generatedText = message.GetProperty("content").GetString() ?? "";

                    return ExtractCommand(generatedText);
                }
            }

            return "Command could not be generated"; // Clean error message without echo
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating command with OpenAI");
            return $"Error: {ex.Message.Replace("\"", "'")}"; // Replace quotes to avoid escaping issues
        }
    }

    private string HandleErrorResponse(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid API key - please check your OpenAI credentials",
            HttpStatusCode.NotFound => "API endpoint or model not found - verify your OpenAI subscription",
            HttpStatusCode.TooManyRequests => "Rate limit exceeded - please try again later",
            _ => $"OpenAI API error: {statusCode}"
        };
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
        sb.AppendLine("WINDOWS COMMAND:");

        return sb.ToString();
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
                line.StartsWith("You can"))
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
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

public static class OpenAIConfigHelper
{
    public static string? GetOpenAIApiKey(string configPath = "default_config.json")
    {
        if (!File.Exists(configPath))
            return null;

        var json = File.ReadAllText(configPath);
        var root = JsonNode.Parse(json);
        var key = root?["cloud"]?["openaiApiKey"]?.ToString();
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }
}