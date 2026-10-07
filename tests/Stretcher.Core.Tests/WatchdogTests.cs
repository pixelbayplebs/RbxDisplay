using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Threading;
using Xunit;

namespace Stretcher;

public sealed class WatchdogTests
{
    [Fact(DisplayName = "watchdog identifies exact parent")]
    public void ParentAliveMatchesPidAndStartTime()
    {
        using (Process current = Process.GetCurrentProcess())
        {
            SessionData session = Samples.Example();
            session.ParentPid = current.Id;
            session.ParentTicks = current.StartTime.ToUniversalTime().Ticks;
            Assert.True(Watchdog.ParentAlive(session));
        }
    }

    [Fact(DisplayName = "watchdog rejects PID reuse / different start time")]
    public void ParentAliveRejectsADifferentStartTime()
    {
        using (Process current = Process.GetCurrentProcess())
        {
            SessionData session = Samples.Example();
            session.ParentPid = current.Id;
            session.ParentTicks = current.StartTime.ToUniversalTime().Ticks + 1;
            Assert.False(Watchdog.ParentAlive(session));
        }
    }

    [Fact(DisplayName = "a missed heartbeat read keeps a fresh lease")]
    public void MissedHeartbeatReadKeepsAFreshLease()
    {
        using (TempDirectory temp = new TempDirectory("Stretcher-tests-"))
        {
            string path = Path.Combine(temp.Path, "heartbeat");
            DateTime fresh = DateTime.UtcNow;
            Store.Atomic(path, fresh.Ticks.ToString(CultureInfo.InvariantCulture));
            Assert.True(Watchdog.TryReadHeartbeat(path, out DateTime beat));
            File.Delete(path);
            DateTime kept = Watchdog.NoteHeartbeat(beat, path);
            Assert.Equal(beat, kept);
            Assert.False(Watchdog.HeartbeatExpired(DateTime.UtcNow, kept));

            Store.Atomic(path, fresh.AddSeconds(-16).Ticks.ToString(CultureInfo.InvariantCulture));
            Assert.True(Watchdog.TryReadHeartbeat(path, out DateTime older));
            Assert.True(older < fresh);
            Assert.Equal(fresh, Watchdog.NoteHeartbeat(fresh, path));
            Store.Atomic(path, "0");
            Assert.False(Watchdog.TryReadHeartbeat(path, out _));
            Assert.Equal(fresh, Watchdog.NoteHeartbeat(fresh, path));
        }
    }

    [Fact(DisplayName = "a heartbeat older than 15 seconds is expired")]
    public void HeartbeatOlderThanFifteenSecondsIsExpired()
    {
        using (TempDirectory temp = new TempDirectory("Stretcher-tests-"))
        {
            string path = Path.Combine(temp.Path, "heartbeat");
            DateTime stale = DateTime.UtcNow.AddSeconds(-16);
            Store.Atomic(path, stale.Ticks.ToString(CultureInfo.InvariantCulture));
            Assert.True(Watchdog.TryReadHeartbeat(path, out DateTime beat));
            Assert.True(Watchdog.HeartbeatExpired(DateTime.UtcNow, beat));
            Assert.False(Watchdog.HeartbeatExpired(DateTime.UtcNow, DateTime.UtcNow.AddSeconds(-14)));
        }
    }

    [Fact(DisplayName = "replacing the heartbeat file does not expire a live lease")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The writer thread must report any failure back to the test.")]
    public void ReplacingTheHeartbeatDoesNotExpireLease()
    {
        using (TempDirectory temp = new TempDirectory("Stretcher-tests-"))
        {
            string path = Path.Combine(temp.Path, "heartbeat");
            Store.Atomic(path, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                Exception? writerError = null;
                Thread writer = new Thread(() =>
                {
                    try
                    {
                        while (!cancel.IsCancellationRequested)
                        {
                            try
                            {
                                Store.Atomic(path, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                            }
                            catch (IOException)
                            {
                            }
                            catch (UnauthorizedAccessException)
                            {
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        writerError = ex;
                    }
                });
                writer.IsBackground = true;
                writer.Start();
                DateTime lastBeat = DateTime.UtcNow.AddSeconds(-14);
                int oldRuleHits = 0;
                int reads = 0;
                DateTime deadline = DateTime.UtcNow.AddSeconds(4);
                while (DateTime.UtcNow < deadline)
                {
                    bool oldRule = OldRuleWouldExpire(path);
                    DateTime noted = Watchdog.NoteHeartbeat(lastBeat, path);
                    if (noted > lastBeat)
                    {
                        lastBeat = noted;
                        reads++;
                    }

                    if (oldRule)
                        oldRuleHits++;
                    Assert.False(Watchdog.HeartbeatExpired(DateTime.UtcNow, lastBeat));
                }

                cancel.Cancel();
                Assert.True(writer.Join(TimeSpan.FromSeconds(10)));
                Assert.Null(writerError);
                Assert.True(reads > 0);
                Assert.True(oldRuleHits > 0);
            }
        }
    }

    private static bool OldRuleWouldExpire(string path)
    {
        if (!File.Exists(path))
            return true;
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        return stamp.Year < 2000 || DateTime.UtcNow - stamp > TimeSpan.FromSeconds(15);
    }
}
