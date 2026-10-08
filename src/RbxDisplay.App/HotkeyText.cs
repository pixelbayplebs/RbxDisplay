using System.Collections.Generic;
using Windows.System;

namespace RbxDisplay;

internal static class HotkeyText
{
    public static string Describe(int modifiers, int key)
    {
        if (key == 0)
            return "Not assigned";
        List<string> parts = [];
        if ((modifiers & 0x2) != 0)
            parts.Add("Ctrl");
        if ((modifiers & 0x1) != 0)
            parts.Add("Alt");
        if ((modifiers & 0x4) != 0)
            parts.Add("Shift");
        if ((modifiers & 0x8) != 0)
            parts.Add("Win");
        parts.Add(Name(key));
        return string.Join(" + ", parts);
    }

    public static bool IsModifier(VirtualKey key)
    {
        return key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
            or VirtualKey.LeftWindows or VirtualKey.RightWindows;
    }

    private static string Name(int key)
    {
        return System.Enum.IsDefined(typeof(VirtualKey), key) ? ((VirtualKey)key).ToString() : "Key " + key.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
