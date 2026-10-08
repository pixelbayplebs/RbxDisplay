using System;
using System.Threading;

namespace RbxDisplay;

internal sealed class RecoveryLock : IDisposable
{
    private readonly Mutex mutex;
    private bool held;

    private RecoveryLock()
    {
        mutex = new Mutex(false, "Local\\RbxDisplay.Recovery");
        try
        {
            try
            {
                held = mutex.WaitOne(3000);
            }
            catch (AbandonedMutexException)
            {
                held = true;
            }

            if (!held)
                throw new InvalidOperationException("Another process is restoring the display. Try again.");
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    public static RecoveryLock Acquire()
    {
        return new RecoveryLock();
    }

    public void Dispose()
    {
        if (!held)
            return;
        held = false;
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
