namespace Stretcher;

public sealed class SessionData
{
    public string? Token { get; set; }
    public string? MonitorDevice { get; set; }
    public string? MonitorIdentity { get; set; }
    public int ParentPid { get; set; }
    public long ParentTicks { get; set; }
    public DisplayMode? OriginalMode { get; set; }
    public DisplayMode? RestoreMode { get; set; }
    public DisplayMode? GameMode { get; set; }
    public DisplayMode? LastOwnedMode { get; set; }
    public float[]? OriginalColor { get; set; }
    public float[]? GameColor { get; set; }
    public bool ModeArmed { get; set; }
    public bool ColorArmed { get; set; }
}
