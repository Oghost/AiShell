using AiShell.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AiShell.LLM;

// --- DTO Classes for robust serialization ---

#nullable disable

public class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; }
}

public class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; }
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate> Candidates { get; set; }

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback PromptFeedback { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent Content { get; set; }
}

public class GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string BlockReason { get; set; }
}

#nullable restore

// --- Provider Implementation ---

public class GeminiProvider : ILlmProvider, IDisposable
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private bool _disposed = false;

    // CORRECTED API endpoint with a valid model name
    private const string API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    public string ProviderName => "Google Gemini";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public GeminiProvider(string apiKey, ILogger logger)
    {
        _apiKey = apiKey;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<bool> InitializeAsync()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("Gemini API key not provided");
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    public async Task<string> GenerateCommandAsync(string naturalLanguage,
                                                  Dictionary<string, VariableEntry> variables,
                                                  List<Core.CommandSession> recentCommands)
    {
        if (!IsAvailable)
        {
            return "Gemini provider is not available. Please check the API key.";
        }

        try
        {
            var prompt = BuildPrompt(naturalLanguage, variables, recentCommands);

            // Use strongly-typed request object
            var requestBody = new GeminiRequest
            {
                Contents = new List<GeminiContent>
                {
                    new()
                    {
                        Parts = new List<GeminiPart>
                        {
                            new() { Text = prompt }
                        }
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, options);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // The API key should be sent in the x-goog-api-key header
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, API_URL);
            requestMessage.Headers.Add("x-goog-api-key", _apiKey);
            requestMessage.Content = httpContent;

            var response = await _httpClient.SendAsync(requestMessage);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug($"Gemini API response: {response.StatusCode}, Body: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Gemini API error {response.StatusCode}: {responseBody}");
                // Try to provide a more helpful error from the response body if possible
                return $"Gemini API error ({response.StatusCode}). Response: {responseBody}";
            }

            // Deserialize into our strongly-typed response object
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseBody, options);

            // **IMPROVED ERROR HANDLING**: Check if the prompt was blocked by safety filters
            if (!string.IsNullOrEmpty(geminiResponse?.PromptFeedback?.BlockReason))
            {
                var reason = geminiResponse.PromptFeedback.BlockReason;
                _logger.LogWarning($"Gemini request was blocked. Reason: {reason}");
                return $"Error: Request blocked by Gemini for safety reasons ({reason}).";
            }

            // Safely extract the text using LINQ and null-conditional operators
            var generatedText = geminiResponse?.Candidates?
                .FirstOrDefault()?
                .Content?.Parts?
                .FirstOrDefault()?
                .Text;

            if (string.IsNullOrEmpty(generatedText))
            {
                return "No command generated by Gemini. The response was empty.";
            }

            return ExtractCommand(generatedText);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Error deserializing Gemini response.");
            return $"Error: Could not parse the response from Gemini. {jsonEx.Message.Replace("\"", "'")}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating command with Gemini");
            return $"Error: {ex.Message.Replace("\"", "'")}";
        }
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

        // Minor tweak: Clean up markdown code blocks if the model returns them
        response = response.Trim().Trim('`');

        var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

        // The logic here is good, but returning the first non-comment-like line is often safest.
        return lines.FirstOrDefault(line =>
            !line.StartsWith("The command") &&
            !line.StartsWith("This will") &&
            !line.StartsWith("You can") &&
            !line.EndsWith(':'))
            ?? lines.LastOrDefault() ?? "No valid command found in response";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
            _disposed = true;
        }
    }
}