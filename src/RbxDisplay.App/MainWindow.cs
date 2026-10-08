using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.AnimatedVisuals;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.System;
using Windows.UI;

namespace RbxDisplay;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The tray handler is disposed when the window closes.")]
internal sealed class MainWindow : Window, IGameHost, ISessionHost
{
    private AppSettings settings;
    private string? configError;
    private readonly MonitoringController monitor = new();
    private readonly GameEditor gameEditor;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly LivePage livePage = new();
    private readonly GamesPage gamesPage;
    private readonly SettingsPage settingsPage;
    private readonly ContentControl content = new();
    private readonly Grid root = new() { RequestedTheme = ElementTheme.Dark };
    private Grid? chromeBar;
    private readonly NativeWindow native;
    private bool initialized;
    private bool closing;
    private bool? captureEmergency;

    public MainWindow(AppSettings initial, string? startupError)
    {
        settings = initial;
        configError = startupError;
        gameEditor = new GameEditor(this);
        gamesPage = new GamesPage(this);
        settingsPage = new SettingsPage(this);
        Title = "RbxDisplay";
        root.Language = "en-US";
        if (DesktopAcrylicController.IsSupported())
            SystemBackdrop = new DesktopAcrylicBackdrop();
        else
            root.Background = Theme.Brush(Theme.Background);
        BuildShell();
        Content = root;
        ExtendsContentIntoTitleBar = true;
        if (chromeBar != null)
            SetTitleBar(chromeBar);
        root.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnShortcutKey), true);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "RbxDisplay.ico"));
        AppWindow.TitleBar.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonForegroundColor = Theme.Foreground;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Theme.Accent;
        AppWindow.TitleBar.ButtonHoverForegroundColor = Theme.Foreground;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Theme.Accent;
        AppWindow.TitleBar.ButtonPressedForegroundColor = Theme.Foreground;
        AppWindow.TitleBar.InactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 960;
            presenter.PreferredMinimumHeight = 680;
        }

        native = new NativeWindow(WinRT.Interop.WindowNative.GetWindowHandle(this),
            () => DispatcherQueue.TryEnqueue(ShowWindow),
            () => DispatcherQueue.TryEnqueue(EmergencyRestore),
            () => DispatcherQueue.TryEnqueue(() => monitor.ToggleMonitoring(this)),
            () => DispatcherQueue.TryEnqueue(Close),
            () => DispatcherQueue.TryEnqueue(() => { gamesPage.Display.ModesDirty = true; FitWindow(false); }));
        native.ApplyHotkeys(settings.EmergencyModifiers, settings.EmergencyKey, settings.MonitorModifiers, settings.MonitorKey);
        root.Loaded += (_, _) => Initialize();
        Closed += (_, _) => OnClosing();
        timer.Tick += (_, _) => monitor.Tick(this);
    }

    public bool IsClosing => closing;
    public bool DialogOpen => gameEditor.DialogOpen;
    public bool Initialized => initialized;
    public bool Ready => initialized;
    public AppSettings Settings => settings;
    public string? ConfigError { get => configError; set => configError = value; }
    public XamlRoot XamlRoot => root.XamlRoot ?? throw new InvalidOperationException("The window is not ready.");
    public bool ModesDirty { get => gamesPage.Display.ModesDirty; set => gamesPage.Display.ModesDirty = value; }

    public void Publish(LiveState state)
    {
        ApplyChrome(state);
    }

    public void Notify(string message)
    {
        livePage.ShowIssue(message, InfoBarSeverity.Warning);
    }

    public void SetTray(string tooltip)
    {
        native.UpdateTooltip(tooltip);
        native.SetMenuStatus(tooltip);
    }

    public MonitorInfo ResolveMonitor(GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return gamesPage.Display.Resolve(profile.MonitorDevice);
    }

    public void ReloadModes(bool refreshMonitors)
    {
        GameProfile? profile = gamesPage.EditorProfile();
        if (profile == null)
            return;
        DisplayMode? baseline = null;
        string? device = null;
        if (monitor.Session.Data is SessionData data && data.OriginalMode != null)
        {
            baseline = data.OriginalMode;
            device = data.MonitorDevice;
        }

        gamesPage.Display.Reload(refreshMonitors, true, profile, gamesPage.AutoBaseOn, baseline, device);
    }

    public void ReportSessionFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Store.Log(exception.ToString());
        try
        {
            monitor.RestoreDisplay(this, exception.Message);
        }
        catch (Exception second)
        {
            Store.Log(second.ToString());
            livePage.ShowIssue(exception.Message + "\n" + second.Message, InfoBarSeverity.Error);
        }
    }

    public AppSettings Capture(bool requireModes)
    {
        return gamesPage.Capture(requireModes);
    }

    public void ReplaceSettings(AppSettings next)
    {
        ArgumentNullException.ThrowIfNull(next);
        settings = next;
    }

    public void Commit(AppSettings next, string status, bool refreshMonitors)
    {
        ArgumentNullException.ThrowIfNull(next);
        settings = next;
        gamesPage.Bind(settings);
        gamesPage.ShowStatus(status, InfoBarSeverity.Success);
        settingsPage.ShowStatus(status, InfoBarSeverity.Success);
        try
        {
            ReloadModes(refreshMonitors);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }

        if (configError == null && !monitor.Monitoring)
            monitor.Arm(this);
    }

    public void UpdateControls()
    {
        gamesPage.UpdateChrome();
        settingsPage.SetTrayAvailable(native.TrayAvailable);
    }

    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Store.Log(exception.ToString());
        livePage.ShowIssue(exception.Message, InfoBarSeverity.Error);
        gamesPage.ShowStatus(exception.Message, InfoBarSeverity.Error);
        settingsPage.ShowStatus(exception.Message, InfoBarSeverity.Error);
    }

    public bool IsActive(string profileId)
    {
        return IsProfileActive(profileId);
    }

    public bool IsProfileActive(string profileId)
    {
        return monitor.Session.Active && monitor.ActiveProfileId == profileId;
    }

    public void EndSession()
    {
        monitor.EndSession(this);
    }

    public void EnsureModes(GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        DisplayBrowser display = gamesPage.Display;
        if (Controls.Value(display.GameResolution) is not DisplayMode gameMode || (!profile.AutoBase && Controls.Value(display.BaseResolution) is not DisplayMode))
            throw new InvalidOperationException("Choose a validated baseline and game resolution.");
        MonitorInfo screen = display.Resolve(profile.MonitorDevice);
        bool owned = monitor.Session.Active && string.Equals(monitor.Session.Data?.MonitorDevice, screen.Device, StringComparison.Ordinal);
        if (profile.AutoBase && !owned)
        {
            DisplayMode live = display.ReadCurrent(screen.Device);
            int hz = display.SelectedRefresh() ?? 0;
            if (hz != live.Refresh || !Rules.SameMode(live, display.CurrentMode))
            {
                ReloadModes(false);
                throw new InvalidOperationException("The Windows display mode changed. Review the updated resolution list before continuing.");
            }
        }

        DisplayMode? baseline = profile.AutoBase
            ? owned ? display.CurrentMode : display.ReadCurrent(screen.Device)
            : Controls.Value(display.BaseResolution) as DisplayMode;
        if (baseline == null || !display.Test(screen.Device, gameMode) || !display.Test(screen.Device, baseline))
        {
            ReloadModes(false);
            throw new InvalidOperationException("The selected mode is no longer available. Choose a validated driver mode.");
        }
    }

    public void ApplyProfile(GameProfile profile)
    {
        monitor.Apply(this, profile);
    }

    public void TestGame(GameProfile profile)
    {
        monitor.TestProfile(this, profile);
    }

    public Task EditGameAsync(bool editing)
    {
        return gameEditor.EditAsync(editing);
    }

    public Task RemoveGameAsync()
    {
        return gameEditor.RemoveAsync();
    }

    public void QueueIcon(GameProfile game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (string.IsNullOrWhiteSpace(game.UniverseIds))
            return;
        string id = game.Id;
        string universes = game.UniverseIds;
        _ = LoadIconAsync(id, universes);
    }

    public void SetMonitoring(bool enabled)
    {
        if (enabled && configError != null)
        {
            settingsPage.SetMonitoring(false);
            settingsPage.ShowStatus("Reset settings before monitoring.", InfoBarSeverity.Warning);
            return;
        }

        monitor.SetMonitoring(this, enabled);
    }

    public Task ResetSettingsAsync()
    {
        return gameEditor.ResetAsync();
    }

    public void BeginShortcutCapture(bool emergency)
    {
        captureEmergency = emergency;
        settingsPage.ShowCapture(emergency);
        root.Focus(FocusState.Programmatic);
    }

    public void ClearShortcut(bool emergency)
    {
        captureEmergency = null;
        if (emergency)
        {
            settings.EmergencyModifiers = 0;
            settings.EmergencyKey = 0;
        }
        else
        {
            settings.MonitorModifiers = 0;
            settings.MonitorKey = 0;
        }

        SaveShortcuts();
    }

    public string? EmergencyStop()
    {
        return monitor.Shutdown();
    }

    private void BuildShell()
    {
        const double navigationRail = 48;
        chromeBar = new Grid
        {
            Height = 40,
            ColumnSpacing = 10,
            Padding = new Thickness(0, 0, 160, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        chromeBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(navigationRail) });
        chromeBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Image logo = new()
        {
            Width = 24,
            Height = 24,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        logo.ImageFailed += (_, error) => Store.Log("Loading RbxDisplay logo: " + error.ErrorMessage);
        logo.Source = new BitmapImage(new Uri("ms-appx:///Assets/RbxDisplay.png"));
        AutomationProperties.SetName(logo, "RbxDisplay logo");
        chromeBar.Children.Add(logo);
        // TextBlock name = new()
        // {
        //     Text = "RbxDisplay",
        //     FontSize = 14,
        //     FontWeight = FontWeights.SemiBold,
        //     VerticalAlignment = VerticalAlignment.Center,
        //     Foreground = Theme.Brush(Theme.Foreground)
        // };
        // Grid.SetColumn(name, 1);
        // chromeBar.Children.Add(name);

        NavigationViewItem live = new()
        {
            Content = "Live",
            Tag = "live",
            Icon = new FontIcon { Glyph = "\uE80F" }
        };
        NavigationViewItem games = new()
        {
            Content = "Games",
            Tag = "games",
            Icon = new FontIcon { Glyph = "\uE7FC" }
        };
        NavigationViewItem settingsItem = new()
        {
            Content = "Settings",
            Tag = "settings",
            Icon = new AnimatedIcon
            {
                Source = new AnimatedSettingsVisualSource(),
                FallbackIconSource = new FontIconSource { Glyph = "\uE713" }
            }
        };
        NavigationView navigation = new()
        {
            PaneDisplayMode = NavigationViewPaneDisplayMode.LeftCompact,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            IsPaneToggleButtonVisible = false,
            IsTitleBarAutoPaddingEnabled = false,
            AlwaysShowHeader = false,
            CompactPaneLength = navigationRail,
            IsPaneOpen = false,
            Content = content
        };
        navigation.MenuItems.Add(live);
        navigation.MenuItems.Add(games);
        navigation.FooterMenuItems.Add(settingsItem);
        content.Content = livePage;
        content.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        content.VerticalContentAlignment = VerticalAlignment.Stretch;
        navigation.SelectedItem = live;
        navigation.SelectionChanged += (_, args) =>
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                content.Content = tag switch
                {
                    "games" => gamesPage,
                    "settings" => settingsPage,
                    _ => livePage
                };
            }
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(chromeBar);
        Grid.SetRow(navigation, 1);
        root.Children.Add(navigation);
    }

    private void Initialize()
    {
        if (initialized)
            return;
        FitWindow(true);
        string? recovery = File.Exists(Store.SessionPath) ? Recovery.FromJournal(null) : null;
        gamesPage.Bind(settings);
        bool displaysReady = false;
        try
        {
            ReloadModes(true);
            displaysReady = true;
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }

        settingsPage.SetTrayAvailable(native.TrayAvailable);
        settingsPage.ShowShortcuts(settings, native.EmergencyReady, native.MonitorReady);
        initialized = true;
        if (configError != null)
        {
            ShowPaused("Settings could not be loaded: " + configError + "\nYour file was left unchanged. Reset settings to create a backup and start with defaults.");
        }
        else if (recovery != null)
        {
            ShowPaused("Restoration needs attention: " + recovery);
        }
        else
        {
            monitor.Arm(this);
            if (displaysReady && gamesPage.EditorProfile() != null && gamesPage.Display.GameResolution.SelectedItem == null)
                gamesPage.ShowStatus("Choose a supported game resolution at the baseline refresh rate.", InfoBarSeverity.Warning);
        }

        timer.Start();
        GameProfile? preset = settings.Games.FirstOrDefault(game => game.Id == "bloxstrike");
        if (preset != null)
            QueueIcon(preset);
        _ = ResolveMissingUniversesAsync(configError == null && recovery == null);
    }

    private void ShowPaused(string message)
    {
        ApplyChrome(new LiveState
        {
            Badge = "Paused",
            Title = "Monitoring is off",
            Message = message,
            Tone = LiveTone.Warning,
            Tray = "RbxDisplay — paused"
        });
        livePage.ShowIssue(message, InfoBarSeverity.Error);
        SetTray("RbxDisplay — paused");
    }

    private async Task ResolveMissingUniversesAsync(bool announceFailure)
    {
        if (configError != null)
            return;
        bool saved = false;
        foreach (GameProfile game in settings.Games.Where(game => string.IsNullOrWhiteSpace(game.UniverseIds)).ToList())
        {
            string original = game.PlaceIds;
            string places;
            try
            {
                places = PlaceInput.Normalize(original);
            }
            catch (InvalidOperationException ex)
            {
                Store.Log("Resolving universe ID for " + game.Name + ": " + ex.Message);
                if (saved)
                    SaveResolvedUniverses();
                if (announceFailure)
                    livePage.ShowIssue("Universe ID lookup failed for " + game.Name + ": " + ex.Message + "\nEdit the game while online and save it again.", InfoBarSeverity.Warning);
                break;
            }

            string universes;
            try
            {
                universes = await UniverseLookup.ResolveAsync(places);
            }
            catch (Exception ex)
            {
                Store.Log("Resolving universe ID for " + game.Name + ": " + ex.Message);
                if (saved)
                    SaveResolvedUniverses();
                if (announceFailure)
                    livePage.ShowIssue("Universe ID lookup failed for " + game.Name + ": " + ex.Message + "\nEdit the game while online and save it again.", InfoBarSeverity.Warning);
                return;
            }

            if (!settings.Games.Contains(game) || !string.Equals(game.PlaceIds, original, StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(game.UniverseIds))
                continue;
            game.PlaceIds = places;
            game.UniverseIds = universes;
            saved = true;
            QueueIcon(game);
        }

        if (saved)
            SaveResolvedUniverses();
    }

    private void SaveResolvedUniverses()
    {
        try
        {
            ProfileRepository.Save(settings);
            gamesPage.Bind(settings);
            ReloadModes(false);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    private void FitWindow(bool first)
    {
        if (closing)
            return;
        double scale = root.XamlRoot?.RasterizationScale ?? 1;
        RectInt32? area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (!area.HasValue)
            return;
        int width = Math.Min(first ? (int)Math.Round(1080 * scale) : AppWindow.Size.Width, Math.Max(100, area.Value.Width - 32));
        int height = Math.Min(first ? (int)Math.Round(760 * scale) : AppWindow.Size.Height, Math.Max(100, area.Value.Height - 32));
        int x = first ? area.Value.X + (area.Value.Width - width) / 2 : Math.Clamp(AppWindow.Position.X, area.Value.X, area.Value.X + area.Value.Width - width);
        int y = first ? area.Value.Y + (area.Value.Height - height) / 2 : Math.Clamp(AppWindow.Position.Y, area.Value.Y, area.Value.Y + area.Value.Height - height);
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void ApplyChrome(LiveState state)
    {
        livePage.Show(state);
        native.SetMenuStatus(state.Badge + " — " + state.Title);
        settingsPage.SetMonitoring(monitor.Monitoring);
    }

    private async Task LoadIconAsync(string id, string universes)
    {
        bool ready = await GameIcon.TryEnsureAsync(id, universes);
        if (!ready || closing)
            return;
        DispatcherQueue.TryEnqueue(() =>
        {
            gamesPage.RefreshIcons();
            livePage.RefreshIcon();
        });
    }

    private void OnShortcutKey(object sender, KeyRoutedEventArgs e)
    {
        if (captureEmergency == null || HotkeyText.IsModifier(e.Key))
            return;
        e.Handled = true;
        if (e.Key == VirtualKey.Escape)
        {
            captureEmergency = null;
            settingsPage.ShowShortcuts(settings, native.EmergencyReady, native.MonitorReady);
            return;
        }

        int modifiers = Native.PressedModifiers();
        if (modifiers == 0)
        {
            settingsPage.ShowStatus("Include Ctrl, Alt, Shift, or the Windows key.", InfoBarSeverity.Warning);
            return;
        }

        int key = (int)e.Key;
        bool emergency = captureEmergency.Value;
        int otherModifiers = emergency ? settings.MonitorModifiers : settings.EmergencyModifiers;
        int otherKey = emergency ? settings.MonitorKey : settings.EmergencyKey;
        if (otherKey != 0 && otherKey == key && otherModifiers == modifiers)
        {
            settingsPage.ShowStatus("Emergency restore and monitoring cannot share one shortcut.", InfoBarSeverity.Warning);
            return;
        }

        if (emergency)
        {
            settings.EmergencyModifiers = modifiers;
            settings.EmergencyKey = key;
        }
        else
        {
            settings.MonitorModifiers = modifiers;
            settings.MonitorKey = key;
        }

        captureEmergency = null;
        SaveShortcuts();
    }

    private void SaveShortcuts()
    {
        if (configError != null)
        {
            settingsPage.ShowStatus("Reset settings before changing shortcuts.", InfoBarSeverity.Warning);
            settingsPage.ShowShortcuts(settings, native.EmergencyReady, native.MonitorReady);
            return;
        }

        try
        {
            ProfileRepository.Save(settings);
            native.ApplyHotkeys(settings.EmergencyModifiers, settings.EmergencyKey, settings.MonitorModifiers, settings.MonitorKey);
            settingsPage.ShowShortcuts(settings, native.EmergencyReady, native.MonitorReady);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    internal void HideToTray()
    {
        if (native.TrayAvailable)
            AppWindow.Hide();
        else
            settingsPage.ShowStatus("The system tray icon is unavailable. Keep this window open or minimize it with the title bar.", InfoBarSeverity.Warning);
    }

    private void EmergencyRestore()
    {
        try
        {
            monitor.RestoreDisplay(this, "Display restored. The profile applies again after the game loses focus.");
            if (configError == null && !File.Exists(Store.SessionPath))
                monitor.KeepWatching();
        }
        catch (Exception ex)
        {
            ReportSessionFailure(ex);
        }
    }

    private void ShowWindow()
    {
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Restore();
        Activate();
    }

    private void OnClosing()
    {
        if (closing)
            return;
        closing = true;
        timer.Stop();
        string? error = monitor.Shutdown();
        native.Dispose();
        Native.CloseColor();
        if (error != null)
        {
            Store.Log("Closing: " + error);
            NativeWindow.Message("The session could not be fully restored:\n" + error + "\nUse Restore.cmd after resolving the issue.", "RbxDisplay");
        }
    }
}
