using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace RbxDisplay;

internal sealed class GamesPage : UserControl
{
    private readonly MainWindow shell;
    private readonly DispatcherTimer saturationTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly ListView list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock detailTitle = new()
    {
        FontSize = 22,
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        Foreground = Theme.Brush(Theme.Foreground)
    };
    private readonly TextBlock placeHint = Controls.Note("");
    private readonly ToggleSwitch autoBase = Controls.Toggle("Use the current Windows resolution as the baseline");
    private readonly ToggleSwitch saturationEnabled = Controls.Toggle("Enable saturation");
    private readonly ToggleSwitch focusResolution = Controls.Toggle("Restore resolution when the game loses focus");
    private readonly Slider saturation = new() { Minimum = 0, Maximum = 200, StepFrequency = 1, SmallChange = 5, LargeChange = 10 };
    private readonly TextBlock saturationText = Controls.Note("135%");
    private readonly StackPanel baseline = new() { Spacing = 8 };
    private readonly Button add = Controls.ActionButton("Add game");
    private readonly Button edit = Controls.ActionButton("Edit");
    private readonly Button remove = Controls.ActionButton("Remove");
    private readonly Button play = Controls.ActionButton("Play");
    private readonly Button test = Controls.ActionButton("Test for 15 seconds");
    private readonly Button refresh = new()
    {
        Content = new FontIcon
        {
            Glyph = "\uE72C",
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        },
        MinWidth = 0,
        MinHeight = 0,
        Padding = new Thickness(0),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        Foreground = Theme.Brush(Theme.Foreground)
    };
    private readonly InfoBar overlap = Controls.Banner(InfoBarSeverity.Warning);
    private readonly InfoBar active = Controls.Banner(InfoBarSeverity.Informational);
    private readonly InfoBar status = Controls.Banner(InfoBarSeverity.Informational);
    private readonly Grid body = new() { ColumnSpacing = 16, RowSpacing = 16 };
    private readonly Border listCard;
    private readonly ScrollViewer detailScroll;
    private int quiet;
    private bool wide = true;
    private string selectedId = "";

    public GamesPage(MainWindow owner)
    {
        shell = owner;
        Display = new DisplayBrowser(SetQuiet, UpdateChrome);
        overlap.Message = "Two saved games share a Universe ID. RbxDisplay uses the earlier game in this list.";
        active.Message = "This profile is applied to the running game. Changes take effect immediately.";
        play.Background = Theme.Brush(Theme.Accent);
        play.Foreground = Theme.Brush(Theme.Background);
        play.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetName(saturation, "Saturation percentage");
        AutomationProperties.SetName(refresh, "Refresh");
        ToolTipService.SetToolTip(refresh, "Refresh");
        Display.Monitors.VerticalAlignment = VerticalAlignment.Center;
        Display.Monitors.SizeChanged += (_, _) => MatchRefreshButton();
        Loaded += (_, _) => MatchRefreshButton();

        Grid listGrid = new() { RowSpacing = 8 };
        listGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        listGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        list.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        listGrid.Children.Add(list);
        KeepOnOneLine(add);
        KeepOnOneLine(edit);
        KeepOnOneLine(remove);
        add.HorizontalAlignment = HorizontalAlignment.Stretch;
        StackPanel actions = new() { Spacing = 8 };
        actions.Children.Add(add);
        Grid editRow = new() { ColumnSpacing = 8 };
        Controls.ConfigureGrid(editRow, 2, [edit, remove]);
        actions.Children.Add(editRow);
        Grid.SetRow(actions, 1);
        listGrid.Children.Add(actions);
        listCard = new Border
        {
            Background = Theme.Brush(Theme.Card),
            BorderBrush = Theme.Brush(Theme.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Child = listGrid
        };

        StackPanel identity = new() { Spacing = 2 };
        identity.Children.Add(detailTitle);
        identity.Children.Add(placeHint);
        StackPanel detail = new() { Spacing = 14 };
        detail.Children.Add(identity);
        detail.Children.Add(active);
        StackPanel displaySection = Controls.Section("DISPLAY");
        Grid monitorRow = new() { ColumnSpacing = 8 };
        monitorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        monitorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        monitorRow.Children.Add(Display.Monitors);
        Grid.SetColumn(refresh, 1);
        monitorRow.Children.Add(refresh);
        displaySection.Children.Add(monitorRow);
        displaySection.Children.Add(Display.Actual);
        displaySection.Children.Add(autoBase);
        baseline.Children.Add(Controls.Note("BASELINE REFRESH RATE"));
        baseline.Children.Add(Display.BaseHz);
        baseline.Children.Add(Controls.Note("BASELINE RESOLUTION"));
        baseline.Children.Add(Display.BaseResolution);
        displaySection.Children.Add(baseline);
        displaySection.Children.Add(Controls.Note("GAME RESOLUTION"));
        displaySection.Children.Add(Display.GameResolution);
        displaySection.Children.Add(Display.ModeHint);
        detail.Children.Add(Controls.Card(displaySection));
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
        colorSection.Children.Add(focusResolution);
        detail.Children.Add(Controls.Card(colorSection));
        Grid playRow = new() { ColumnSpacing = 8 };
        Controls.ConfigureGrid(playRow, 2, [play, test]);
        detail.Children.Add(playRow);
        detail.Children.Add(overlap);
        detail.Children.Add(status);
        detailScroll = new ScrollViewer
        {
            Content = detail,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        StackPanel heading = Controls.Heading("Games", "Each game keeps its own monitor, resolution, and colors. RbxDisplay picks the profile from the running game.");
        body.Children.Add(listCard);
        body.Children.Add(detailScroll);
        Grid page = new() { RowSpacing = 16, Margin = new Thickness(32, 12, 32, 24) };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.Children.Add(heading);
        Grid.SetRow(body, 1);
        page.Children.Add(body);
        Content = page;

        list.SelectionChanged += (_, _) => SwitchSelection();
        autoBase.Toggled += (_, _) => CommitForm(true, true);
        saturationEnabled.Toggled += (_, _) => CommitForm(false, true);
        focusResolution.Toggled += (_, _) => CommitForm(false, true);
        Display.Monitors.SelectionChanged += (_, _) => CommitForm(true, true);
        Display.BaseResolution.SelectionChanged += (_, _) => CommitForm(false, true);
        Display.GameResolution.SelectionChanged += (_, _) => CommitForm(false, true);
        Display.BaseHz.SelectionChanged += (_, _) =>
        {
            if (IsQuiet || !shell.Ready || EditorProfile() is not GameProfile profile)
                return;
            Display.Populate(Controls.Value(Display.BaseResolution) as DisplayMode, Controls.Value(Display.GameResolution) as DisplayMode, profile, autoBase.IsOn);
            CommitForm(false, true);
        };
        saturation.ValueChanged += (_, _) =>
        {
            saturationText.Text = Math.Round(saturation.Value) + "%";
            if (IsQuiet || !shell.Ready)
                return;
            saturationTimer.Stop();
            saturationTimer.Start();
        };
        saturationTimer.Tick += (_, _) =>
        {
            saturationTimer.Stop();
            CommitForm(false, true);
        };
        add.Click += async (_, _) => await shell.EditGameAsync(false);
        edit.Click += async (_, _) => await shell.EditGameAsync(true);
        remove.Click += async (_, _) => await shell.RemoveGameAsync();
        play.Click += (_, _) =>
        {
            if (!CommitForm(false, true) || EditorProfile() is not GameProfile game)
                return;
            MonitoringController.Launch(game);
            ShowStatus("Launching " + game.Name + ". Waiting for a confirmed connection.", InfoBarSeverity.Informational);
        };
        test.Click += (_, _) =>
        {
            if (!CommitForm(false, false) || EditorProfile() is not GameProfile game)
                return;
            try
            {
                shell.TestGame(game);
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, InfoBarSeverity.Error);
            }
        };
        refresh.Click += (_, _) =>
        {
            try
            {
                shell.ReloadModes(true);
                ShowStatus("Display modes refreshed at the baseline refresh rate.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, InfoBarSeverity.Error);
            }
        };
        SizeChanged += (_, e) => Adapt(e.NewSize.Width);
        Adapt(900);
    }

    public DisplayBrowser Display { get; }

    public bool AutoBaseOn => autoBase.IsOn;

    public void Bind(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        saturationTimer.Stop();
        SetQuiet(true);
        try
        {
            list.Items.Clear();
            if (settings.Games.Count == 0)
            {
                selectedId = "";
                ShowEmpty();
            }
            else
            {
                selectedId = settings.Games.Any(game => game.Id == settings.SelectedGameId)
                    ? settings.SelectedGameId
                    : settings.Games[0].Id;
                foreach (GameProfile game in settings.Games)
                {
                    ListViewItem item = new()
                    {
                        Tag = game.Id,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Content = Row(game)
                    };
                    list.Items.Add(item);
                    if (game.Id == selectedId)
                        list.SelectedItem = item;
                }

                if (EditorProfile() is GameProfile editor)
                    LoadEditors(editor);
            }
        }
        finally
        {
            SetQuiet(false);
        }

        UpdateChrome();
    }

    public GameProfile? EditorProfile()
    {
        if (selectedId.Length == 0)
            return null;
        return shell.Settings.Games.FirstOrDefault(game => game.Id == selectedId);
    }

    public AppSettings Capture(bool requireModes)
    {
        if (shell.ConfigError != null)
            throw new InvalidOperationException("Reset or repair your saved settings before editing a profile.");
        AppSettings next = shell.Settings.Copy();
        if (EditorProfile() == null)
        {
            if (requireModes)
                throw new InvalidOperationException("Add a game before editing a profile.");
            return next;
        }

        GameProfile game = next.Games.First(item => item.Id == selectedId);
        DisplayMode? selected = Controls.Value(Display.GameResolution) as DisplayMode;
        DisplayMode? baselineMode = Controls.Value(Display.BaseResolution) as DisplayMode;
        if (Controls.Value(Display.Monitors) is MonitorInfo selectedMonitor)
            game.MonitorDevice = selectedMonitor.Device;
        if (requireModes && (selected == null || (!autoBase.IsOn && baselineMode == null)))
            throw new InvalidOperationException("Choose a validated baseline and game resolution.");
        if (selected != null)
        {
            game.GameWidth = selected.Width;
            game.GameHeight = selected.Height;
        }

        game.SaturationEnabled = saturationEnabled.IsOn;
        game.Saturation = saturation.Value / 100;
        game.ResolutionOnFocusOnly = focusResolution.IsOn;
        game.AutoBase = autoBase.IsOn;
        if (baselineMode != null)
        {
            game.BaseWidth = baselineMode.Width;
            game.BaseHeight = baselineMode.Height;
        }

        game.BaseRefresh = game.AutoBase ? 0 : Controls.Value(Display.BaseHz) is int hz ? hz : game.BaseRefresh;
        next.SelectedGameId = selectedId;
        next.Validate();
        return next;
    }

    public void RefreshIcons()
    {
        RefreshRows();
    }

    public void ShowStatus(string text, InfoBarSeverity severity)
    {
        status.Severity = severity;
        status.Message = text;
        status.IsOpen = !string.IsNullOrWhiteSpace(text);
    }

    public void UpdateChrome()
    {
        saturationText.Text = Math.Round(saturation.Value) + "%";
        saturation.IsEnabled = saturationEnabled.IsOn;
        baseline.Visibility = autoBase.IsOn ? Visibility.Collapsed : Visibility.Visible;
        bool configOk = shell.ConfigError == null;
        bool dialog = shell.DialogOpen;
        bool hasGame = configOk && EditorProfile() != null;
        detailScroll.IsEnabled = hasGame && !dialog;
        list.IsEnabled = configOk && !dialog;
        add.IsEnabled = configOk && !dialog;
        edit.IsEnabled = hasGame && !dialog;
        remove.IsEnabled = configOk && !dialog && shell.Settings.Games.Count > 0;
        bool modesReady = Display.GameResolution.SelectedItem != null && (autoBase.IsOn || Display.BaseResolution.SelectedItem != null);
        refresh.IsEnabled = hasGame && !dialog;
        play.IsEnabled = test.IsEnabled = hasGame && modesReady && !dialog;
        overlap.IsOpen = configOk && shell.Settings.UniversesOverlap();
        active.IsOpen = configOk && selectedId.Length > 0 && shell.IsProfileActive(selectedId);
        GameProfile? game = EditorProfile();
        if (game != null)
        {
            detailTitle.Text = game.Name;
            placeHint.Text = string.IsNullOrWhiteSpace(game.UniverseIds)
                ? "Universe ID: not saved yet. Edit this game while online."
                : "Universe ID: " + game.UniverseIds;
        }
        else
        {
            ShowEmpty();
        }
    }

    private bool IsQuiet => quiet > 0;

    private void SetQuiet(bool value)
    {
        if (value)
            quiet++;
        else if (quiet > 0)
            quiet--;
    }

    private void ShowEmpty()
    {
        detailTitle.Text = "No games";
        placeHint.Text = "Add a game to save a profile.";
    }

    private void LoadEditors(GameProfile game)
    {
        SetQuiet(true);
        try
        {
            detailTitle.Text = game.Name;
            autoBase.IsOn = game.AutoBase;
            saturationEnabled.IsOn = game.SaturationEnabled;
            saturation.Value = Math.Round(game.Saturation * 100);
            focusResolution.IsOn = game.ResolutionOnFocusOnly;
        }
        finally
        {
            SetQuiet(false);
        }
    }

    private bool CommitForm(bool refreshModes, bool apply)
    {
        if (IsQuiet || !shell.Ready || EditorProfile() == null)
            return false;
        SetQuiet(true);
        try
        {
            if (refreshModes)
                shell.ReloadModes(false);
            AppSettings next = Capture(false);
            GameProfile game = next.Games.First(item => item.Id == selectedId);
            if (apply && shell.IsProfileActive(game.Id))
                shell.EnsureModes(game);
            ProfileRepository.Save(next);
            shell.ReplaceSettings(next);
            RefreshRows();
            if (!apply)
                return true;
            try
            {
                shell.ApplyProfile(game);
                return true;
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, InfoBarSeverity.Error);
                shell.ReportSessionFailure(ex);
                return false;
            }
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, InfoBarSeverity.Error);
            try
            {
                shell.ReloadModes(false);
            }
            catch (Exception reload)
            {
                Store.Log(reload.ToString());
            }

            return false;
        }
        finally
        {
            SetQuiet(false);
            UpdateChrome();
        }
    }

    private void SwitchSelection()
    {
        if (IsQuiet || !shell.Ready)
            return;
        if (list.SelectedItem is not ListViewItem item || item.Tag is not string id || id == selectedId)
            return;
        string previous = selectedId;
        if (!CommitForm(false, true))
        {
            Reselect(previous);
            return;
        }

        selectedId = id;
        if (EditorProfile() is GameProfile chosen)
            LoadEditors(chosen);
        try
        {
            AppSettings next = shell.Settings.Copy();
            next.SelectedGameId = id;
            ProfileRepository.Save(next);
            shell.ReplaceSettings(next);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, InfoBarSeverity.Error);
        }

        try
        {
            shell.ReloadModes(false);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, InfoBarSeverity.Error);
        }

        UpdateChrome();
    }

    private void Reselect(string id)
    {
        SetQuiet(true);
        try
        {
            foreach (object entry in list.Items)
            {
                if (entry is ListViewItem item && item.Tag as string == id)
                    list.SelectedItem = item;
            }
        }
        finally
        {
            SetQuiet(false);
        }
    }

    private void RefreshRows()
    {
        foreach (object entry in list.Items)
        {
            if (entry is ListViewItem item && item.Tag is string id)
            {
                GameProfile? game = shell.Settings.Games.FirstOrDefault(candidate => candidate.Id == id);
                if (game != null)
                    item.Content = Row(game);
            }
        }
    }

    private void Adapt(double width)
    {
        bool next = width >= 760;
        if (next == wide && body.ColumnDefinitions.Count > 0)
            return;
        wide = next;
        body.ColumnDefinitions.Clear();
        body.RowDefinitions.Clear();
        if (wide)
        {
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(listCard, 0);
            Grid.SetRow(listCard, 0);
            Grid.SetColumn(detailScroll, 1);
            Grid.SetRow(detailScroll, 0);
            detailScroll.Margin = new Thickness(0);
            list.MaxHeight = double.PositiveInfinity;
        }
        else
        {
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(listCard, 0);
            Grid.SetRow(listCard, 0);
            Grid.SetColumn(detailScroll, 0);
            Grid.SetRow(detailScroll, 1);
            detailScroll.Margin = new Thickness(0, 4, 0, 0);
            list.MaxHeight = 220;
        }
    }

    private void MatchRefreshButton()
    {
        double height = Display.Monitors.ActualHeight;
        if (height <= 0)
            return;
        if (Math.Abs(refresh.Height - height) >= 0.5)
            refresh.Height = height;
        if (Math.Abs(refresh.Width - height) >= 0.5)
            refresh.Width = height;
    }

    private static void KeepOnOneLine(Button button)
    {
        if (button.Content is TextBlock text)
            text.TextWrapping = TextWrapping.NoWrap;
    }

    private static Grid Row(GameProfile game)
    {
        Grid row = new() { ColumnSpacing = 10, Padding = new Thickness(2, 4, 2, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Image icon = new() { Width = 32, Height = 32, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
        string path = GameIcon.PathFor(game.Id);
        if (File.Exists(path))
        {
            try
            {
                icon.Source = new BitmapImage(new Uri(Path.GetFullPath(path)));
            }
            catch (Exception ex)
            {
                Store.Log("Loading icon for " + game.Name + ": " + ex.Message);
            }
        }

        row.Children.Add(icon);
        StackPanel labels = new() { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock
        {
            Text = game.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Theme.Brush(Theme.Foreground)
        });
        labels.Children.Add(Controls.Note(game.GameWidth + " × " + game.GameHeight));
        Grid.SetColumn(labels, 1);
        row.Children.Add(labels);
        return row;
    }
}
