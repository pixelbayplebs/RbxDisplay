using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace RbxDisplay;

internal static partial class Watchdog
{
    private static readonly TimeSpan HeartbeatLimit = TimeSpan.FromSeconds(15);

    [GeneratedRegex("^[a-f0-9]{32}$")]
    private static partial Regex TokenPattern();

    public static bool ParentAlive(SessionData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            using Process parent = Process.GetProcessById(data.ParentPid);
            return !parent.HasExited && parent.StartTime.ToUniversalTime().Ticks == data.ParentTicks;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal static bool TryReadHeartbeat(string path, out DateTime beat)
    {
        beat = default;
        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (!long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
                return false;
            DateTime parsed = new(ticks, DateTimeKind.Utc);
            if (parsed.Year < 2000)
                return false;
            beat = parsed;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // A replace can hide the file or fail the read. Only a newer successful timestamp moves the lease.
    internal static DateTime NoteHeartbeat(DateTime lastBeat, string path)
    {
        if (TryReadHeartbeat(path, out DateTime beat) && beat > lastBeat)
            return beat;
        return lastBeat;
    }

    internal static bool HeartbeatExpired(DateTime utcNow, DateTime lastBeat)
    {
        return utcNow - lastBeat > HeartbeatLimit;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The watchdog must attempt restoration after any failure and then exit.")]
    public static int Run(string token)
    {
        if (!TokenPattern().IsMatch(token))
            return 2;
        try
        {
            if (!File.Exists(Store.SessionPath))
                return 0;
            SessionData data = Store.Read<SessionData>(Store.SessionPath);
            if (data.Token != token)
                return 0;
            Store.Atomic(Store.PathFor("ready", token), "ready");
            // The parent writes a heartbeat before starting this process.
            DateTime lastBeat = DateTime.UtcNow;
            while (File.Exists(Store.SessionPath))
            {
                if (!TryReadSession(token, ref data))
                    return 0;
                lastBeat = NoteHeartbeat(lastBeat, Store.PathFor("heartbeat", token));
                DateTime utcNow = DateTime.UtcNow;
                bool parentGone = ParentIsGone(data);
                bool expired = HeartbeatExpired(utcNow, lastBeat);
                bool stop = File.Exists(Store.PathFor("stop", token));
                if (parentGone || expired || stop)
                {
                    Store.Log("Watchdog stopped the session: " + StopReason(parentGone, expired, utcNow, lastBeat) + ".");
                    Store.Atomic(Store.PathFor("stop", token), "stop");
                    string? error = null;
                    for (int i = 0; i < 3; i++)
                    {
                        error = Recovery.FromJournal(token);
                        if (error == null)
                            break;
                        Thread.Sleep(1000);
                    }

                    if (error != null)
                        Store.Log("Watchdog: " + error);
                    return error == null ? 0 : 1;
                }

                Thread.Sleep(1000);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Store.Log("Watchdog: " + ex.Message);
            Recovery.FromJournal(token);
            return 1;
        }
        finally
        {
            Native.CloseColor();
        }
    }

    private static bool TryReadSession(string token, ref SessionData data)
    {
        try
        {
            SessionData read = Store.Read<SessionData>(Store.SessionPath);
            if (read.Token != token)
                return false;
            data = read;
            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (JsonException)
        {
            return true;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A transient process query must not be treated as a dead parent. Heartbeat expiry still covers a parent that is really gone.")]
    private static bool ParentIsGone(SessionData data)
    {
        try
        {
            return !ParentAlive(data);
        }
        catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
        {
            return false;
        }
    }

    private static string StopReason(bool parentGone, bool expired, DateTime utcNow, DateTime lastBeat)
    {
        if (parentGone)
            return "the parent process is gone";
        if (expired)
            return "the heartbeat is " + (int)Math.Ceiling((utcNow - lastBeat).TotalSeconds) + " seconds old";
        return "a stop file is present";
    }
}
