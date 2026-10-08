using System;
using System.Collections.Generic;
using System.Linq;

namespace RbxDisplay;

internal static class ModeCatalog
{
    public static List<int> RefreshRates(IEnumerable<DisplayMode> modes, DisplayMode reference)
    {
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentNullException.ThrowIfNull(reference);
        return modes.Where(mode => mode.Refresh > 1 && mode.Width >= 640 && mode.Height >= 480 && mode.Bits == reference.Bits && mode.Orientation == reference.Orientation && mode.Flags == reference.Flags)
            .Select(mode => mode.Refresh).Distinct().OrderByDescending(hz => hz).ToList();
    }

    public static List<DisplayMode> AtRefresh(IEnumerable<DisplayMode> modes, DisplayMode reference, int refresh)
    {
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentNullException.ThrowIfNull(reference);
        return modes.Where(mode => mode.Refresh == refresh && mode.Width >= 640 && mode.Height >= 480 &&
                mode.Bits == reference.Bits && mode.Orientation == reference.Orientation && mode.Flags == reference.Flags)
            .GroupBy(mode => mode.Width + "x" + mode.Height).Select(group => group.First())
            .OrderByDescending(mode => (long)mode.Width * mode.Height).ThenByDescending(mode => mode.Width).ToList();
    }

    public static List<DisplayMode> TestedAtRefresh(IEnumerable<DisplayMode> modes, DisplayMode reference, int refresh, Func<DisplayMode, bool> test)
    {
        ArgumentNullException.ThrowIfNull(test);
        return AtRefresh(modes, reference, refresh).Where(test).ToList();
    }

    public static string ResolutionText(DisplayMode? mode)
    {
        if (mode == null)
            return "";
        int a = mode.Width;
        int b = mode.Height;
        while (b != 0)
        {
            int remainder = a % b;
            a = b;
            b = remainder;
        }

        string ratio = a > 0 ? (mode.Width / a) + ":" + (mode.Height / a) : "";
        return mode.Width + " × " + mode.Height + "  ·  " + ratio;
    }
}
