using System;

namespace RbxDisplay;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--watchdog")
                return Watchdog.Run(args[1]);
            if (args.Length == 1 && args[0] == "--restore")
            {
                string? error = Recovery.FromJournal(null);
                if (error != null)
                {
                    Store.Log("Manual restoration: " + error);
                    return 1;
                }

                return 0;
            }

            return 2;
        }
        finally
        {
            Native.CloseColor();
        }
    }
}
