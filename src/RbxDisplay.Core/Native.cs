using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace RbxDisplay;

internal static class Native
{
    private const int EnumCurrentSettings = -1;
    private const uint DisplayDeviceAttachedToDesktop = 0x1;
    private const uint DisplayDevicePrimaryDevice = 0x4;
    private const uint DisplayDeviceMirroringDriver = 0x8;
    private const uint EddGetDeviceInterfaceName = 0x1;
    private const uint CdsTest = 0x2;
    private const uint CdsDynamic = 0;
    private const uint DmBitsPerPel = 0x00040000;
    private const uint DmPelsWidth = 0x00080000;
    private const uint DmPelsHeight = 0x00100000;
    private const uint DmDisplayFlags = 0x00200000;
    private const uint DmDisplayFrequency = 0x00400000;
    private const uint DmDisplayOrientation = 0x00000080;
    private const uint DisplayModeFields = DmBitsPerPel | DmPelsWidth | DmPelsHeight | DmDisplayFrequency | DmDisplayFlags | DmDisplayOrientation;
    public const int EmergencyHotkeyId = 1;
    public const int MonitorHotkeyId = 2;
    private const uint ModNoRepeat = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string? DeviceName;
        public ushort SpecVersion, DriverVersion, Size, DriverExtra;
        public uint Fields;
        public int PositionX, PositionY;
        public uint Orientation, FixedOutput;
        public short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string? FormName;
        public ushort LogPixels;
        public uint Bits, Width, Height, Flags, Frequency, ICMMethod, ICMIntent;
        public uint MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string? DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string? DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string? DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string? DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)] public float[]? Values;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? device, int mode, ref DevMode value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice value, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string? device, ref DevMode mode, IntPtr hwnd, uint flags, IntPtr param);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int key);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("Magnification.dll", SetLastError = true)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    private static extern bool MagUninitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    private static extern bool MagGetFullscreenColorEffect(ref ColorEffect effect);

    [DllImport("Magnification.dll", SetLastError = true)]
    private static extern bool MagSetFullscreenColorEffect(ref ColorEffect effect);

    private static bool colorInitialized;

    private static DevMode EmptyMode()
    {
        return new DevMode { Size = (ushort)Marshal.SizeOf<DevMode>() };
    }

    public static int ModeSize => Marshal.SizeOf<DevMode>();

    public static List<MonitorInfo> Monitors()
    {
        List<MonitorInfo> result = [];
        for (uint i = 0; i < 64; i++)
        {
            DisplayDevice adapter = new() { Size = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, i, ref adapter, 0))
                break;
            if ((adapter.StateFlags & DisplayDeviceAttachedToDesktop) == 0 || (adapter.StateFlags & DisplayDeviceMirroringDriver) != 0)
                continue;
            DisplayDevice monitor = new() { Size = Marshal.SizeOf<DisplayDevice>() };
            bool found = EnumDisplayDevices(adapter.DeviceName, 0, ref monitor, EddGetDeviceInterfaceName);
            result.Add(new MonitorInfo
            {
                Device = adapter.DeviceName ?? "",
                Identity = found && !string.IsNullOrEmpty(monitor.DeviceId) ? monitor.DeviceId ?? "" : adapter.DeviceId ?? "",
                Name = found ? monitor.DeviceString ?? "" : adapter.DeviceString ?? "",
                Primary = (adapter.StateFlags & DisplayDevicePrimaryDevice) != 0
            });
        }

        return result;
    }

    public static MonitorInfo? Monitor(string? device)
    {
        return Monitors().SingleOrDefault(monitor => string.Equals(monitor.Device, device, StringComparison.OrdinalIgnoreCase));
    }

    private static DisplayMode Snapshot(DevMode mode)
    {
        return new DisplayMode
        {
            Width = (int)mode.Width,
            Height = (int)mode.Height,
            Refresh = (int)mode.Frequency,
            Bits = (int)mode.Bits,
            Orientation = (int)mode.Orientation,
            Flags = (int)mode.Flags
        };
    }

    public static DisplayMode Current(string? device)
    {
        DevMode mode = EmptyMode();
        if (!EnumDisplaySettings(device, EnumCurrentSettings, ref mode))
            throw new InvalidOperationException("Cannot read the display mode for " + device + ".");
        return Snapshot(mode);
    }

    private static DevMode FindMode(string? device, DisplayMode? desired)
    {
        for (int i = 0; i < 8192; i++)
        {
            DevMode candidate = EmptyMode();
            if (!EnumDisplaySettings(device, i, ref candidate))
                break;
            if (Rules.SameMode(Snapshot(candidate), desired))
                return candidate;
        }

        throw new InvalidOperationException("The driver does not support " + desired + ". Select a supported driver mode. RbxDisplay will not lower the refresh rate automatically.");
    }

    public static void ValidateMode(string? device, DisplayMode? desired)
    {
        DevMode candidate = FindMode(device, desired);
        candidate.Fields = DisplayModeFields;
        int result = ChangeDisplaySettingsEx(device, ref candidate, IntPtr.Zero, CdsTest, IntPtr.Zero);
        if (result != 0)
            throw new InvalidOperationException("Display mode validation failed (Windows: " + result + "). The display was not changed.");
    }

    public static void SetMode(string? device, DisplayMode? desired)
    {
        DevMode candidate = FindMode(device, desired);
        candidate.Fields = DisplayModeFields;
        // Dynamic change only. Never write a stretched mode to the Windows registry.
        int result = ChangeDisplaySettingsEx(device, ref candidate, IntPtr.Zero, CdsDynamic, IntPtr.Zero);
        if (result != 0)
            throw new InvalidOperationException("Changing the display mode failed (Windows: " + result + ").");
        if (!Rules.SameMode(Current(device), desired))
            throw new InvalidOperationException("The driver reported a different display mode than requested.");
    }

    public static List<DisplayMode> DisplayModes(string? device)
    {
        List<DisplayMode> modes = [];
        for (int i = 0; i < 8192; i++)
        {
            DevMode mode = EmptyMode();
            if (!EnumDisplaySettings(device, i, ref mode))
                break;
            modes.Add(Snapshot(mode));
        }

        return modes;
    }

    public static int ForegroundPid()
    {
        // The thread id is unused. The process id is returned through the out argument.
#pragma warning disable CA1806
        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
#pragma warning restore CA1806
        return (int)pid;
    }

    public static bool TryRegisterHotkey(IntPtr hwnd, int id, int modifiers, int key)
    {
        UnregisterHotKey(hwnd, id);
        if (key == 0)
            return true;
        return RegisterHotKey(hwnd, id, (uint)modifiers | ModNoRepeat, (uint)key);
    }

    public static void RemoveHotkeys(IntPtr hwnd)
    {
        UnregisterHotKey(hwnd, EmergencyHotkeyId);
        UnregisterHotKey(hwnd, MonitorHotkeyId);
    }

    public static int PressedModifiers()
    {
        int mods = 0;
        if (IsDown(0x11))
            mods |= 0x2;
        if (IsDown(0x12))
            mods |= 0x1;
        if (IsDown(0x10))
            mods |= 0x4;
        if (IsDown(0x5B) || IsDown(0x5C))
            mods |= 0x8;
        return mods;
    }

    private static bool IsDown(int key)
    {
        return (GetKeyState(key) & 0x8000) != 0;
    }

    public static bool InitializeColor()
    {
        if (colorInitialized)
            return true;
        colorInitialized = MagInitialize();
        return colorInitialized;
    }

    public static float[] ReadColor()
    {
        if (!InitializeColor())
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The Windows color API is unavailable.");
        ColorEffect effect = new() { Values = new float[25] };
        if (!MagGetFullscreenColorEffect(ref effect))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read the current color transform.");
        return effect.Values!;
    }

    public static void SetColor(float[]? values)
    {
        if (values == null || values.Length != 25)
            throw new ArgumentException("Invalid color transform.");
        if (!InitializeColor())
            throw new Win32Exception(Marshal.GetLastWin32Error());
        ColorEffect effect = new() { Values = values };
        if (!MagSetFullscreenColorEffect(ref effect))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not apply the color transform.");
    }

    public static void CloseColor()
    {
        if (!colorInitialized)
            return;
        MagUninitialize();
        colorInitialized = false;
    }
}
