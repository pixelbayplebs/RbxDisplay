using System.Threading;
using Xunit;

namespace Stretcher;

public sealed class RecoveryLockTests
{
    [Fact(DisplayName = "watchdog cannot restore between arming the journal and committing a display change")]
    public void HeldLockBlocksAnotherWaiter()
    {
        using (RecoveryLock.Acquire())
        {
            // A pool task can be inlined onto the owner. A mutex is reentrant there, so the waiter needs its own thread.
            bool blocked = false;
            Thread waiter = new(() =>
            {
                using (Mutex recovery = new Mutex(false, "Local\\RobloxDisplayProfile.Recovery"))
                {
                    bool entered = recovery.WaitOne(0);
                    if (entered)
                        recovery.ReleaseMutex();
                    blocked = !entered;
                }
            });
            waiter.Start();
            waiter.Join();
            Assert.True(blocked);
        }
    }

    [Fact(DisplayName = "recovery lock is released after the display operation")]
    public void LockIsReleasedOnDispose()
    {
        using (RecoveryLock.Acquire())
        {
        }

        using (Mutex recovery = new Mutex(false, "Local\\RobloxDisplayProfile.Recovery"))
        {
            bool entered = recovery.WaitOne(0);
            Assert.True(entered);
            if (entered)
                recovery.ReleaseMutex();
        }
    }
}
