namespace AiShell.Core;

public class HardwareInfo
{
    public bool HasCuda { get; set; }
    public bool HasDirectML { get; set; }
    public bool HasOpenVINO { get; set; }
    public int TotalMemoryMB { get; set; }
}