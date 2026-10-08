using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RbxDisplay;

internal sealed class SettingsPage : UserControl
{
    private readonly MainWindow shell;
    private readonly ToggleSwitch monitoring = Controls.Toggle("Monitoring");
    private readonly TextBlock emergencyValue = Controls.Note("Ctrl + Alt + F12");
    private readonly TextBlock monitorValue = Controls.Note("Not assigned");
    private readonly InfoBar hotkey = Controls.Banner(InfoBarSeverity.Informational);
    private readonly InfoBar status = Controls.Banner(InfoBarSeverity.Informational);
    private readonly Button hide = Controls.ActionButton("Minimize to tray");
    private bool quiet;

    public SettingsPage(MainWindow owner)
    {
        shell = owner;
        monitoring.IsOn = true;
        monitoring.Toggled += (_, _) =>
        {
            if (!quiet)
                shell.SetMonitoring(monitoring.IsOn);
        };
        hide.Click += (_, _) => shell.HideToTray();
        Button resetButton = Controls.ActionButton("Reset settings");
        resetButton.Click += async (_, _) => await shell.ResetSettingsAsync();

        StackPanel monitorCard = new() { Spacing = 8 };
        monitorCard.Children.Add(monitoring);

        StackPanel shortcuts = new() { Spacing = 12 };
        shortcuts.Children.Add(Shortcut("Emergency restore", emergencyValue, true));
        shortcuts.Children.Add(Shortcut("Toggle monitoring", monitorValue, false));
        shortcuts.Children.Add(hotkey);

        StackPanel window = new() { Spacing = 10 };
        window.Children.Add(Controls.Note("Hide RbxDisplay and keep monitoring. Open it again from the tray icon."));
        window.Children.Add(hide);

        StackPanel storage = new() { Spacing = 10 };
        storage.Children.Add(Controls.Note("Settings"));
        storage.Children.Add(PathBox(ProfileRepository.ConfigPath));
        storage.Children.Add(Controls.Note("Diagnostics"));
        storage.Children.Add(PathBox(Path.Combine(Store.AppRoot, "diagnostic.log")));
        storage.Children.Add(Controls.Note("Reset keeps a backup of the current settings file, then starts again from the BloxStrike preset."));
        storage.Children.Add(resetButton);

        StackPanel body = new()
        {
            Spacing = 16,
            Children =
            {
                Controls.Card(monitorCard),
                Controls.Card(shortcuts),
                Controls.Card(window),
                Controls.Card(storage),
                status
            }
        };
        ScrollViewer scroll = new()
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = body
        };
        Grid page = new() { RowSpacing = 16, Margin = new Thickness(32, 12, 32, 24) };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.Children.Add(Controls.Heading("Settings", "Monitoring starts with RbxDisplay. These options apply to the whole app."));
        Grid.SetRow(scroll, 1);
        page.Children.Add(scroll);
        Content = page;
    }

    public void SetMonitoring(bool on)
    {
        if (monitoring.IsOn == on)
            return;
        quiet = true;
        monitoring.IsOn = on;
        quiet = false;
    }

    public void ShowShortcuts(AppSettings settings, bool emergencyReady, bool monitorReady)
    {
        ArgumentNullException.ThrowIfNull(settings);
        emergencyValue.Text = HotkeyText.Describe(settings.EmergencyModifiers, settings.EmergencyKey);
        monitorValue.Text = HotkeyText.Describe(settings.MonitorModifiers, settings.MonitorKey);
        if (settings.EmergencyKey != 0 && !emergencyReady)
        {
            hotkey.Severity = InfoBarSeverity.Warning;
            hotkey.Message = "Emergency restore is already in use by another program. Restore display remains available from the tray icon.";
            hotkey.IsOpen = true;
        }
        else if (settings.MonitorKey != 0 && !monitorReady)
        {
            hotkey.Severity = InfoBarSeverity.Warning;
            hotkey.Message = "The monitoring shortcut is already in use by another program.";
            hotkey.IsOpen = true;
        }
        else
        {
            hotkey.IsOpen = false;
        }
    }

    public void ShowCapture(bool emergency)
    {
        TextBlock target = emergency ? emergencyValue : monitorValue;
        target.Text = "Press a shortcut…";
    }

    public void SetTrayAvailable(bool available)
    {
        hide.IsEnabled = available;
    }

    public void ShowStatus(string text, InfoBarSeverity severity)
    {
        status.Severity = severity;
        status.Message = text;
        status.IsOpen = !string.IsNullOrWhiteSpace(text);
    }

    private Grid Shortcut(string title, TextBlock value, bool emergency)
    {
        TextBlock heading = new()
        {
            Text = title,
            FontSize = 15,
            Foreground = Theme.Brush(Theme.Foreground)
        };
        StackPanel labels = new() { Spacing = 2 };
        labels.Children.Add(heading);
        labels.Children.Add(value);
        Button set = Plain("Set shortcut");
        Button clear = Plain("Clear");
        set.Click += (_, _) => shell.BeginShortcutCapture(emergency);
        clear.Click += (_, _) => shell.ClearShortcut(emergency);
        Grid row = new() { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(labels);
        Grid.SetColumn(set, 1);
        Grid.SetColumn(clear, 2);
        row.Children.Add(set);
        row.Children.Add(clear);
        return row;
    }

    private static Button Plain(string text)
    {
        return new Button
        {
            Content = text,
            MinWidth = 96,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static TextBox PathBox(string path)
    {
        return new TextBox
        {
            Text = path,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            IsSpellCheckEnabled = false
        };
    }
}
