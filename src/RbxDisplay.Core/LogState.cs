using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RbxDisplay;

internal sealed partial class LogState
{
    [GeneratedRegex(@"\[FLog::Output\]\s+(?:!\s+)?Joining game '[^']+' place (\d+)\b")]
    private static partial Regex JoinPattern();

    [GeneratedRegex(@"\bplaceid:(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoadPlacePattern();

    [GeneratedRegex(@"\buniverseid:(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoadUniversePattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T[^,]+")]
    private static partial Regex StampPattern();

    public long? UniverseId { get; private set; }
    public long Generation { get; private set; }

    private long? pendingPlaceId;
    private long? pendingUniverseId;
    private long? stagedPlace;
    private long? stagedUniverse;
    private bool connectionSeen;
    private bool transitioning;

    public void Clear()
    {
        UniverseId = pendingPlaceId = pendingUniverseId = stagedPlace = stagedUniverse = null;
        connectionSeen = transitioning = false;
        Generation++;
    }

    public void Accept(string line, DateTime processStart)
    {
        Match timestamp = StampPattern().Match(line);
        if (!timestamp.Success || !DateTime.TryParse(timestamp.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime time) || time < processStart.AddSeconds(-5))
            return;
        if (line.Contains("[FLog::SingleSurfaceApp]", StringComparison.Ordinal) &&
            (line.Contains("leaveGame", StringComparison.Ordinal) || line.Contains("leaveUGCGame", StringComparison.Ordinal) || line.Contains("returnToLuaApp", StringComparison.Ordinal)))
        {
            Clear();
            return;
        }

        if (line.Contains("[FLog::Network]", StringComparison.Ordinal) &&
            (line.Contains("Client:Disconnect", StringComparison.Ordinal) || line.Contains("Time to disconnect replication data:", StringComparison.Ordinal)))
        {
            if (!pendingPlaceId.HasValue && !transitioning)
                Clear();
            return;
        }

        if (IsTransition(line))
        {
            if (UniverseId.HasValue)
                transitioning = true;
            else
                Clear();
            return;
        }

        Match join = JoinPattern().Match(line);
        if (join.Success && long.TryParse(join.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long place))
        {
            pendingPlaceId = place;
            pendingUniverseId = null;
            connectionSeen = false;
            if (stagedPlace == place && stagedUniverse.HasValue)
                ApplyLoad(place, stagedUniverse.Value);
            return;
        }

        if (line.Contains("[FLog::GameJoinLoadTime]", StringComparison.Ordinal) && line.Contains("Report game_join_loadtime:", StringComparison.Ordinal))
        {
            Match loadedPlace = LoadPlacePattern().Match(line);
            Match loadedUniverse = LoadUniversePattern().Match(line);
            if (loadedPlace.Success && loadedUniverse.Success &&
                long.TryParse(loadedPlace.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long reportedPlace) &&
                long.TryParse(loadedUniverse.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long reportedUniverse))
            {
                stagedPlace = reportedPlace;
                stagedUniverse = reportedUniverse;
                ApplyLoad(reportedPlace, reportedUniverse);
            }

            return;
        }

        if (line.Contains("[FLog::Network]", StringComparison.Ordinal) && line.Contains("Replicator created:", StringComparison.Ordinal) && pendingPlaceId.HasValue)
        {
            connectionSeen = true;
            TryConfirm();
        }
    }

    private static bool IsTransition(string line)
    {
        return (line.Contains("[FLog::GameJoinUtil]", StringComparison.Ordinal) &&
                (line.Contains("joinGamePost", StringComparison.Ordinal) ||
                 line.Contains("initiateGameJoin", StringComparison.Ordinal) ||
                 line.Contains("initiateTeleportToPlace", StringComparison.Ordinal) ||
                 line.Contains("initiateTeleportToReservedServer", StringComparison.Ordinal))) ||
            (line.Contains("[FLog::SingleSurfaceApp]", StringComparison.Ordinal) && line.Contains("destroyDataModel", StringComparison.Ordinal));
    }

    private void ApplyLoad(long place, long universe)
    {
        if (pendingPlaceId != place)
            return;
        if (UniverseId == universe)
        {
            pendingPlaceId = pendingUniverseId = null;
            connectionSeen = false;
            transitioning = false;
            return;
        }

        if (UniverseId.HasValue)
        {
            UniverseId = null;
            transitioning = false;
            Generation++;
        }

        pendingUniverseId = universe;
        TryConfirm();
    }

    private void TryConfirm()
    {
        if (!pendingPlaceId.HasValue || !pendingUniverseId.HasValue || !connectionSeen)
            return;
        UniverseId = pendingUniverseId;
        pendingPlaceId = pendingUniverseId = null;
        connectionSeen = false;
        transitioning = false;
        Generation++;
    }
}
