using AiShell.LLM;

namespace AiShell.Core;

public static class QuantizationSelector
{
    public static ModelQuantization SelectQuantization(RuntimeStrategy strategy)
    {
        // Example logic, adjust as needed:
        if (strategy.UseCuda)
            return ModelQuantization.FP16;
        if (strategy.UseDirectML)
            return ModelQuantization.FP16;
        if (strategy.UseOpenVINO)
            return ModelQuantization.INT8;
        if (strategy.MaxMemoryMB >= 3000)
            return ModelQuantization.INT8;
        return ModelQuantization.INT4;
    }

    public static ModelQuantization SelectQuantization(SystemInfo systemInfo, string executionProvider, int maxMemoryMB)
    {
        // Example logic, adjust as needed:
        if (executionProvider == "CUDA" || executionProvider == "DirectML")
            return ModelQuantization.FP16;
        if (executionProvider == "OpenVINO")
            return ModelQuantization.INT8;
        if (maxMemoryMB >= 3000)
            return ModelQuantization.INT8;
        return ModelQuantization.INT4;
    }
}