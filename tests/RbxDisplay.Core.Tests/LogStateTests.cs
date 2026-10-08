using System;
using Xunit;

namespace RbxDisplay;

public sealed class LogStateTests
{
    private const long BloxStrikePlace = GameProfile.BloxStrikePlaceId;
    private const long BloxStrikeUniverse = GameProfile.BloxStrikeUniverseId;

    private static DateTime Start()
    {
        return DateTime.UtcNow.AddMinutes(-2);
    }

    private static Settings BloxStrikeTargets()
    {
        return new Settings { UniverseIds = BloxStrikeUniverse.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    }

    [Fact(DisplayName = "join request alone never activates")]
    public void JoinRequestDoesNotActivate()
    {
        DateTime start = Start();
        LogState log = new LogState();
        log.Accept(Samples.Line(start, "[FLog::GameJoinUtil] joinGamePostStandard BODY: {\"placeId\":" + BloxStrikePlace + ",\"universeId\":" + BloxStrikeUniverse + "}"), start);
        Assert.False(log.UniverseId.HasValue);
    }

    [Fact(DisplayName = "joining alone awaits connection confirmation")]
    public void JoiningWaitsForConnection()
    {
        DateTime start = Start();
        LogState log = new LogState();
        log.Accept(Samples.Line(start, "[FLog::Output] ! Joining game 'guid' place " + BloxStrikePlace + " at 127.0.0.1"), start);
        Assert.False(log.UniverseId.HasValue);
    }

    [Fact(DisplayName = "load time alone awaits the replicator")]
    public void LoadTimeWaitsForTheReplicator()
    {
        DateTime start = Start();
        LogState log = new LogState();
        log.Accept(Samples.Line(start, "[FLog::Output] ! Joining game 'guid' place " + BloxStrikePlace + " at 127.0.0.1"), start);
        log.Accept(Samples.Line(start, "[FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:" + BloxStrikePlace + ", universeid:" + BloxStrikeUniverse + ","), start);
        Assert.False(log.UniverseId.HasValue);
    }

    [Fact(DisplayName = "replicator before the universe id still confirms")]
    public void ReplicatorBeforeUniverseStillConfirms()
    {
        DateTime start = Start();
        LogState log = new LogState();
        log.Accept(Samples.Line(start, "[FLog::Output] ! Joining game 'guid' place " + BloxStrikePlace + " at 127.0.0.1"), start);
        log.Accept(Samples.Line(start, "[FLog::Network] Replicator created: serverId: 127.0.0.1|12345"), start);
        Assert.False(log.UniverseId.HasValue);
        log.Accept(Samples.Line(start, "[FLog::GameJoinLoadTime] Report game_join_loadtime: universeid:" + BloxStrikeUniverse + ", placeid:" + BloxStrikePlace + ","), start);
        Assert.Equal(BloxStrikeUniverse, log.UniverseId);
    }

    [Fact(DisplayName = "current bang-prefixed join + connection recognized")]
    public void BangPrefixedJoinAndConnectionAreRecognized()
    {
        DateTime start = Start();
        LogState log = new LogState();
        log.Accept(Samples.Line(start, "[FLog::Output] ! Joining game 'guid' place " + BloxStrikePlace + " at 127.0.0.1"), start);
        log.Accept(Samples.Line(start, "[FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:" + BloxStrikePlace + ", universeid:" + BloxStrikeUniverse + ","), start);
        log.Accept(Samples.Line(start, "[FLog::Network] Replicator created: serverId: 127.0.0.1|12345"), start);
        Assert.Equal(BloxStrikeUniverse, log.UniverseId);
    }

    [Fact(DisplayName = "same-universe teleport keeps the confirmed game")]
    public void SameUniverseTeleportKeepsTheConfirmedGame()
    {
        DateTime start = Start();
        LogState log = new LogState();
        Samples.JoinAndConnect(log, start, BloxStrikePlace, BloxStrikeUniverse, true);
        long generation = log.Generation;
        log.Accept(Samples.Line(start, "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToPlace"), start);
        log.Accept(Samples.Line(start, "[FLog::SingleSurfaceApp] destroyDataModel"), start);
        log.Accept(Samples.Line(start, "[FLog::Network] Time to disconnect replication data: 0.485100"), start);
        log.Accept(Samples.Line(start, "[FLog::Output] ! Joining game 'guid' place 999 at 127.0.0.1"), start);
        log.Accept(Samples.Line(start, "[FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:999, universeid:" + BloxStrikeUniverse + ","), start);
        log.Accept(Samples.Line(start, "[FLog::Network] Replicator created: serverId: 127.0.0.1|12345"), start);
        Assert.Equal(BloxStrikeUniverse, log.UniverseId);
        Assert.Equal(generation, log.Generation);
    }

    [Fact(DisplayName = "different universe clears immediately and confirms after the replicator")]
    public void DifferentUniverseClearsUntilTheReplicator()
    {
        DateTime start = Start();
        LogState log = new LogState();
        Samples.JoinAndConnect(log, start, BloxStrikePlace, BloxStrikeUniverse, true);
        long generation = log.Generation;
        log.Accept(Samples.Line(start, "[FLog::GameJoinUtil] initiateGameJoin"), start);
        log.Accept(Samples.Line(start, "[FLog::Output] ! Joining game 'guid' place 50 at 127.0.0.1"), start);
        log.Accept(Samples.Line(start, "[FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:50, universeid:123,"), start);
        Assert.False(log.UniverseId.HasValue);
        Assert.True(log.Generation > generation);
        long cleared = log.Generation;
        log.Accept(Samples.Line(start, "[FLog::Network] Replicator created: serverId: 127.0.0.1|12345"), start);
        Assert.Equal(123, log.UniverseId);
        Assert.True(log.Generation > cleared);
    }

    [Fact(DisplayName = "load time for another place leaves the confirmed universe")]
    public void UnrelatedLoadTimeLeavesTheConfirmedUniverse()
    {
        DateTime start = Start();
        LogState log = new LogState();
        Samples.JoinAndConnect(log, start, BloxStrikePlace, BloxStrikeUniverse, true);
        long generation = log.Generation;
        log.Accept(Samples.Line(start, "[FLog::GameJoinLoadTime] Report game_join_loadtime: universeid:5, placeid:9,"), start);
        Assert.Equal(BloxStrikeUniverse, log.UniverseId);
        Assert.Equal(generation, log.Generation);
    }

    [Fact(DisplayName = "other game replaces target")]
    public void AnotherGameReplacesTheTarget()
    {
        DateTime start = Start();
        LogState log = new LogState();
        Samples.JoinAndConnect(log, start, 50, 123, true);
        Assert.Equal(123, log.UniverseId);
    }

    [Fact(DisplayName = "legacy join accepted")]
    public void LegacyJoinWithoutBangIsAccepted()
    {
        DateTime start = Start();
        LogState log = new LogState();
        Samples.JoinAndConnect(log, start, BloxStrikePlace, BloxStrikeUniverse, false);
        Assert.Equal(BloxStrikeUniverse, log.UniverseId);
    }

    [Fact(DisplayName = "home navigation clears target")]
    public void HomeNavigationClearsTheTarget()
    {
        DateTime start = Start();
        LogState log = new LogState();
        Samples.JoinAndConnect(log, start, BloxStrikePlace, BloxStrikeUniverse, true);
        log.Accept(Samples.Line(start, "[FLog::SingleSurfaceApp] leaveUGCGameInternal"), start);
        Assert.False(log.UniverseId.HasValue);
    }

    [Fact(DisplayName = "disconnect during an in-flight join still confirms the new universe")]
    public void DisconnectDuringJoinStillConfirmsTheNewUniverse()
    {
        DateTime start = Start();
        LogState log = new LogState();
        log.Accept(Samples.Line(start, "[FLog::Output] ! Joining game 'guid' place " + BloxStrikePlace + " at 127.0.0.1"), start);
        log.Accept(Samples.Line(start, "[FLog::Network] Time to disconnect replication data: 0.485100"), start);
        log.Accept(Samples.Line(start, "[FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:" + BloxStrikePlace + ", universeid:" + BloxStrikeUniverse + ","), start);
        log.Accept(Samples.Line(start, "[FLog::Network] Replicator created: 000001FA840BB700"), start);
        Assert.Equal(BloxStrikeUniverse, log.UniverseId);
    }

    [Fact(DisplayName = "disconnect clears target")]
    public void DisconnectClearsTheTarget()
    {
        DateTime start = Start();
        LogState log = new LogState();
        Samples.JoinAndConnect(log, start, BloxStrikePlace, BloxStrikeUniverse, true);
        log.Accept(Samples.Line(start, "[FLog::Network] Time to disconnect replication data: 12.3"), start);
        Assert.False(log.UniverseId.HasValue);
    }

    [Fact(DisplayName = "reserved-server teleport keeps the same universe")]
    public void ReservedServerTeleportKeepsTheSameUniverse()
    {
        DateTime start = Start();
        LogState log = new LogState();
        Samples.JoinAndConnect(log, start, BloxStrikePlace, BloxStrikeUniverse, true);
        long generation = log.Generation;
        log.Accept(Samples.Line(start, "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToReservedServer"), start);
        log.Accept(Samples.Line(start, "[FLog::Network] Client:Disconnect"), start);
        Assert.Equal(BloxStrikeUniverse, log.UniverseId);
        Assert.Equal(generation, log.Generation);
    }

    [Fact(DisplayName = "stale timestamps ignored")]
    public void StaleTimestampsAreIgnored()
    {
        DateTime start = Start();
        LogState log = new LogState();
        log.Accept(Samples.Line(start.AddMinutes(-10), "[FLog::Output] ! Joining game 'guid' place " + BloxStrikePlace + " at 127.0.0.1"), start);
        log.Accept(Samples.Line(start.AddMinutes(-10), "[FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:" + BloxStrikePlace + ", universeid:" + BloxStrikeUniverse + ","), start);
        log.Accept(Samples.Line(start.AddMinutes(-10), "[FLog::Network] Replicator created: serverId: 127.0.0.1|12345"), start);
        Assert.False(log.UniverseId.HasValue);
    }

    [Fact(DisplayName = "arbitrary ID text ignored")]
    public void ArbitraryPlaceTextIsIgnored()
    {
        LogState log = new LogState();
        DateTime start = Start();
        log.Accept("placeId=" + BloxStrikePlace, start);
        log.Accept("universeid:" + BloxStrikeUniverse, start);
        Assert.False(log.UniverseId.HasValue);
    }

    [Fact(DisplayName = "target session recognized")]
    public void MatchingObservationIsATarget()
    {
        GameObservation observation = new GameObservation { Pid = 55, UniverseId = BloxStrikeUniverse };
        Assert.True(observation.IsTarget(BloxStrikeTargets().Targets()));
    }

    [Fact(DisplayName = "neighboring ID doesn't match")]
    public void NeighboringUniverseIsNotATarget()
    {
        GameObservation observation = new GameObservation { Pid = 55, UniverseId = BloxStrikeUniverse + 1 };
        Assert.False(observation.IsTarget(BloxStrikeTargets().Targets()));
    }

    [Fact(DisplayName = "process required")]
    public void ObservationWithoutProcessIsNotATarget()
    {
        GameObservation observation = new GameObservation { Pid = 0, UniverseId = BloxStrikeUniverse };
        Assert.False(observation.IsTarget(BloxStrikeTargets().Targets()));
    }
}
