using AiShell.Storage;

namespace AiShell.LLM;

public interface ILlmProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    
    Task<string> GenerateCommandAsync(string naturalLanguage, 
                                    Dictionary<string, VariableEntry> variables,
                                    List<Core.CommandSession> recentCommands);
    
    Task<bool> InitializeAsync();
    void Dispose();
}

public enum ModelQuantization
{
    FP16,
    INT8,
    INT4
}

public class RuntimeStrategy
{
    public string Provider { get; set; } = "CPU";
    public ModelQuantization Quantization { get; set; } = ModelQuantization.INT8;
    public int MaxMemoryMB { get; set; } = 2048;
    public bool UseCuda { get; set; } = false;
    public bool UseDirectML { get; set; } = false;
    public bool UseOpenVINO { get; set; } = false;
}