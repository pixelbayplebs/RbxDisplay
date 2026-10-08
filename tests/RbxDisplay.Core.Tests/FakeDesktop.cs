using System.IO;

namespace RbxDisplay;

internal sealed class FakeDesktop : IDesktop
{
    public string MonitorIdentity = "monitor-A";
    public DisplayMode? Mode;
    public float[]? Color;
    public bool FailMode;
    public bool FailColor;
    public int ModeWrites;
    public int ColorWrites;

    public string? Identity(string? device)
    {
        return MonitorIdentity;
    }

    public DisplayMode Current(string? device)
    {
        return Mode!;
    }

    public void SetMode(string? device, DisplayMode? mode)
    {
        if (FailMode)
            throw new IOException("simulated mode failure");
        ModeWrites++;
        Mode = mode;
    }

    public float[] ReadColor()
    {
        if (FailColor)
            throw new IOException("simulated color failure");
        return Color!;
    }

    public void SetColor(float[]? color)
    {
        ColorWrites++;
        Color = color;
    }
}
