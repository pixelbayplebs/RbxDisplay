using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Stretcher;

public sealed class Settings
{
    public string PlaceIds { get; set; } = "114234929420007";
    public string UniverseIds { get; set; } = "";
    public string MonitorDevice { get; set; } = "";
    public bool AutoBase { get; set; } = true;
    public int BaseWidth { get; set; } = 3440;
    public int BaseHeight { get; set; } = 1440;
    public int BaseRefresh { get; set; }
    public int GameWidth { get; set; } = 1920;
    public int GameHeight { get; set; } = 1440;
    public int GameRefresh { get; set; }
    public bool SaturationEnabled { get; set; } = true;
    public double Saturation { get; set; } = 1.35;
    public bool ResolutionOnFocusOnly { get; set; }
    public string LogsFolder { get; set; } = "";
    public int PollMilliseconds { get; set; } = 750;

    public int BaselineRefresh(DisplayMode current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return AutoBase || BaseRefresh == 0 ? current.Refresh : BaseRefresh;
    }

    public DisplayMode ProfileMode(DisplayMode current)
    {
        // GameRefresh is retained for old config files. The profile always uses baseline Hz.
        return Rules.Desired(current, GameWidth, GameHeight, BaselineRefresh(current));
    }

    public HashSet<long> Targets()
    {
        return ParseIds(UniverseIds, "Universe IDs must be positive numbers. Separate additional IDs with commas.", false);
    }

    public HashSet<long> Places()
    {
        return ParseIds(PlaceIds, "Place IDs must be positive numbers. Separate additional IDs with commas.", true);
    }

    public void Validate()
    {
        Places();
        Targets();
        if (GameWidth < 640 || GameWidth > 16384 || GameHeight < 480 || GameHeight > 16384 ||
            BaseWidth < 640 || BaseWidth > 16384 || BaseHeight < 480 || BaseHeight > 16384)
            throw new InvalidOperationException("Invalid resolution.");
        if (GameRefresh < 0 || GameRefresh > 1000 || BaseRefresh < 0 || BaseRefresh > 1000)
            throw new InvalidOperationException("A refresh rate of 0 means the current Windows refresh rate; other values must be positive.");
        if (double.IsNaN(Saturation) || double.IsInfinity(Saturation) || Saturation < 0 || Saturation > 2)
            throw new InvalidOperationException("Saturation must be between 0% and 200%.");
        PollMilliseconds = Math.Max(500, Math.Min(3000, PollMilliseconds));
    }

    public string LogPath()
    {
        return string.IsNullOrWhiteSpace(LogsFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs")
            : LogsFolder;
    }

    private static HashSet<long> ParseIds(string? raw, string invalidMessage, bool required)
    {
        string[] items = (raw ?? "").Split([',', ';', ' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        HashSet<long> result = [];
        foreach (string item in items)
        {
            if (!long.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out long value) || value <= 0)
                throw new InvalidOperationException(invalidMessage);
            result.Add(value);
        }

        if (required && result.Count == 0)
            throw new InvalidOperationException("Enter at least one Place ID.");
        return result;
    }
}
