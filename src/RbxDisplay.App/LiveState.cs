namespace RbxDisplay;

internal enum LiveTone
{
    Idle,
    Active,
    Warning
}

internal sealed class LiveState
{
    public string Badge { get; init; } = "Monitoring";
    public string Title { get; init; } = "No game detected";
    public string Message { get; init; } = "Monitoring is on. A saved profile applies when that game is in the foreground.";
    public string Resolution { get; init; } = "—";
    public string Saturation { get; init; } = "—";
    public string Monitor { get; init; } = "—";
    public string Focus { get; init; } = "—";
    public string Tray { get; init; } = "RbxDisplay — monitoring";
    public string? GameId { get; init; }
    public LiveTone Tone { get; init; } = LiveTone.Idle;
}
