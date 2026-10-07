using System;
using System.IO;

namespace Stretcher;

internal static class Samples
{
    public static DisplayMode Mode(int width, int height, int hz)
    {
        return new DisplayMode { Width = width, Height = height, Refresh = hz, Bits = 32 };
    }

    public static SessionData Example()
    {
        DisplayMode original = Mode(3440, 1440, 180);
        DisplayMode game = Mode(1920, 1440, 180);
        return new SessionData
        {
            Token = "0123456789abcdef0123456789abcdef",
            MonitorDevice = "display-A",
            MonitorIdentity = "monitor-A",
            OriginalMode = original,
            RestoreMode = original,
            GameMode = game,
            OriginalColor = Rules.Identity(),
            GameColor = Rules.Saturated(Rules.Identity(), 1.35),
            ModeArmed = true,
            ColorArmed = true
        };
    }

    public static string Line(DateTime stamp, string text)
    {
        return stamp.ToString("o") + ",1.0,abcd,6,Info " + text;
    }

    public static void JoinAndConnect(LogState state, DateTime start, long placeId, long universeId, bool bang)
    {
        state.Accept(Line(start, "[FLog::Output] " + (bang ? "! " : "") + "Joining game '00000000-0000-0000-0000-000000000000' place " + placeId + " at 127.0.0.1"), start);
        state.Accept(Line(start, "[FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:" + placeId + ", universeid:" + universeId + ","), start);
        state.Accept(Line(start, "[FLog::Network] Replicator created: serverId: 127.0.0.1|12345"), start);
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory(string prefix)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
