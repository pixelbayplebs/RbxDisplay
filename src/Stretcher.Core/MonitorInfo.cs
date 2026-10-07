namespace Stretcher;

public sealed class MonitorInfo
{
    public string Device { get; set; } = "";
    public string Identity { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Primary { get; set; }

    public override string ToString()
    {
        return Device + "  ·  " + Name + (Primary ? " (primary)" : "");
    }
}
