using System;
using System.Globalization;
using System.IO;

namespace RbxDisplay;

public sealed class GameProfile
{
    public const long BloxStrikePlaceId = 114234929420007L;
    public const long BloxStrikeUniverseId = 7633926880L;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string PlaceIds { get; set; } = "";
    public string UniverseIds { get; set; } = "";
    public string MonitorDevice { get; set; } = "";
    public bool AutoBase { get; set; } = true;
    public int BaseWidth { get; set; } = 3440;
    public int BaseHeight { get; set; } = 1440;
    public int BaseRefresh { get; set; }
    public int GameWidth { get; set; } = 1920;
    public int GameHeight { get; set; } = 1440;
    public bool SaturationEnabled { get; set; } = true;
    public double Saturation { get; set; } = 1.35;
    public bool ResolutionOnFocusOnly { get; set; }

    public static GameProfile BloxStrike()
    {
        return new GameProfile
        {
            Id = "bloxstrike",
            Name = "BloxStrike",
            PlaceIds = BloxStrikePlaceId.ToString(CultureInfo.InvariantCulture),
            UniverseIds = BloxStrikeUniverseId.ToString(CultureInfo.InvariantCulture)
        };
    }

    public override string ToString()
    {
        return Name;
    }

    public GameProfile Copy()
    {
        return new GameProfile
        {
            Id = Id,
            Name = Name,
            PlaceIds = PlaceIds,
            UniverseIds = UniverseIds,
            MonitorDevice = MonitorDevice,
            AutoBase = AutoBase,
            BaseWidth = BaseWidth,
            BaseHeight = BaseHeight,
            BaseRefresh = BaseRefresh,
            GameWidth = GameWidth,
            GameHeight = GameHeight,
            SaturationEnabled = SaturationEnabled,
            Saturation = Saturation,
            ResolutionOnFocusOnly = ResolutionOnFocusOnly
        };
    }

    public void AdoptSetup(GameProfile source)
    {
        ArgumentNullException.ThrowIfNull(source);
        MonitorDevice = source.MonitorDevice;
        AutoBase = source.AutoBase;
        BaseWidth = source.BaseWidth;
        BaseHeight = source.BaseHeight;
        BaseRefresh = source.BaseRefresh;
        GameWidth = source.GameWidth;
        GameHeight = source.GameHeight;
        SaturationEnabled = source.SaturationEnabled;
        Saturation = source.Saturation;
        ResolutionOnFocusOnly = source.ResolutionOnFocusOnly;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidDataException("A saved game is missing its identifier.");
        Name = (Name ?? "").Trim();
        if (Name.Length == 0 || Name.Length > 60)
            throw new InvalidOperationException("Enter a game name with 1 to 60 characters.");
        PlaceIds = PlaceInput.Normalize(PlaceIds);
        UniverseIds = UniverseIds ?? "";
        new Settings
        {
            PlaceIds = PlaceIds,
            UniverseIds = UniverseIds,
            MonitorDevice = MonitorDevice,
            AutoBase = AutoBase,
            BaseWidth = BaseWidth,
            BaseHeight = BaseHeight,
            BaseRefresh = BaseRefresh,
            GameWidth = GameWidth,
            GameHeight = GameHeight,
            SaturationEnabled = SaturationEnabled,
            Saturation = Saturation
        }.Validate();
    }

    public string LaunchUrl()
    {
        string first = PlaceInput.Normalize(PlaceIds).Split(',')[0];
        return "roblox://placeId=" + first;
    }
}
