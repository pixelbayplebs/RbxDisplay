using System;
using System.Collections.Generic;

namespace Stretcher;

internal interface IDisplayCatalog
{
    List<MonitorInfo> Monitors();
    DisplayMode Current(string device);
    List<DisplayMode> Modes(string device);
    bool Test(string device, DisplayMode mode);
}

internal sealed class WindowsDisplayCatalog : IDisplayCatalog
{
    public List<MonitorInfo> Monitors()
    {
        return Native.Monitors();
    }

    public DisplayMode Current(string device)
    {
        return Native.Current(device);
    }

    public List<DisplayMode> Modes(string device)
    {
        return Native.DisplayModes(device);
    }

    public bool Test(string device, DisplayMode mode)
    {
        try
        {
            Native.ValidateMode(device, mode);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
