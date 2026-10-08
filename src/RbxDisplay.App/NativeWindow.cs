using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RbxDisplay;

internal sealed class NativeWindow : IDisposable
{
    private const uint TrayMessage = 0x8000 + 41;
    private const uint WmNull = 0;
    private const uint WmHotkey = 0x0312;
    private const uint WmDisplayChange = 0x007E;
    private const uint WmContextMenu = 0x007B;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint NimAdd = 0;
    private const uint NimModify = 1;
    private const uint NimDelete = 2;
    private const uint NifMessage = 1;
    private const uint NifIcon = 2;
    private const uint NifTip = 4;
    private const uint MfString = 0;
    private const uint MfGrayed = 0x1;
    private const uint MfDisabled = 0x2;
    private const uint MfSeparator = 0x800;
    private const uint TpmReturnCmd = 0x100;
    private const uint TpmRightButton = 0x2;
    private const uint MbIconInformation = 0x40;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x10;
    private const int IconSize = 32;
    private const int TipLimit = 127;
    private const int SubclassId = 1;

    private readonly IntPtr hwnd;
    private readonly SubclassProc procedure;
    private readonly Action show;
    private readonly Action restore;
    private readonly Action toggleMonitoring;
    private readonly Action exit;
    private readonly Action displayChanged;
    private readonly uint taskbarCreated;
    private IntPtr icon;
    private bool added;
    private bool emergencyReady;
    private bool monitorReady;
    private bool disposed;
    private string tooltip = "RbxDisplay — ready";
    private string menuStatus = "Monitoring";

    public bool TrayAvailable => added;
    public bool EmergencyReady => emergencyReady;
    public bool MonitorReady => monitorReady;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string? Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string? Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string? InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id, UIntPtr data);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr instance, string file, uint type, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string id);

    public static void Message(string text, string title)
    {
        _ = MessageBox(IntPtr.Zero, text, title, MbIconInformation);
    }

    public static void SetAppIdentity()
    {
        _ = SetCurrentProcessExplicitAppUserModelID("RbxDisplay.App");
    }

    public NativeWindow(IntPtr window, Action show, Action restore, Action toggleMonitoring, Action exit, Action displayChanged)
    {
        hwnd = window;
        this.show = show;
        this.restore = restore;
        this.toggleMonitoring = toggleMonitoring;
        this.exit = exit;
        this.displayChanged = displayChanged;
        procedure = WindowProc;
        taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        if (!SetWindowSubclass(hwnd, procedure, new UIntPtr(SubclassId), UIntPtr.Zero))
            throw new InvalidOperationException("Could not attach the tray and emergency restore handler.");
        icon = LoadImage(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "Assets", "RbxDisplay.ico"), ImageIcon, IconSize, IconSize, LrLoadFromFile);
        AddTray();
    }

    public void ApplyHotkeys(int emergencyModifiers, int emergencyKey, int monitorModifiers, int monitorKey)
    {
        emergencyReady = Native.TryRegisterHotkey(hwnd, Native.EmergencyHotkeyId, emergencyModifiers, emergencyKey);
        monitorReady = Native.TryRegisterHotkey(hwnd, Native.MonitorHotkeyId, monitorModifiers, monitorKey);
    }

    public void SetMenuStatus(string value)
    {
        menuStatus = string.IsNullOrWhiteSpace(value) ? "RbxDisplay" : value.Length > 80 ? value[..80] : value;
    }

    private NotifyIconData Data()
    {
        return new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = hwnd,
            Id = 1,
            Flags = NifMessage | NifIcon | NifTip,
            CallbackMessage = TrayMessage,
            Icon = icon,
            Tip = tooltip,
            Info = "",
            InfoTitle = ""
        };
    }

    private void AddTray()
    {
        if (icon == IntPtr.Zero)
            return;
        NotifyIconData data = Data();
        added = Shell_NotifyIcon(NimAdd, ref data);
    }

    public void UpdateTooltip(string value)
    {
        tooltip = value.Length > TipLimit ? value[..TipLimit] : value;
        if (!added)
            return;
        NotifyIconData data = Data();
        Shell_NotifyIcon(NimModify, ref data);
    }

    private IntPtr WindowProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        // Queue XAML actions after returning to the native message loop.
        if (message == WmHotkey)
        {
            ulong hotkeyId = wParam.ToUInt64();
            if (hotkeyId == Native.EmergencyHotkeyId)
                restore();
            else if (hotkeyId == Native.MonitorHotkeyId)
                toggleMonitoring();
        }
        if (message == WmDisplayChange)
            displayChanged();
        if (taskbarCreated != 0 && message == taskbarCreated)
        {
            AddTray();
            if (!added)
                show();
        }

        if (message == TrayMessage)
        {
            uint notification = (uint)lParam.ToInt64();
            if (notification == WmLButtonUp || notification == WmLButtonDblClk)
                show();
            if (notification == WmRButtonUp || notification == WmContextMenu)
                ShowMenu();
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;
        try
        {
            AppendMenu(menu, MfString | MfGrayed | MfDisabled, UIntPtr.Zero, menuStatus);
            AppendMenu(menu, MfSeparator, UIntPtr.Zero, null);
            AppendMenu(menu, MfString, new UIntPtr(1), "Open RbxDisplay");
            AppendMenu(menu, MfString, new UIntPtr(2), "Restore display");
            AppendMenu(menu, MfSeparator, UIntPtr.Zero, null);
            AppendMenu(menu, MfString, new UIntPtr(3), "Exit RbxDisplay");
            GetCursorPos(out Point point);
            SetForegroundWindow(hwnd);
            uint selected = TrackPopupMenu(menu, TpmReturnCmd | TpmRightButton, point.X, point.Y, 0, hwnd, IntPtr.Zero);
            PostMessage(hwnd, WmNull, UIntPtr.Zero, IntPtr.Zero);
            if (selected == 1)
                show();
            else if (selected == 2)
                restore();
            else if (selected == 3)
                exit();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Native.RemoveHotkeys(hwnd);
        if (added)
        {
            NotifyIconData data = Data();
            Shell_NotifyIcon(NimDelete, ref data);
            added = false;
        }

        RemoveWindowSubclass(hwnd, procedure, new UIntPtr(SubclassId));
        if (icon != IntPtr.Zero)
        {
            DestroyIcon(icon);
            icon = IntPtr.Zero;
        }

        GC.KeepAlive(procedure);
    }
}
