using System;
using System.Globalization;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace RbxDisplay;

internal static class Program
{
    private static Mutex? instance;

    [STAThread]
    private static int Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        instance = new Mutex(true, "Local\\RbxDisplay.Main", out bool first);
        if (!first)
        {
            NativeWindow.Message("RbxDisplay is already running. Open it from the system tray.", "RbxDisplay");
            instance.Dispose();
            return 0;
        }

        try
        {
            if (args.Length == 1 && args[0] == "--restore")
            {
                string? error = Recovery.FromJournal(null);
                NativeWindow.Message(error ?? "Display settings restored. If no session was saved, the display was left unchanged.", "RbxDisplay");
                return error == null ? 0 : 1;
            }

            NativeWindow.SetAppIdentity();
            ResourceSetup.Ensure();
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                RbxDisplayApp app = new();
                GC.KeepAlive(app);
            });
            return 0;
        }
        catch (Exception ex)
        {
            Store.Log(ex.ToString());
            string? restore = Recovery.FromJournal(null);
            NativeWindow.Message(ex.Message + (restore == null ? "" : "\nRestoration needs attention: " + restore), "RbxDisplay");
            return 1;
        }
        finally
        {
            Native.CloseColor();
            instance.ReleaseMutex();
            instance.Dispose();
        }
    }
}
