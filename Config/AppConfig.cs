    using AiShell.Core;
    using AiShell.LLM;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    namespace AiShell
    {
        // Use source generator context for AOT-safe serialization
        [JsonSerializable(typeof(ModelQuantization))]
        [JsonSerializable(typeof(AppConfig))]
        [JsonSerializable(typeof(ModelConfig))]
        [JsonSerializable(typeof(UiConfig))]
        [JsonSerializable(typeof(SecurityConfig))]
        [JsonSerializable(typeof(LearningConfig))]
        [JsonSerializable(typeof(PluginsConfig))]
        [JsonSerializable(typeof(CloudConfig))]
        [JsonSerializable(typeof(PerformanceConfig))]
        [JsonSerializable(typeof(UpdatesConfig))]

        [JsonSourceGenerationOptions(WriteIndented = true)]
        internal partial class AppConfigJsonContext : JsonSerializerContext
        {
        }

        public class AppConfig
        {
            public ModelConfig Model { get; set; } = new();
            public UiConfig Ui { get; set; } = new();
            public SecurityConfig Security { get; set; } = new();
            public LearningConfig Learning { get; set; } = new();
            public PluginsConfig Plugins { get; set; } = new();
            public CloudConfig Cloud { get; set; } = new();
            public PerformanceConfig Performance { get; set; } = new();
            public UpdatesConfig Updates { get; set; } = new();
            public string Version { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; } = DateTime.Now;
            public DateTime LastUpdated { get; set; } = DateTime.Now;

            public void SaveToFile(string path)
            {
                LastUpdated = DateTime.Now;

                try
                {
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        TypeInfoResolver = AppConfigJsonContext.Default
                    };

                    var json = JsonSerializer.Serialize(this, AppConfigJsonContext.Default.AppConfig);
                    File.WriteAllText(path, json);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to save AppConfig to {path}: {ex.Message}", ex);
                }
            }

            public static AppConfig LoadFromFile(string path)
            {
                if (!File.Exists(path))
                    return new AppConfig();

                try
                {
                    var json = File.ReadAllText(path);

                    var config = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig) as AppConfig;
                    return config ?? new AppConfig();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load AppConfig from {path}: {ex.Message}");
                    return new AppConfig();
                }
            }
        }

        public class ModelConfig
        {
            public ModelQuantization Quantization { get; set; }
            public int MaxMemoryMB { get; set; }
            public bool EnableCuda { get; set; }
            public bool EnableDirectML { get; set; }
            public bool EnableOpenVINO { get; set; }
            public string ModelPath { get; set; } = string.Empty;
        }

        public class UiConfig
        {
            public string Theme { get; set; } = string.Empty;
            public bool ShowBanner { get; set; }
            public bool ShowProgressBars { get; set; }
            public int MaxOutputLines { get; set; }
            public bool EnableColors { get; set; }
            public string PromptStyle { get; set; } = string.Empty;
        }

        public class SecurityConfig
        {
            public bool SafeMode { get; set; }
            public bool RequireConfirmation { get; set; }
            public bool AllowElevation { get; set; }
            public bool TrustedSoftwareOnly { get; set; }
            public int MaxCommandLength { get; set; }
        }

        public class LearningConfig
        {
            public bool EnableLearning { get; set; }
            public bool ShareKnowledge { get; set; }
            public int MaxLearningHistory { get; set; }
            public int AutoSaveInterval { get; set; }
        }

        public class PluginsConfig
        {
            public bool EnableBuiltIn { get; set; }
            public bool AutoInstallDependencies { get; set; }
            public int PluginTimeout { get; set; }
        }

        public class CloudConfig
        {
            public bool EnableCloudLLM { get; set; }
            public string OpenaiApiKey { get; set; } = string.Empty;
            public string ClaudeApiKey { get; set; } = string.Empty;
            public string GeminiApiKey { get; set; } = string.Empty;
            public string DefaultProvider { get; set; } = string.Empty;
        }

        public class PerformanceConfig
        {
            public int MaxConcurrentCommands { get; set; }
            public int CommandTimeout { get; set; }
            public int CacheSize { get; set; }
            public bool EnableTelemetry { get; set; }
        }

        public class UpdatesConfig
        {
            public bool CheckForUpdates { get; set; }
            public bool AutoUpdate { get; set; }
            public string UpdateChannel { get; set; } = string.Empty;
        }

   
    }