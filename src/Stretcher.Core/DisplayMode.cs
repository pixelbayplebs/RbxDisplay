namespace Stretcher;

public sealed class DisplayMode
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Refresh { get; set; }
    public int Bits { get; set; }
    public int Orientation { get; set; }
    public int Flags { get; set; }

    public override string ToString()
    {
        return Width + " × " + Height + "  ·  " + Refresh + " Hz";
    }
}
