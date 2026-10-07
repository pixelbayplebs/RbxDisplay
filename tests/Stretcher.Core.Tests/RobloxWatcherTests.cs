using System;
using System.IO;
using Xunit;

namespace Stretcher;

public sealed class RobloxWatcherTests
{
    [Fact(DisplayName = "crash handler log is not selected over the player log")]
    public void CrashHandlerIsNotSelectedOverThePlayerLog()
    {
        using TempDirectory directory = new("logs");
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        string player = Path.Combine(directory.Path, "0.741.0.7411058_20261006T230024Z_Player_2EFEE_last.log");
        string crash = Path.Combine(directory.Path, "0.741.0.7411058_20261006T230024Z_Player_2AC3B_CrashHandler_last.log");
        File.WriteAllText(player, "player");
        File.WriteAllText(crash, "crash");
        File.SetCreationTimeUtc(player, start);
        File.SetLastWriteTimeUtc(player, start);
        File.SetCreationTimeUtc(crash, start.AddMilliseconds(200));
        File.SetLastWriteTimeUtc(crash, start.AddMilliseconds(200));

        FileInfo? selected = RobloxWatcher.SelectLog(directory.Path, start);

        Assert.NotNull(selected);
        Assert.Equal(player, selected.FullName, ignoreCase: true);
    }
}
