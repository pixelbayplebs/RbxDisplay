using System.IO;
using Xunit;

namespace RbxDisplay;

public sealed class JournalTests
{
    [Fact(DisplayName = "64-bit process timestamp survives JSON exactly")]
    public void ProcessTimestampRoundTrips()
    {
        using (TempDirectory temp = new TempDirectory("RbxDisplay-tests-"))
        {
            string file = Path.Combine(temp.Path, "session.json");
            SessionData session = Samples.Example();
            session.ParentTicks = 639270123456789012L;
            Store.Save(file, session);
            SessionData loaded = Store.Read<SessionData>(file);
            Assert.Equal(session.ParentTicks, loaded.ParentTicks);
        }
    }

    [Fact(DisplayName = "recovery journal round trip")]
    public void JournalRoundTripsModeAndColor()
    {
        using (TempDirectory temp = new TempDirectory("RbxDisplay-tests-"))
        {
            string file = Path.Combine(temp.Path, "session.json");
            SessionData session = Samples.Example();
            Store.Save(file, session);
            SessionData loaded = Store.Read<SessionData>(file);
            Assert.True(Rules.SameMode(loaded.GameMode, session.GameMode));
            Assert.True(Rules.SameColor(loaded.GameColor, session.GameColor));
        }
    }

    [Fact(DisplayName = "atomic replace leaves complete journal only")]
    public void AtomicReplaceLeavesOneFile()
    {
        using (TempDirectory temp = new TempDirectory("RbxDisplay-tests-"))
        {
            string file = Path.Combine(temp.Path, "session.json");
            SessionData session = Samples.Example();
            session.ModeArmed = false;
            Store.Save(file, session);
            Assert.False(Store.Read<SessionData>(file).ModeArmed);
            Assert.Single(Directory.GetFiles(temp.Path));
        }
    }

    [Fact(DisplayName = "literal session JSON keeps ParentTicks and place fields")]
    public void LiteralSessionJsonDeserializes()
    {
        using (TempDirectory temp = new TempDirectory("RbxDisplay-tests-"))
        {
            string file = Path.Combine(temp.Path, "session.json");
            File.WriteAllText(file, """
                {
                  "Token": "0123456789abcdef0123456789abcdef",
                  "MonitorDevice": "display-A",
                  "MonitorIdentity": "monitor-A",
                  "ParentPid": 42,
                  "ParentTicks": 639270123456789012,
                  "OriginalMode": { "Width": 3440, "Height": 1440, "Refresh": 180, "Bits": 32, "Orientation": 0, "Flags": 0 },
                  "RestoreMode": { "Width": 3440, "Height": 1440, "Refresh": 180, "Bits": 32, "Orientation": 0, "Flags": 0 },
                  "GameMode": { "Width": 1920, "Height": 1440, "Refresh": 180, "Bits": 32, "Orientation": 0, "Flags": 0 },
                  "LastOwnedMode": { "Width": 3440, "Height": 1440, "Refresh": 180, "Bits": 32, "Orientation": 0, "Flags": 0 },
                  "OriginalColor": [1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1],
                  "GameColor": [1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1],
                  "ModeArmed": true,
                  "ColorArmed": false
                }
                """);
            SessionData loaded = Store.Read<SessionData>(file);
            Assert.NotNull(loaded.GameMode);
            Assert.NotNull(loaded.OriginalColor);
            DisplayMode game = loaded.GameMode!;
            float[] color = loaded.OriginalColor!;
            Assert.Equal(639270123456789012L, loaded.ParentTicks);
            Assert.Equal(42, loaded.ParentPid);
            Assert.Equal(1920, game.Width);
            Assert.Equal(1440, game.Height);
            Assert.Equal(180, game.Refresh);
            Assert.Equal(32, game.Bits);
            Assert.False(loaded.ColorArmed);
            Assert.True(loaded.ModeArmed);
            Assert.Equal(1, color[0]);
            Assert.Equal(1, color[24]);
        }
    }
}
