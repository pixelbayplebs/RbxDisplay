using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;

namespace RbxDisplay;

internal static class Recovery
{
    // Return only actual failures. External changes belong to the user and are left alone.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed color or mode write must be reported and must not hide the other restoration.")]
    public static List<string> Restore(SessionData session, IDesktop desktop)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(desktop);
        List<string> errors = [];
        if (session.ColorArmed)
        {
            try
            {
                float[] current = desktop.ReadColor();
                if (Rules.SameColor(current, session.GameColor))
                {
                    desktop.SetColor(session.OriginalColor);
                    if (!Rules.SameColor(desktop.ReadColor(), session.OriginalColor))
                        throw new InvalidOperationException("Windows did not confirm color restoration.");
                }
                else if (!Rules.SameColor(current, session.OriginalColor))
                {
                    Store.Log("Colors changed externally; restoration skipped.");
                }

                session.ColorArmed = false;
            }
            catch (Exception ex)
            {
                errors.Add("Colors: " + ex.Message);
            }
        }

        if (session.ModeArmed)
        {
            try
            {
                string? identity = desktop.Identity(session.MonitorDevice);
                if (identity != session.MonitorIdentity)
                {
                    // Keep the journal to retry on the exact same monitor after reconnection.
                    throw new InvalidOperationException("The monitor was disconnected or its identity changed. Reconnect the same monitor and use Restore.");
                }

                DisplayMode current = desktop.Current(session.MonitorDevice);
                if (Rules.CanRestoreMode(session.MonitorIdentity, identity, current, session.GameMode))
                {
                    desktop.SetMode(session.MonitorDevice, session.RestoreMode);
                    if (!Rules.SameMode(desktop.Current(session.MonitorDevice), session.RestoreMode))
                        throw new InvalidOperationException("Windows did not confirm display mode restoration.");
                }
                else if (!Rules.SameMode(current, session.OriginalMode) && !Rules.SameMode(current, session.RestoreMode))
                {
                    Store.Log("The display mode changed externally; restoration skipped.");
                }

                session.ModeArmed = false;
            }
            catch (Exception ex)
            {
                errors.Add("Resolution: " + ex.Message);
            }
        }

        return errors;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Restoration must report every failure instead of leaving the desktop unrestored without a message.")]
    public static string? FromJournal(string? expectedToken)
    {
        using (Mutex mutex = new(false, "Local\\RbxDisplay.Recovery"))
        {
            bool held = false;
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
                    return "Another process is restoring the display. Try again.";
                if (!File.Exists(Store.SessionPath))
                    return null;
                SessionData session = Store.Read<SessionData>(Store.SessionPath);
                if (string.IsNullOrWhiteSpace(session.Token))
                    return "The session journal is invalid. No guessed display settings were applied.";
                if (expectedToken != null && session.Token != expectedToken)
                    return null;
                Store.Atomic(Store.PathFor("stop", session.Token), "stop");
                List<string> errors = Restore(session, new WindowsDesktop());
                if (errors.Count > 0)
                {
                    Store.Save(Store.SessionPath, session);
                    return string.Join(Environment.NewLine, errors);
                }

                Store.Delete(Store.SessionPath);
                foreach (string prefix in new[] { "heartbeat", "ready" })
                    Store.Delete(Store.PathFor(prefix, session.Token));
                // stop is retained until the parent acknowledges recovery. It prevents reapplying an expired lease.
                Store.Log("Session settings restored.");
                return null;
            }
            catch (Exception ex)
            {
                Store.Log("Restoration: " + ex.Message);
                return ex.Message;
            }
            finally
            {
                if (held)
                    mutex.ReleaseMutex();
            }
        }
    }
}
