using System;
using System.Collections.Generic;

namespace RbxDisplay;

internal sealed class GameObservation
{
    public int Pid { get; set; }
    public long ProcessTicks { get; set; }
    public long? UniverseId { get; set; }
    public long Generation { get; set; }
    public bool Focused { get; set; }
    public string Detail { get; set; } = "";

    public bool IsTarget(HashSet<long> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return Pid > 0 && UniverseId.HasValue && targets.Contains(UniverseId.Value);
    }
}
