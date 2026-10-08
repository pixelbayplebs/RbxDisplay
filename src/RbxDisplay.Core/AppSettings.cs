using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RbxDisplay;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 3;
    public string SelectedGameId { get; set; } = "bloxstrike";
    public List<GameProfile> Games { get; set; } = [GameProfile.BloxStrike()];
    public string LogsFolder { get; set; } = "";
    public int PollMilliseconds { get; set; } = 750;
    public int EmergencyModifiers { get; set; } = 0x3;
    public int EmergencyKey { get; set; } = 0x7B;
    public int MonitorModifiers { get; set; }
    public int MonitorKey { get; set; }

    public GameProfile SelectedGame()
    {
        return Games.FirstOrDefault(game => game.Id == SelectedGameId)
            ?? throw new InvalidDataException("The selected game is missing from the saved list.");
    }

    public void Validate()
    {
        if (SchemaVersion != 3)
            throw new InvalidDataException("This settings version is not supported. The file has been left unchanged.");
        if (Games == null)
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

        if (Games.Count == 0)
        {
            if (!string.IsNullOrEmpty(SelectedGameId))
                throw new InvalidDataException("The selected game is missing from the saved list.");
        }
        else
        {
            Runtime(SelectedGame()).Validate();
        }
        PollMilliseconds = Math.Max(500, Math.Min(3000, PollMilliseconds));
        if (EmergencyKey != 0 && EmergencyKey == MonitorKey && EmergencyModifiers == MonitorModifiers)
            throw new InvalidOperationException("Emergency restore and monitoring cannot share one shortcut.");
    }

    public Settings Runtime(GameProfile game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new Settings
        {
            PlaceIds = game.PlaceIds,
            UniverseIds = game.UniverseIds,
            MonitorDevice = game.MonitorDevice,
            AutoBase = game.AutoBase,
            BaseWidth = game.BaseWidth,
            BaseHeight = game.BaseHeight,
            BaseRefresh = game.BaseRefresh,
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

    public GameProfile? Match(long? universeId)
    {
        if (universeId is not long id)
            return null;
        foreach (GameProfile game in Games)
        {
            if (Runtime(game).Targets().Contains(id))
                return game;
        }

        return null;
    }

    public bool UniversesOverlap()
    {
        HashSet<long> seen = [];
        foreach (GameProfile game in Games)
        {
            foreach (long id in Runtime(game).Targets())
            {
                if (!seen.Add(id))
                    return true;
            }
        }

        return false;
    }

    public AppSettings Copy()
    {
        return new AppSettings
        {
            SchemaVersion = SchemaVersion,
            SelectedGameId = SelectedGameId,
            Games = Games.Select(game => game.Copy()).ToList(),
            LogsFolder = LogsFolder,
            PollMilliseconds = PollMilliseconds,
            EmergencyModifiers = EmergencyModifiers,
            EmergencyKey = EmergencyKey,
            MonitorModifiers = MonitorModifiers,
            MonitorKey = MonitorKey
        };
    }
}
