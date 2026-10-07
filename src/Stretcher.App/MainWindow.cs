using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace Stretcher;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The tray handler is disposed when the window closes.")]
internal sealed class MainWindow : Window, IProfileHost, IGameHost
{
    private AppSettings settings;
    private string? configError;
    private readonly bool migrated;
    private readonly DisplayBrowser display;
    private readonly MonitoringController monitor = new();
    private readonly GameEditor gameEditor;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly ComboBox games = Controls.Picker("Saved game");
    private readonly CheckBox autoBase = Controls.Check("Use the current Windows resolution as the baseline");
    private readonly CheckBox saturationEnabled = Controls.Check("Enable saturation");
    private readonly CheckBox focusResolution = Controls.Check("Restore resolution when the game loses focus");
    private readonly Slider saturation = new() { Minimum = 0, Maximum = 200, StepFrequency = 1, SmallChange = 5, LargeChange = 10 };
    private readonly TextBlock placeHint = Controls.Note("");
    private readonly TextBlock saturationText = Controls.Note("135%");
    private readonly TextBlock status = Controls.Note("Ready.");
    private readonly Button start = Controls.ActionButton("Start monitoring");
    private readonly Button test = Controls.ActionButton("Test for 15 seconds");
    private readonly Button save = Controls.ActionButton("Save profile");
    private readonly Button launch = Controls.ActionButton("Play selected game");
    private readonly Button refresh = Controls.ActionButton("Refresh");
    private readonly Button add = Controls.ActionButton("Add game");
    private readonly Button edit = Controls.ActionButton("Edit");
    private readonly Button remove = Controls.ActionButton("Remove");
    private readonly Button restore = Controls.ActionButton("Restore and stop");
    private readonly Button hide = Controls.ActionButton("Minimize to tray");
    private readonly Button reset = Controls.ActionButton("Reset settings");
    private readonly StackPanel editor = new() { Spacing = 16 };
    private readonly ContentControl editorHost = new() { IsTabStop = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel baseline = new() { Spacing = 8 };
    private readonly Grid actionGrid = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly Grid gameActions = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly Grid root = new() { Background = Theme.Brush(Theme.Background), RequestedTheme = ElementTheme.Dark };
    private readonly NativeWindow native;
    private bool updating;
    private bool initialized;
    private bool closing;

    public MainWindow(AppSettings initial, string? startupError, bool imported)
    {
        settings = initial;
        configError = startupError;
        migrated = imported;
        display = new DisplayBrowser(value => updating = value, UpdateControls);
        gameEditor = new GameEditor(this);
        Title = "Stretcher";
        Content = root;
        root.Language = "en-US";
        BuildInterface();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Stretcher.ico"));
        AppWindow.TitleBar.BackgroundColor = Theme.Background;
        AppWindow.TitleBar.ForegroundColor = Theme.Foreground;
        AppWindow.TitleBar.ButtonBackgroundColor = Theme.Background;
        AppWindow.TitleBar.ButtonForegroundColor = Theme.Foreground;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Theme.Accent;
        AppWindow.TitleBar.ButtonHoverForegroundColor = Theme.Background;
        AppWindow.TitleBar.InactiveBackgroundColor = Theme.Background;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Theme.Background;
        AppWindow.Resize(new SizeInt32(620, 900));
        native = new NativeWindow(WinRT.Interop.WindowNative.GetWindowHandle(this),
            () => DispatcherQueue.TryEnqueue(ShowWindow),
            () => DispatcherQueue.TryEnqueue(() => Guard(() => monitor.StopProfile(this, "Emergency restore completed. Monitoring stopped."))),
            () => DispatcherQueue.TryEnqueue(Close),
            () => DispatcherQueue.TryEnqueue(() => { display.ModesDirty = true; FitWindowToDesktop(false); }));
        root.Loaded += (_, _) => Initialize();
        root.SizeChanged += (_, e) => AdaptLayout(e.NewSize.Width);
        Closed += (_, _) => OnClosing();
        timer.Tick += (_, _) => monitor.Tick(this);
        BindEvents();
    }

    public bool IsClosing => closing;
    public bool DialogOpen => gameEditor.DialogOpen;
    public bool Initialized => initialized;
    public AppSettings Settings => settings;
    public DisplayBrowser Display => display;
    public bool IsBusy => monitor.Monitoring || monitor.Testing;
    public string? ConfigError { get => configError; set => configError = value; }
    public XamlRoot XamlRoot => root.XamlRoot ?? throw new InvalidOperationException("The window is not ready.");

    private void BuildInterface()
    {
        ScrollViewer scroll = new()
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled,
            IsTabStop = false
        };
        root.Children.Add(scroll);
        StackPanel content = new()
        {
            Spacing = 18,
            Margin = new Thickness(24, 22, 24, 24),
            MaxWidth = 660,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        scroll.Content = content;
        Grid header = new() { ColumnSpacing = 16 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Image logo = new() { Width = 64, Height = 64, Stretch = Stretch.Uniform };
        logo.ImageFailed += (_, error) => Store.Log("Loading Stretcher logo: " + error.ErrorMessage);
        logo.Source = new BitmapImage(new Uri("ms-appx:///Assets/Stretcher.png"));
        AutomationProperties.SetName(logo, "Stretcher logo");
        header.Children.Add(logo);
        StackPanel headings = new() { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        headings.Children.Add(new TextBlock
        {
            Text = "Stretcher",
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Brush(Theme.Foreground)
        });
        headings.Children.Add(Controls.Note("Your games. Your resolution and colors."));
        Grid.SetColumn(headings, 1);
        header.Children.Add(headings);
        content.Children.Add(header);
        editorHost.Content = editor;
        content.Children.Add(editorHost);
        StackPanel gameSection = Controls.Section("GAME");
        gameSection.Children.Add(games);
        Controls.ConfigureGrid(gameActions, 3, [add, edit, remove]);
        gameSection.Children.Add(gameActions);
        gameSection.Children.Add(placeHint);
        editor.Children.Add(Controls.Card(gameSection));
        StackPanel displaySection = Controls.Section("DISPLAY");
        Grid monitorRow = new() { ColumnSpacing = 8 };
        monitorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        monitorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        monitorRow.Children.Add(display.Monitors);
        Grid.SetColumn(refresh, 1);
        monitorRow.Children.Add(refresh);
        displaySection.Children.Add(monitorRow);
        displaySection.Children.Add(display.Actual);
        displaySection.Children.Add(autoBase);
        baseline.Children.Add(Controls.Note("BASELINE REFRESH RATE"));
        baseline.Children.Add(display.BaseHz);
        baseline.Children.Add(Controls.Note("BASELINE RESOLUTION"));
        baseline.Children.Add(display.BaseResolution);
        displaySection.Children.Add(baseline);
        displaySection.Children.Add(Controls.Note("GAME RESOLUTION"));
        displaySection.Children.Add(display.GameResolution);
        displaySection.Children.Add(display.ModeHint);
        editor.Children.Add(Controls.Card(displaySection));
        StackPanel colorSection = Controls.Section("COLORS");
        Grid colorHeading = new() { ColumnSpacing = 8 };
        colorHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colorHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        colorHeading.Children.Add(saturationEnabled);
        Grid.SetColumn(saturationText, 1);
        saturationText.VerticalAlignment = VerticalAlignment.Center;
        colorHeading.Children.Add(saturationText);
        colorSection.Children.Add(colorHeading);
        colorSection.Children.Add(saturation);
        AutomationProperties.SetName(saturation, "Saturation percentage");
        colorSection.Children.Add(Controls.Note("100% keeps the original colors. Saturation affects all displays while your game is focused."));
        colorSection.Children.Add(focusResolution);
        colorSection.Children.Add(Controls.Note("Colors return on Alt+Tab. By default, resolution stays stretched until you leave the game."));
        editor.Children.Add(Controls.Card(colorSection));
        start.Background = Theme.Brush(Theme.Accent);
        start.Foreground = Theme.Brush(Theme.Background);
        start.FontWeight = FontWeights.SemiBold;
        Controls.ConfigureGrid(actionGrid, 2, [start, restore, save, test, launch, hide]);
        content.Children.Add(actionGrid);
        reset.Visibility = Visibility.Collapsed;
        content.Children.Add(reset);
        status.Foreground = Theme.Brush(Theme.Foreground);
        content.Children.Add(Controls.Card(new StackPanel { Children = { status } }));
        content.Children.Add(Controls.Note("Emergency restore: Ctrl+Alt+F12"));
    }

    private void AdaptLayout(double width)
    {
        int columns = width < 490 ? 1 : 2;
        if (actionGrid.ColumnDefinitions.Count != columns)
            Controls.ConfigureGrid(actionGrid, columns, [start, restore, save, test, launch, hide]);
        int gameColumns = width < 390 ? 1 : 3;
        if (gameActions.ColumnDefinitions.Count != gameColumns)
            Controls.ConfigureGrid(gameActions, gameColumns, [add, edit, remove]);
    }

    private void Initialize()
    {
        if (initialized)
            return;
        initialized = true;
        FitWindowToDesktop(true);
        string? recovery = File.Exists(Store.SessionPath) ? Recovery.FromJournal(null) : null;
        LoadGames();
        LoadProfile();
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

        if (configError != null)
            SetStatus("Settings could not be loaded: " + configError + "\nYour file was left unchanged. Reset settings to create a backup and start with defaults.");
        else if (recovery != null)
            SetStatus("Restoration needs attention: " + recovery);
        else if (!native.HotkeyAvailable)
            SetStatus("Ctrl+Alt+F12 is already in use. Restore and stop remains available.");
        else if (displaysReady && migrated)
            SetStatus("Your previous settings were imported. Choose a game and save its profile.");
        else if (displaysReady)
            SetStatus(display.GameResolution.SelectedItem == null ? "Choose a supported game resolution at the baseline refresh rate." : "Ready. Test your profile, then start monitoring.");
        UpdateControls();
        timer.Start();
        bool quiet = configError == null && recovery == null && native.HotkeyAvailable;
        _ = ResolveMissingUniversesAsync(quiet);
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
                    SetStatus("Universe ID lookup failed for " + game.Name + ": " + ex.Message + "\nEdit the game while online and save it again.");
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
                    SetStatus("Universe ID lookup failed for " + game.Name + ": " + ex.Message + "\nEdit the game while online and save it again.");
                return;
            }

            if (!settings.Games.Contains(game) || !string.Equals(game.PlaceIds, original, StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(game.UniverseIds))
                continue;
            game.PlaceIds = places;
            game.UniverseIds = universes;
            saved = true;
        }

        if (saved)
            SaveResolvedUniverses();
    }

    private void SaveResolvedUniverses()
    {
        try
        {
            ProfileRepository.Save(settings);
            LoadProfile();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    private void FitWindowToDesktop(bool first)
    {
        if (closing)
            return;
        double scale = root.XamlRoot?.RasterizationScale ?? 1;
        RectInt32? area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (!area.HasValue)
            return;
        int width = Math.Min(first ? (int)Math.Round(620 * scale) : AppWindow.Size.Width, Math.Max(100, area.Value.Width - 32));
        int height = Math.Min(first ? (int)Math.Round(900 * scale) : AppWindow.Size.Height, Math.Max(100, area.Value.Height - 32));
        int x = first ? area.Value.X + (area.Value.Width - width) / 2 : Math.Clamp(AppWindow.Position.X, area.Value.X, area.Value.X + area.Value.Width - width);
        int y = first ? area.Value.Y + (area.Value.Height - height) / 2 : Math.Clamp(AppWindow.Position.Y, area.Value.Y, area.Value.Y + area.Value.Height - height);
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void BindEvents()
    {
        games.SelectionChanged += (_, _) => { if (initialized && !updating) Guard(SwitchGame); };
        display.Monitors.SelectionChanged += (_, _) => { if (initialized && !updating) Guard(() => ReloadModes(false)); };
        autoBase.Checked += (_, _) => { if (initialized && !updating) Guard(() => ReloadModes(false)); };
        autoBase.Unchecked += (_, _) => { if (initialized && !updating) Guard(() => ReloadModes(false)); };
        display.BaseHz.SelectionChanged += (_, _) =>
        {
            if (initialized && !updating)
                Guard(() => display.Populate(Controls.Value(display.BaseResolution) as DisplayMode, Controls.Value(display.GameResolution) as DisplayMode, settings, autoBase.IsChecked == true));
        };
        display.BaseResolution.SelectionChanged += (_, _) => UpdateControls();
        display.GameResolution.SelectionChanged += (_, _) => UpdateControls();
        saturation.ValueChanged += (_, _) => UpdateControls();
        saturationEnabled.Checked += (_, _) => UpdateControls();
        saturationEnabled.Unchecked += (_, _) => UpdateControls();
        add.Click += async (_, _) => await gameEditor.EditAsync(false);
        edit.Click += async (_, _) => await gameEditor.EditAsync(true);
        remove.Click += async (_, _) => await gameEditor.RemoveAsync();
        save.Click += (_, _) => Guard(() => { SaveUi(); SetStatus("Profile saved. Saving does not change the display."); });
        start.Click += (_, _) => Guard(() => monitor.ToggleMonitoring(this));
        test.Click += (_, _) => Guard(() => monitor.TestProfile(this));
        restore.Click += (_, _) => Guard(() => monitor.StopProfile(this, "Monitoring stopped. Display settings restored."));
        launch.Click += (_, _) => Guard(() => monitor.LaunchGame(this));
        refresh.Click += (_, _) => Guard(() => { ReloadModes(true); SetStatus("Display modes refreshed at the baseline refresh rate."); });
        hide.Click += (_, _) =>
        {
            if (native.TrayAvailable)
                AppWindow.Hide();
            else
                SetStatus("The system tray icon is unavailable. Keep this window open or minimize it with the title bar.");
        };
        reset.Click += async (_, _) => await gameEditor.ResetAsync();
    }

    private void LoadGames()
    {
        updating = true;
        try
        {
            Controls.Fill(games, settings.Games, game => game.Name, game => game.Id == settings.SelectedGameId);
        }
        finally
        {
            updating = false;
        }
    }

    private void LoadProfile()
    {
        GameProfile game = settings.SelectedGame();
        updating = true;
        try
        {
            autoBase.IsChecked = settings.AutoBase;
            saturationEnabled.IsChecked = game.SaturationEnabled;
            saturation.Value = Math.Round(game.Saturation * 100);
            focusResolution.IsChecked = game.ResolutionOnFocusOnly;
            placeHint.Text = string.IsNullOrWhiteSpace(game.UniverseIds)
                ? "Universe ID: not saved yet. Edit this game while online."
                : "Universe ID: " + game.UniverseIds;
        }
        finally
        {
            updating = false;
        }

        UpdateControls();
    }

    public AppSettings ReadEditors(bool requireModes)
    {
        if (configError != null)
            throw new InvalidOperationException("Reset or repair your saved settings before editing a profile.");
        if (monitor.IsBusy)
            throw new InvalidOperationException("Restore and stop before editing profiles.");
        AppSettings next = settings.Copy();
        GameProfile game = next.SelectedGame();
        DisplayMode? selected = Controls.Value(display.GameResolution) as DisplayMode;
        DisplayMode? baselineMode = Controls.Value(display.BaseResolution) as DisplayMode;
        if (Controls.Value(display.Monitors) is MonitorInfo selectedMonitor)
            next.MonitorDevice = selectedMonitor.Device;
        if (requireModes && (selected == null || (autoBase.IsChecked != true && baselineMode == null)))
            throw new InvalidOperationException("Choose a validated baseline and game resolution.");
        if (selected != null)
        {
            game.GameWidth = selected.Width;
            game.GameHeight = selected.Height;
        }

        game.SaturationEnabled = saturationEnabled.IsChecked == true;
        game.Saturation = saturation.Value / 100;
        game.ResolutionOnFocusOnly = focusResolution.IsChecked == true;
        next.AutoBase = autoBase.IsChecked == true;
        if (baselineMode != null)
        {
            next.BaseWidth = baselineMode.Width;
            next.BaseHeight = baselineMode.Height;
        }

        next.BaseRefresh = next.AutoBase ? 0 : Controls.Value(display.BaseHz) is int hz ? hz : next.BaseRefresh;
        next.Validate();
        return next;
    }

    private void SwitchGame()
    {
        if (Controls.Value(games) is not GameProfile selected || selected.Id == settings.SelectedGameId)
            return;
        try
        {
            AppSettings next = ReadEditors(false);
            next.SelectedGameId = selected.Id;
            ProfileRepository.Save(next);
            settings = next;
            LoadGames();
            LoadProfile();
            ReloadModes(false, false);
            SetStatus("Selected " + settings.SelectedGame().Name + ". Its saved profile is ready to edit.");
        }
        catch
        {
            LoadGames();
            throw;
        }
    }

    public void Commit(AppSettings next, string status, bool refreshMonitors)
    {
        settings = next;
        LoadGames();
        LoadProfile();
        ReloadModes(refreshMonitors, false);
        SetStatus(status);
    }

    public void ReloadModes(bool refreshMonitors)
    {
        ReloadModes(refreshMonitors, true);
    }

    private void ReloadModes(bool refreshMonitors, bool preserveChoices)
    {
        display.Reload(refreshMonitors, preserveChoices, settings, autoBase.IsChecked == true, monitor.Session.Active);
    }

    public void UpdateControls()
    {
        if (!initialized || updating)
            return;
        bool busy = monitor.IsBusy;
        editorHost.IsEnabled = !busy && configError == null && !gameEditor.DialogOpen;
        baseline.Visibility = autoBase.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        saturation.IsEnabled = saturationEnabled.IsChecked == true;
        saturationText.Text = Math.Round(saturation.Value) + "%";
        bool selected = display.GameResolution.SelectedItem != null && (autoBase.IsChecked == true || display.BaseResolution.SelectedItem != null);
        bool ready = selected && configError == null && !gameEditor.DialogOpen && !File.Exists(Store.SessionPath);
        save.IsEnabled = test.IsEnabled = !busy && ready;
        start.IsEnabled = busy || ready;
        start.Content = new TextBlock { Text = busy ? "Stop monitoring" : "Start monitoring", TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        launch.IsEnabled = !monitor.Testing && (monitor.Monitoring || ready) && !gameEditor.DialogOpen;
        remove.IsEnabled = settings.Games.Count > 1;
        hide.IsEnabled = native.TrayAvailable;
        reset.Visibility = configError == null ? Visibility.Collapsed : Visibility.Visible;
        reset.IsEnabled = !gameEditor.DialogOpen;
        native.UpdateTooltip("Stretcher — " + (monitor.Testing ? "testing" : monitor.Monitoring ? "monitoring " + settings.SelectedGame().Name : "ready"));
    }

    public void SaveUi()
    {
        AppSettings next = ReadEditors(true);
        MonitorInfo selected = display.SelectedMonitor();
        DisplayMode live = display.ReadCurrent(selected.Device);
        int hz = display.SelectedRefresh() ?? 0;
        if (next.AutoBase && (hz != live.Refresh || !Rules.SameMode(live, display.CurrentMode)))
        {
            ReloadModes(false);
            throw new InvalidOperationException("The Windows display mode changed. Review the updated resolution list before continuing.");
        }

        DisplayMode? game = Controls.Value(display.GameResolution) as DisplayMode;
        DisplayMode? baselineMode = next.AutoBase ? live : Controls.Value(display.BaseResolution) as DisplayMode;
        if (game == null || baselineMode == null || hz == 0 || !display.Test(selected.Device, game) || !display.Test(selected.Device, baselineMode))
        {
            ReloadModes(false);
            throw new InvalidOperationException("The selected mode is no longer available. Choose a validated driver mode.");
        }

        ProfileRepository.Save(next);
        settings = next;
        monitor.Runtime = settings.Runtime(settings.SelectedGame());
        monitor.ResetPoll();
    }

    public string? EmergencyStop()
    {
        return monitor.StopCore(this);
    }

    public void SetStatus(string value)
    {
        status.Text = value;
    }

    public void ReportError(Exception exception)
    {
        string? restoreError = monitor.StopCore(this);
        Store.Log(exception.ToString());
        SetStatus(exception.Message + (restoreError == null ? "" : "\nRestoration needs attention: " + restoreError));
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
        string? error = monitor.StopCore(this);
        native.Dispose();
        Native.CloseColor();
        if (error != null)
        {
            Store.Log("Closing: " + error);
            NativeWindow.Message("The session could not be fully restored:\n" + error + "\nUse Restore.cmd after resolving the issue.", "Stretcher");
        }
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }
}
