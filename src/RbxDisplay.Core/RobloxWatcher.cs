using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace RbxDisplay;

internal sealed class RobloxWatcher
{
    private const int TailWindowBytes = 2 * 1024 * 1024;
    private const int ChunkBytes = 256 * 1024;
    private const int PendingLimit = 65536;

    private int lastPid;
    private long lastTicks;
    private long offset;
    private string? logFile;
    private string pending = "";
    private readonly LogState state = new();

    internal static FileInfo? SelectLog(string directory, DateTime processStart)
    {
        return new DirectoryInfo(directory).GetFiles("*.log")
            .Where(file => file.Name.Contains("_Player_", StringComparison.OrdinalIgnoreCase) &&
                !file.Name.Contains("CrashHandler", StringComparison.OrdinalIgnoreCase) &&
                file.CreationTimeUtc >= processStart.AddSeconds(-10) &&
                file.LastWriteTimeUtc >= processStart.AddSeconds(-5))
            .OrderByDescending(file => file.CreationTimeUtc)
            .ThenByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    public void Reset()
    {
        lastPid = 0;
        lastTicks = offset = 0;
        logFile = null;
        pending = "";
        state.Clear();
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Detection must pause on any read failure and leave the desktop unchanged.")]
    public GameObservation Poll(string directory)
    {
        GameObservation result = new() { Detail = "Waiting for Roblox." };
        Process[] players = Process.GetProcessesByName("RobloxPlayerBeta");
        try
        {
            if (players.Length != 1)
            {
                Reset();
                result.Detail = players.Length == 0 ? "Roblox is not running." : "Multiple Roblox processes detected. Profile paused.";
                return result;
            }

            Process player = players[0];
            DateTime started = player.StartTime.ToUniversalTime();
            if (player.Id != lastPid || started.Ticks != lastTicks)
            {
                Reset();
                lastPid = player.Id;
                lastTicks = started.Ticks;
            }

            if (!Directory.Exists(directory))
            {
                Reset();
                result.Detail = "The Roblox logs folder was not found.";
                return result;
            }

            FileInfo? selectedLog = SelectLog(directory, started);
            if (selectedLog == null)
            {
                state.Clear();
                result.Detail = "Waiting for the current Roblox process log.";
                return result;
            }

            string selected = selectedLog.FullName;
            if (!string.Equals(logFile, selected, StringComparison.OrdinalIgnoreCase))
            {
                logFile = selected;
                offset = 0;
                pending = "";
                state.Clear();
            }

            bool caughtUp;
            using (FileStream stream = new(logFile!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < offset)
                {
                    offset = 0;
                    pending = "";
                    state.Clear();
                }

                if (offset == 0 && stream.Length > TailWindowBytes)
                {
                    offset = stream.Length - TailWindowBytes;
                    stream.Position = offset;
                    int next;
                    do
                    {
                        next = stream.ReadByte();
                    }
                    while (next >= 0 && next != '\n');
                    offset = stream.Position;
                }

                stream.Position = offset;
                byte[] chunk = new byte[(int)Math.Min(ChunkBytes, Math.Max(0, stream.Length - offset))];
                int read = stream.Read(chunk, 0, chunk.Length);
                offset += read;
                caughtUp = offset >= stream.Length;
                string text = pending + Encoding.UTF8.GetString(chunk, 0, read);
                // Explicit overload also supports journals migrated from the Framework version.
                string[] lines = text.Split(['\n'], StringSplitOptions.None);
                pending = lines[^1];
                if (pending.Length > PendingLimit)
                    pending = "";
                for (int i = 0; i < lines.Length - 1; i++)
                    state.Accept(lines[i], started);
            }

            result.Pid = player.Id;
            result.ProcessTicks = started.Ticks;
            result.UniverseId = caughtUp ? state.UniverseId : null;
            result.Generation = state.Generation;
            result.Focused = Native.ForegroundPid() == player.Id;
            result.Detail = !caughtUp
                ? "Reading the current game state..."
                : result.UniverseId.HasValue ? "Universe ID: " + result.UniverseId.Value : "Waiting for a confirmed game connection in the log.";
            return result;
        }
        catch (Exception ex)
        {
            Reset();
            result.Detail = "Detection paused: " + ex.Message;
            return result;
        }
        finally
        {
            foreach (Process player in players)
                player.Dispose();
        }
    }
}
