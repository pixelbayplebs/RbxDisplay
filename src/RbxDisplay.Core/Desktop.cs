namespace RbxDisplay;

internal interface IDesktop
{
    string? Identity(string? device);
    DisplayMode Current(string? device);
    void SetMode(string? device, DisplayMode? mode);
    float[] ReadColor();
    void SetColor(float[]? color);
}

internal sealed class WindowsDesktop : IDesktop
{
    public string? Identity(string? device)
    {
        MonitorInfo? monitor = Native.Monitor(device);
        return monitor?.Identity;
    }

    public DisplayMode Current(string? device)
    {
        return Native.Current(device);
    }

    public void SetMode(string? device, DisplayMode? mode)
    {
        Native.SetMode(device, mode);
    }

    public float[] ReadColor()
    {
        return Native.ReadColor();
    }

    public void SetColor(float[]? color)
    {
        Native.SetColor(color);
    }
}
