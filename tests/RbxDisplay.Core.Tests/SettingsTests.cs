using System;
using System.Collections.Generic;
using Xunit;

namespace RbxDisplay;

public sealed class SettingsTests
{
    [Fact(DisplayName = "default place ID is BloxStrike")]
    public void DefaultPlaceIsBloxStrike()
    {
        Settings settings = new Settings();
        settings.Validate();
        Assert.Equal(GameProfile.BloxStrikePlaceId, Assert.Single(settings.Places()));
        Assert.Empty(settings.Targets());
    }

    [Fact(DisplayName = "4:3 default")]
    public void DefaultResolutionIsFourByThree()
    {
        Settings settings = new Settings();
        Assert.Equal(settings.GameHeight * 4, settings.GameWidth * 3);
    }

    [Fact(DisplayName = "automatic baseline, stable resolution on blur")]
    public void DefaultKeepsResolutionWhenUnfocused()
    {
        Settings settings = new Settings();
        Assert.True(settings.AutoBase);
        Assert.False(settings.ResolutionOnFocusOnly);
    }

    [Fact(DisplayName = "universe IDs are the detection targets")]
    public void UniverseIdsAreTheDetectionTargets()
    {
        Settings settings = new Settings { UniverseIds = "7633926880, 123; 456" };
        Assert.Equal(3, settings.Targets().Count);
    }

    [Fact(DisplayName = "reject invalid IDs rather than partial matching")]
    public void InvalidPlaceIdIsRejected()
    {
        Settings settings = new Settings { PlaceIds = "114234929420007x" };
        Assert.Throws<InvalidOperationException>(() => settings.Validate());
    }

    [Fact(DisplayName = "reject invalid universe IDs rather than partial matching")]
    public void InvalidUniverseIdIsRejected()
    {
        Settings settings = new Settings { UniverseIds = "12x" };
        Assert.Throws<InvalidOperationException>(settings.Targets);
    }

    [Fact(DisplayName = "reject nonfinite color values")]
    public void NonFiniteSaturationIsRejected()
    {
        Settings settings = new Settings { Saturation = double.NaN };
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact(DisplayName = "DEVMODEW layout is 220 bytes")]
    public void DevModeLayoutIs220Bytes()
    {
        Assert.Equal(220, Native.ModeSize);
    }

    [Fact(DisplayName = "preserve Windows refresh rate")]
    public void ZeroRefreshKeepsCurrentRate()
    {
        DisplayMode desired = Rules.Desired(Samples.Mode(3440, 1440, 180), 1920, 1440, 0);
        Assert.Equal(180, desired.Refresh);
    }

    [Fact(DisplayName = "different refresh is a different mode")]
    public void RefreshRateIsPartOfModeIdentity()
    {
        Assert.False(Rules.SameMode(Samples.Mode(1920, 1440, 180), Samples.Mode(1920, 1440, 60)));
    }

    [Fact(DisplayName = "legacy game refresh override cannot lower automatic baseline Hz")]
    public void LegacyGameRefreshDoesNotLowerBaseline()
    {
        Settings settings = new Settings { GameRefresh = 60 };
        Assert.Equal(180, settings.ProfileMode(Samples.Mode(3440, 1440, 180)).Refresh);
    }

    [Fact(DisplayName = "manual baseline Hz also controls game Hz")]
    public void ManualBaselineControlsGameRefresh()
    {
        Settings settings = new Settings { AutoBase = false, BaseRefresh = 144 };
        Assert.Equal(144, settings.ProfileMode(Samples.Mode(3440, 1440, 180)).Refresh);
    }

    [Fact(DisplayName = "legacy zero manual Hz preserves current refresh")]
    public void ZeroManualBaselineKeepsCurrentRefresh()
    {
        Settings settings = new Settings { AutoBase = false, BaseRefresh = 0 };
        Assert.Equal(180, settings.ProfileMode(Samples.Mode(3440, 1440, 180)).Refresh);
    }
}
