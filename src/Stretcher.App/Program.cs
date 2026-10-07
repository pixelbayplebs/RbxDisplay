using System;
using System.Globalization;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Stretcher;

internal static class Program
{
    private static Mutex? instance;

    [STAThread]
    private static int Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        // Shared with earlier releases: two versions must never control one desktop.
        instance = new Mutex(true, "Local\\RobloxDisplayProfile.Main", out bool first);
        if (!first)
        {
            NativeWindow.Message("Stretcher or an earlier version is already running. Open it from the system tray.", "Stretcher");
            instance.Dispose();
            return 0;
        }

        try
        {
            if (args.Length == 1 && args[0] == "--restore")
            {
                string? error = Recovery.FromJournal(null);
                NativeWindow.Message(error ?? "Display settings restored. If no session was saved, the display was left unchanged.", "Stretcher");
                return error == null ? 0 : 1;
            }

            NativeWindow.SetAppIdentity();
            ResourceSetup.Ensure();
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                StretcherApp app = new();
                GC.KeepAlive(app);
            });
            return 0;
        }
        catch (Exception ex)
        {
            Store.Log(ex.ToString());
            string? restore = Recovery.FromJournal(null);
            NativeWindow.Message(ex.Message + (restore == null ? "" : "\nRestoration needs attention: " + restore), "Stretcher");
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
