using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace Stretcher;

internal sealed class DisplayBrowser
{
    private readonly WindowsDisplayCatalog catalog = new();
    private readonly Action<bool> setUpdating;
    private readonly Action refreshControls;
    private readonly Dictionary<int, List<DisplayMode>> testedModes = [];
    private List<DisplayMode> driverModes = [];
    private DisplayMode? currentMode;

    public DisplayBrowser(Action<bool> setUpdating, Action refreshControls)
    {
        this.setUpdating = setUpdating;
        this.refreshControls = refreshControls;
        Monitors = Controls.Picker("Monitor");
        BaseResolution = Controls.Picker("Baseline resolution");
        BaseHz = Controls.Picker("Baseline refresh rate");
        GameResolution = Controls.Picker("Game resolution");
        Actual = Controls.Note("Reading Windows display modes…");
        ModeHint = Controls.Note("");
    }

    public ComboBox Monitors { get; }
    public ComboBox BaseResolution { get; }
    public ComboBox BaseHz { get; }
    public ComboBox GameResolution { get; }
    public TextBlock Actual { get; }
    public TextBlock ModeHint { get; }
    public bool ModesDirty { get; set; }
    public DisplayMode? CurrentMode => currentMode;

    public void Reload(bool refreshMonitors, bool preserveChoices, AppSettings settings, bool autoBase, bool sessionActive)
    {
        if (sessionActive)
            throw new InvalidOperationException("Restore the active session before refreshing display modes.");
        DisplayMode? oldBase = preserveChoices ? Controls.Value(BaseResolution) as DisplayMode : null;
        DisplayMode? oldGame = preserveChoices ? Controls.Value(GameResolution) as DisplayMode : null;
        int selectedHz = preserveChoices && Controls.Value(BaseHz) is int selected ? selected : settings.BaseRefresh;
        setUpdating(true);
        try
        {
            if (refreshMonitors)
            {
                string device = (Controls.Value(Monitors) as MonitorInfo)?.Device ?? settings.MonitorDevice;
                List<MonitorInfo> displays = catalog.Monitors();
                // Pick the primary display only on first use. A disconnected saved display
                // must not silently redirect a profile to a different monitor.
                MonitorInfo? desired = displays.FirstOrDefault(monitor => monitor.Device == device);
                if (string.IsNullOrEmpty(device))
                    desired = displays.FirstOrDefault(monitor => monitor.Primary) ?? displays.FirstOrDefault();
                Controls.Fill(Monitors, displays, monitor => monitor.ToString(), monitor => monitor == desired);
            }

            MonitorInfo monitor = SelectedMonitor();
            currentMode = catalog.Current(monitor.Device);
            driverModes = catalog.Modes(monitor.Device);
            testedModes.Clear();
            List<int> rates = ModeCatalog.RefreshRates(driverModes, currentMode);
            int hz = autoBase || selectedHz == 0 ? currentMode.Refresh : selectedHz;
            if (autoBase && !rates.Contains(hz))
                rates.Add(hz);
            Controls.Fill(BaseHz, rates, rate => rate + " Hz", rate => rate == hz);
        }
        finally
        {
            setUpdating(false);
        }

        Populate(oldBase, oldGame, settings, autoBase);
        ModesDirty = false;
    }

    public void Populate(DisplayMode? preferredBase, DisplayMode? preferredGame, AppSettings settings, bool autoBase)
    {
        if (currentMode == null)
            return;
        int hz = Controls.Value(BaseHz) is int value ? value : 0;
        if (!testedModes.TryGetValue(hz, out List<DisplayMode>? compatible))
        {
            string device = SelectedMonitor().Device;
            compatible = hz == 0 ? [] : ModeCatalog.TestedAtRefresh(driverModes, currentMode, hz, mode => catalog.Test(device, mode));
            testedModes[hz] = compatible;
        }

        GameProfile game = settings.SelectedGame();
        int baseWidth = autoBase ? currentMode.Width : preferredBase?.Width ?? settings.BaseWidth;
        int baseHeight = autoBase ? currentMode.Height : preferredBase?.Height ?? settings.BaseHeight;
        int gameWidth = preferredGame?.Width ?? game.GameWidth;
        int gameHeight = preferredGame?.Height ?? game.GameHeight;
        setUpdating(true);
        try
        {
            Controls.Fill(BaseResolution, compatible, ModeCatalog.ResolutionText, mode => mode.Width == baseWidth && mode.Height == baseHeight);
            Controls.Fill(GameResolution, compatible, ModeCatalog.ResolutionText, mode => mode.Width == gameWidth && mode.Height == gameHeight);
        }
        finally
        {
            setUpdating(false);
        }

        Actual.Text = "Windows: " + currentMode;
        ModeHint.Text = compatible.Count == 0
            ? "No validated driver modes match the baseline refresh rate. Refresh the list or choose a supported baseline."
            : compatible.Count + " validated resolutions at " + hz + " Hz. In-game refresh rate stays the same.";
        if (GameResolution.SelectedItem == null && compatible.Count > 0)
            ModeHint.Text += " Saved resolution " + game.GameWidth + " × " + game.GameHeight + " is unavailable. Choose a supported option.";
        refreshControls();
    }

    public MonitorInfo SelectedMonitor()
    {
        if (Controls.Value(Monitors) is not MonitorInfo selected)
            throw new InvalidOperationException("No display is selected.");
        MonitorInfo? live = catalog.Monitors().FirstOrDefault(monitor => monitor.Device == selected.Device && monitor.Identity == selected.Identity);
        return live ?? throw new InvalidOperationException("The selected display changed. Refresh the display list.");
    }

    public int? SelectedRefresh()
    {
        return Controls.Value(BaseHz) is int hz ? hz : null;
    }

    public DisplayMode ReadCurrent(string device)
    {
        return catalog.Current(device);
    }

    public bool Test(string device, DisplayMode mode)
    {
        return catalog.Test(device, mode);
    }
}
