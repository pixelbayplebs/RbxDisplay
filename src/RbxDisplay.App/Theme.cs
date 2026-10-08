using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace RbxDisplay;

internal static class Theme
{
    private static readonly string[] AccentColorKeys =
    [
        "SystemAccentColor",
        "SystemAccentColorLight1",
        "SystemAccentColorLight2",
        "SystemAccentColorLight3",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAccentColorDark3"
    ];

    private static readonly string[] AccentBrushKeys =
    [
        "AccentFillColorDefaultBrush",
        "AccentFillColorSecondaryBrush",
        "AccentFillColorTertiaryBrush",
        "AccentTextFillColorPrimaryBrush",
        "AccentTextFillColorSecondaryBrush",
        "AccentTextFillColorTertiaryBrush",
        "SystemControlHighlightAccentBrush"
    ];

    private static readonly string[] ForegroundBrushKeys =
    [
        "TextFillColorPrimaryBrush",
        "SystemControlForegroundBaseHighBrush",
        "ApplicationForegroundThemeBrush"
    ];

    private static readonly string[] OnAccentBrushKeys =
    [
        "TextOnAccentFillColorPrimaryBrush",
        "TextOnAccentFillColorSecondaryBrush",
        "TextOnAccentFillColorDisabledBrush"
    ];

    public static readonly Color Background = Color.FromArgb(255, 0x24, 0x24, 0x24);
    public static readonly Color Foreground = Color.FromArgb(255, 0xFF, 0xF7, 0xE7);
    public static readonly Color Accent = Color.FromArgb(255, 0xF4, 0x3F, 0x42);
    public static readonly Color Card = Color.FromArgb(255, 0x2C, 0x2C, 0x2C);
    public static readonly Color Stroke = Color.FromArgb(255, 0x3E, 0x3E, 0x3E);

    public static SolidColorBrush Brush(Color color)
    {
        return new SolidColorBrush(color);
    }

    public static ResourceDictionary CreateResources()
    {
        ResourceDictionary resources = new();
        resources.MergedDictionaries.Add(new XamlControlsResources());
        foreach (string key in AccentColorKeys)
            resources[key] = Accent;
        foreach (string key in AccentBrushKeys)
            resources[key] = Brush(Accent);
        foreach (string key in ForegroundBrushKeys)
            resources[key] = Brush(Foreground);
        resources["TextFillColorSecondaryBrush"] = Brush(Color.FromArgb(190, 0xFF, 0xF7, 0xE7));
        resources["ApplicationPageBackgroundThemeBrush"] = Brush(Background);
        foreach (string key in OnAccentBrushKeys)
            resources[key] = Brush(Foreground);
        return resources;
    }
}
