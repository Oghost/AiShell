using AiShell.LLM;
using Microsoft.Extensions.Logging;

namespace AiShell.Core;

public class SmartInstaller
{
    private readonly ILogger _logger;
    private readonly HardwareDetector _hardwareDetector;

    public SmartInstaller(ILogger logger)
    {
        _logger = logger;
        _hardwareDetector = new HardwareDetector(logger);
    }

    public async Task<InstallationStrategy> DetermineOptimalStrategyAsync()
    {
        _logger.LogInformation("Analyzing system for optimal AiShell configuration...");

        var systemInfo = await _hardwareDetector.GetSystemInfoAsync();
        var strategy = new InstallationStrategy();

        // Determine memory allocation
        strategy.MaxMemoryMB = DetermineMemoryAllocation(systemInfo.TotalMemoryMB);
        
        // Determine quantization based on available memory and hardware
        strategy.Quantization = DetermineQuantization(systemInfo);
        
        // Determine execution provider
        strategy.ExecutionProvider = await DetermineExecutionProviderAsync(systemInfo);

        // After hardware and memory detection, before returning:
        strategy.Quantization = QuantizationSelector.SelectQuantization(systemInfo, strategy.ExecutionProvider, strategy.MaxMemoryMB);

        // Check for required dependencies
        _logger.LogInformation($"Recommended configuration:");
        _logger.LogInformation($"- Memory: {strategy.MaxMemoryMB} MB");
        _logger.LogInformation($"- Quantization: {strategy.Quantization}");
        _logger.LogInformation($"- Provider: {strategy.ExecutionProvider}");

        return strategy;
    }

    private int DetermineMemoryAllocation(long totalMemoryMB)
    {
        // Allocate memory based on system capacity
        return totalMemoryMB switch
        {
            < 4096 => 1024,      // 4GB or less - conservative
            < 8192 => 2048,      // 4-8GB - moderate  
            < 16384 => 4096,     // 8-16GB - generous
            < 32768 => 6144,     // 16-32GB - high performance
            _ => 8192            // 32GB+ - maximum performance
        };
    }

    private ModelQuantization DetermineQuantization(SystemInfo systemInfo)
    {
        // Check if we have a dedicated GPU
        var hasNvidiaGPU = systemInfo.GPUs.Any(gpu => 
            gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
            !gpu.Contains("Basic", StringComparison.OrdinalIgnoreCase));

        var hasAMD_GPU = systemInfo.GPUs.Any(gpu => 
            gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase));

        // GPU can handle FP16 better
        if (hasNvidiaGPU || hasAMD_GPU)
        {
            return systemInfo.TotalMemoryMB >= 8192 ? ModelQuantization.FP16 : ModelQuantization.INT8;
        }

        // CPU-only systems
        return systemInfo.TotalMemoryMB >= 16384 ? ModelQuantization.INT8 : ModelQuantization.INT4;
    }

    private async Task<string> DetermineExecutionProviderAsync(SystemInfo systemInfo)
    {
        // Check for CUDA first (best performance)
        var runtimeStrategy = await _hardwareDetector.DetermineRuntimeStrategyAsync(new ModelConfig());
        return runtimeStrategy.Provider;
    }

    public async Task<bool> ValidateInstallationAsync()
    {
        _logger.LogInformation("Validating AiShell installation...");

        try
        {
            // Check if Models directory exists
            var modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
            if (!Directory.Exists(modelsDir))
            {
                Directory.CreateDirectory(modelsDir);
                _logger.LogInformation("Created Models directory");
            }

            // Check if Config directory exists
            var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
                _logger.LogInformation("Created Config directory");
            }

            // Validate user data directory
            var userDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AiShell");
            if (!Directory.Exists(userDataDir))
            {
                Directory.CreateDirectory(userDataDir);
                _logger.LogInformation("Created user data directory");
            }

            // Check write permissions
            await ValidateWritePermissionsAsync(userDataDir);

            _logger.LogInformation("Installation validation completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installation validation failed");
            return false;
        }
    }

    private async Task ValidateWritePermissionsAsync(string directory)
    {
        try
        {
            var testFile = Path.Combine(directory, "test_write.tmp");
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);
            _logger.LogInformation("Write permissions validated");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"No write permissions in {directory}", ex);
        }
    }

    public async Task<List<string>> GetRequiredDependenciesAsync()
    {
        var dependencies = new List<string>();

        try
        {
            var systemInfo = await _hardwareDetector.GetSystemInfoAsync();
            
            // Check for Visual C++ Redistributable
            if (!IsVCRedistInstalled())
            {
                dependencies.Add("Microsoft Visual C++ Redistributable 2022");
            }

            // Check for .NET 8.0
            if (!IsDotNet8Installed())
            {
                dependencies.Add(".NET 8.0 Runtime");
            }

            // Check for specific GPU dependencies
            if (systemInfo.GPUs.Any(gpu => gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)))
            {
                dependencies.Add("NVIDIA GPU Driver (Latest)");
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking dependencies");
        }

        return dependencies;
    }

    private bool IsVCRedistInstalled()
    {
        try
        {
            // Check common locations for VC++ redistributable
            var commonPaths = new[]
            {
                @"C:\Windows\System32\msvcp140.dll",
                @"C:\Windows\System32\vcruntime140.dll"
            };

            return commonPaths.All(File.Exists);
        }
        catch
        {
            return false;
        }
    }

    private bool IsDotNet8Installed()
    {
        try
        {
            return Environment.Version.Major >= 8;
        }
        catch
        {
            return false;
        }
    }
}

public class InstallationStrategy
{
    public int MaxMemoryMB { get; set; } = 2048;
    public ModelQuantization Quantization { get; set; } = ModelQuantization.INT8;
    public string ExecutionProvider { get; set; } = "CPU";
    public List<string> RequiredDependencies { get; set; } = new();
    public bool RequiresElevation { get; set; } = false;
}