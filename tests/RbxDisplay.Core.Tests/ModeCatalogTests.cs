using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace RbxDisplay;

public sealed class ModeCatalogTests
{
    private static List<DisplayMode> Catalog()
    {
        DisplayMode oddBits = new DisplayMode { Width = 1920, Height = 1440, Refresh = 240, Bits = 16 };
        DisplayMode oddOrientation = new DisplayMode { Width = 1920, Height = 1440, Refresh = 240, Bits = 32, Orientation = 1 };
        DisplayMode oddFlags = new DisplayMode { Width = 1920, Height = 1440, Refresh = 240, Bits = 32, Flags = 2 };
        return new List<DisplayMode>
        {
            Samples.Mode(1920, 1440, 180),
            Samples.Mode(3440, 1440, 180),
            Samples.Mode(1920, 1440, 180),
            Samples.Mode(2560, 1440, 180),
            Samples.Mode(1280, 960, 144),
            Samples.Mode(1920, 1080, 60),
            Samples.Mode(1600, 1200, 180),
            Samples.Mode(320, 200, 180),
            oddBits,
            oddOrientation,
            oddFlags
        };
    }

    [Fact(DisplayName = "refresh list matches color depth, orientation and scan flags")]
    public void RefreshListMatchesCurrentModeFamily()
    {
        List<int> rates = ModeCatalog.RefreshRates(Catalog(), Samples.Mode(3440, 1440, 180));
        Assert.Equal(180, rates[0]);
        Assert.Equal(144, rates[1]);
        Assert.Equal(60, rates[2]);
        Assert.Equal(3, rates.Count);
    }

    [Fact(DisplayName = "resolution list removes other Hz, duplicates and invalid small modes")]
    public void ResolutionListDropsDuplicatesAndOtherRates()
    {
        List<DisplayMode> filtered = ModeCatalog.AtRefresh(Catalog(), Samples.Mode(3440, 1440, 180), 180);
        Assert.Equal(4, filtered.Count);
        Assert.All(filtered, mode => Assert.Equal(180, mode.Refresh));
    }

    [Fact(DisplayName = "largest supported modes are shown first")]
    public void LargestModesComeFirst()
    {
        List<DisplayMode> filtered = ModeCatalog.AtRefresh(Catalog(), Samples.Mode(3440, 1440, 180), 180);
        Assert.Equal(3440, filtered[0].Width);
        Assert.Equal(2560, filtered[1].Width);
    }

    [Fact(DisplayName = "driver-rejected modes never enter dropdown choices")]
    public void DriverRejectionRemovesModes()
    {
        int tests = 0;
        List<DisplayMode> filtered = ModeCatalog.TestedAtRefresh(Catalog(), Samples.Mode(3440, 1440, 180), 180, mode =>
        {
            tests++;
            return mode.Width != 1600;
        });
        Assert.Equal(4, tests);
        Assert.Equal(3, filtered.Count);
        Assert.All(filtered, mode => Assert.NotEqual(1600, mode.Width));
    }

    [Fact(DisplayName = "unavailable baseline Hz does not fall back to lower Hz")]
    public void MissingRefreshDoesNotFallBack()
    {
        Assert.Empty(ModeCatalog.AtRefresh(Catalog(), Samples.Mode(3440, 1440, 180), 200));
    }

    [Fact(DisplayName = "dropdown text explains stretched aspect ratio")]
    public void ResolutionTextIncludesAspectRatio()
    {
        Assert.EndsWith("4:3", ModeCatalog.ResolutionText(Samples.Mode(1920, 1440, 180)));
        Assert.EndsWith("16:9", ModeCatalog.ResolutionText(Samples.Mode(2560, 1440, 180)));
    }
}
