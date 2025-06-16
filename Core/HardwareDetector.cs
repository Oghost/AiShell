using AiShell.LLM;
using Microsoft.Extensions.Logging;
using System.Management;
using System.Text.Json;

namespace AiShell.Core;

public class HardwareDetector
{
    private readonly ILogger? _logger;

    public HardwareDetector(ILogger? logger = null)
    {
        _logger = logger;
    }

    public async Task<RuntimeStrategy> DetermineRuntimeStrategyAsync(ModelConfig config)
    {
        var strategy = new RuntimeStrategy
        {
            Quantization = config.Quantization,
            MaxMemoryMB = config.MaxMemoryMB
        };

        try
        {
            // Detect NVIDIA GPU with CUDA
            if (await DetectCudaAsync())
            {
                strategy.UseCuda = true;
                strategy.Provider = "CUDA";
                strategy.Quantization = ModelQuantization.FP16; // Better for GPU
                _logger?.LogInformation("CUDA GPU detected - using GPU acceleration");
                return strategy;
            }

            // Detect DirectML support (Windows 10+)
            if (DetectDirectML())
            {
                strategy.UseDirectML = true;
                strategy.Provider = "DirectML";
                strategy.Quantization = ModelQuantization.FP16;
                _logger?.LogInformation("DirectML support detected - using DirectML acceleration");
                return strategy;
            }

            // Detect Intel GPU/CPU with OpenVINO
            if (DetectOpenVINO())
            {
                strategy.UseOpenVINO = true;
                strategy.Provider = "OpenVINO";
                _logger?.LogInformation("Intel hardware detected - using OpenVINO acceleration");
                return strategy;
            }

            // Fallback to CPU
            strategy.Provider = "CPU";
            strategy.Quantization = ModelQuantization.INT8; // More efficient for CPU
            _logger?.LogInformation("Using CPU execution");

        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting hardware, falling back to CPU");
            strategy.Provider = "CPU";
            strategy.Quantization = ModelQuantization.INT8;
        }
               

        // After hardware and memory detection, before returning:
        strategy.Quantization = QuantizationSelector.SelectQuantization(strategy);

        return strategy;
    }

    private async Task<bool> DetectCudaAsync()
    {
        try
        {
            // Check for NVIDIA GPU
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogInformation($"Found NVIDIA GPU: {name}");
                    
                    // Try to detect CUDA runtime
                    return await CheckCudaRuntimeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error checking for NVIDIA GPU");
        }

        return false;
    }

    private async Task<bool> CheckCudaRuntimeAsync()
    {
        try
        {
            // Check if nvidia-smi is available
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    _logger?.LogInformation($"CUDA runtime available. GPU: {output.Trim()}");
                    return true;
                }
            }
        }
        catch
        {
            // nvidia-smi not found
        }

        return false;
    }

    private bool DetectDirectML()
    {
        try
        {
            // DirectML is available on Windows 10 version 1903 and later
            var version = Environment.OSVersion.Version;
            if (version.Major >= 10 && version.Build >= 18362)
            {
                _logger?.LogInformation("DirectML support available on this Windows version");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error checking DirectML support");
        }

        return false;
    }

    private bool DetectOpenVINO()
    {
        try
        {
            // Check for Intel hardware
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogInformation($"Found Intel CPU: {name}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error checking for Intel hardware");
        }

        return false;
    }

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var info = new SystemInfo
        {
            OS = $"Windows {Environment.OSVersion.Version}",
            Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
            ProcessorCount = Environment.ProcessorCount,
            TotalMemoryMB = await GetTotalMemoryMBAsync(),
            UserName = Environment.UserName,
            MachineName = Environment.MachineName,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            info.GPUs = await GetGPUInfoAsync();
            info.CPUInfo = await GetCPUInfoAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error getting detailed system info");
        }

        return info;
    }

    private async Task<long> GetTotalMemoryMBAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["TotalPhysicalMemory"] != null)
                {
                    var bytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    return bytes / (1024 * 1024); // Convert to MB
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error getting total memory");
        }

        return 0;
    }

    private async Task<List<string>> GetGPUInfoAsync()
    {
        var gpus = new List<string>();
        
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrEmpty(name) && !name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                {
                    gpus.Add(name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error getting GPU info");
        }

        return gpus;
    }

    private async Task<string> GetCPUInfoAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["Name"]?.ToString() ?? "Unknown CPU";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error getting CPU info");
        }

        return "Unknown CPU";
    }
}

public class SystemInfo
{
    public string OS { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public long TotalMemoryMB { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<string> GPUs { get; set; } = new();
    public string CPUInfo { get; set; } = string.Empty;
}