using AiShell.Core;
using AiShell.LLM;
using AiShell.Storage;
using AiShell.UI;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiShell;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configure logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<Program>();

        PrintWelcomeBanner();

        // 1. Check first run
        var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "AppConfig.json");
                                     
        var isFirstRun = !File.Exists(configPath);

        if (isFirstRun)
        {
            Console.WriteLine("   Welcome to AiShell!");
            Console.WriteLine("   Setting up your system for the first time...\n");

            // 2. Smart installation
            var installer = new SmartInstaller(logger);
            var strategy = await installer.DetermineOptimalStrategyAsync();

            // 3. Download optimal model
            await DownloadOptimalModelAsync(strategy.Quantization, logger);

            // 4. Save configuration
            var config = new AppConfig
            {
                Model = new ModelConfig
                {
                    MaxMemoryMB = strategy.MaxMemoryMB,
                    Quantization = strategy.Quantization
                }
            };
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            config.SaveToFile(configPath);

            Console.WriteLine("Setup completed!\n");
        }

        // 5. Load existing configuration
        var appConfig = AppConfig.LoadFromFile(configPath);

        // 6. Determine runtime strategy
        var detector = new HardwareDetector();
        var runtimeStrategy = await detector.DetermineRuntimeStrategyAsync(appConfig.Model);

        // 7. Initialize components
        var localDataStore = new LocalDataStore();
        var sharedKnowledge = new SharedKnowledge();
        var llmProvider = new PhiOptimizedProvider(runtimeStrategy, logger);

        // Initialize the LLM provider before using it
        if (!await llmProvider.InitializeAsync())
        {
            Console.WriteLine("Failed to initialize the LLM provider. Exiting.");
            return;
        }

        // 8. Initialize AiShell
        var shell = new ShellEngine(llmProvider, appConfig.Cloud, localDataStore, sharedKnowledge, logger);

        await shell.RunAsync();
    }

    private static void PrintWelcomeBanner()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
      
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║         AiShell for Windows          ║");
        Console.WriteLine("║   Intelligent Terminal with Local AI ║");
        Console.WriteLine($"║               v{version}                 ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.WriteLine($"User: {Environment.UserName}");
        Console.WriteLine($"WinVer {Environment.OSVersion.Version}");
        Console.WriteLine();
    }

    private static async Task DownloadOptimalModelAsync(ModelQuantization quantization, ILogger logger)
    {
        var modelName = quantization switch
        {
            ModelQuantization.FP16 => "phi-3.5-mini-instruct-fp16.onnx",
            ModelQuantization.INT8 => "phi-3.5-mini-instruct-int8.onnx",
            ModelQuantization.INT4 => "phi-3.5-mini-instruct-int4.onnx",
            _ => "phi-3.5-mini-instruct-int8.onnx"
        };

        Console.WriteLine($"Downloading optimized model: {modelName}");

        var modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        Directory.CreateDirectory(modelsDir);

        var modelPath = Path.Combine(modelsDir, modelName);

        if (!File.Exists(modelPath))
        {
            // Hugging Face URL
            var downloadUrl = $"https://huggingface.co/microsoft/Phi-3.5-mini-instruct-onnx/resolve/main/cpu_and_mobile/cpu-{quantization.ToString().ToLower()}/{modelName}";

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(15); // Large models need time

                Console.WriteLine($"Connecting to Hugging Face...");
                var response = await client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                await using var fileStream = File.Create(modelPath);
                await using var downloadStream = await response.Content.ReadAsStreamAsync();

                var buffer = new byte[8192];
                int bytesRead;

                Console.WriteLine($"📊 Downloading {totalBytes / (1024 * 1024):F1} MB...");

                while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (double)downloadedBytes / totalBytes * 100;
                        Console.Write($"\r📥 Progress: {progress:F1}% ({downloadedBytes / (1024 * 1024):F1} MB)");
                    }
                }

                Console.WriteLine("\n Model downloaded successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error downloading model");
                Console.WriteLine($"\n Error downloading model: {ex.Message}");
                Console.WriteLine("💡 You can download manually from:");
                Console.WriteLine($"   {downloadUrl}");
                Console.WriteLine($"   And place it at: {modelPath}");

                // Create placeholder file for development
                await File.WriteAllTextAsync(modelPath, "// Model placeholder - replace with real model");
                Console.WriteLine("🔧 Created placeholder file for development");
            }
        }
        else
        {
            Console.WriteLine("Model already exists locally");
        }
    }
}