using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Stretcher;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 2;
    public string SelectedGameId { get; set; } = "bloxstrike";
    public List<GameProfile> Games { get; set; } = [GameProfile.BloxStrike()];
    public string MonitorDevice { get; set; } = "";
    public bool AutoBase { get; set; } = true;
    public int BaseWidth { get; set; } = 3440;
    public int BaseHeight { get; set; } = 1440;
    public int BaseRefresh { get; set; }
    public string LogsFolder { get; set; } = "";
    public int PollMilliseconds { get; set; } = 750;

    public GameProfile SelectedGame()
    {
        return Games.FirstOrDefault(game => game.Id == SelectedGameId)
            ?? throw new InvalidDataException("The selected game is missing from the saved list.");
    }

    public void Validate()
    {
        if (SchemaVersion != 2)
            throw new InvalidDataException("This settings version is not supported. The file has been left unchanged.");
        if (Games is not { Count: > 0 })
            throw new InvalidDataException("The saved game list is empty.");
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameProfile game in Games)
        {
            if (game is null)
                throw new InvalidDataException("The saved game list contains an invalid entry.");
            game.Validate();
            if (!ids.Add(game.Id) || !names.Add(game.Name))
                throw new InvalidDataException("Saved games must have unique names and identifiers.");
        }

        Runtime(SelectedGame()).Validate();
        PollMilliseconds = Math.Max(500, Math.Min(3000, PollMilliseconds));
    }

    public Settings Runtime(GameProfile game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new Settings
        {
            PlaceIds = game.PlaceIds,
            UniverseIds = game.UniverseIds,
            MonitorDevice = MonitorDevice,
            AutoBase = AutoBase,
            BaseWidth = BaseWidth,
            BaseHeight = BaseHeight,
            BaseRefresh = BaseRefresh,
            GameWidth = game.GameWidth,
            GameHeight = game.GameHeight,
            GameRefresh = 0,
            SaturationEnabled = game.SaturationEnabled,
            Saturation = game.Saturation,
            ResolutionOnFocusOnly = game.ResolutionOnFocusOnly,
            LogsFolder = LogsFolder,
            PollMilliseconds = PollMilliseconds
        };
    }

    public AppSettings Copy()
    {
        return new AppSettings
        {
            SchemaVersion = SchemaVersion,
            SelectedGameId = SelectedGameId,
            Games = Games.Select(game => game.Copy()).ToList(),
            MonitorDevice = MonitorDevice,
            AutoBase = AutoBase,
            BaseWidth = BaseWidth,
            BaseHeight = BaseHeight,
            BaseRefresh = BaseRefresh,
            LogsFolder = LogsFolder,
            PollMilliseconds = PollMilliseconds
        };
    }
}
