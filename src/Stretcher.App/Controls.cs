using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Stretcher;

internal static class Controls
{
    public static TextBlock Note(string value)
    {
        return new TextBlock
        {
            Text = value,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Theme.Brush(Theme.Foreground),
            Opacity = 0.8
        };
    }

    public static StackPanel Section(string label)
    {
        StackPanel panel = new() { Spacing = 10 };
        TextBlock heading = Note(label);
        heading.FontSize = 12;
        heading.CharacterSpacing = 90;
        panel.Children.Add(heading);
        return panel;
    }

    public static Border Card(StackPanel child)
    {
        return new Border
        {
            Child = child,
            Background = Theme.Brush(Theme.Card),
            BorderBrush = Theme.Brush(Theme.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16)
        };
    }

    public static Button ActionButton(string text)
    {
        return new Button
        {
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 38,
            Padding = new Thickness(10, 7, 10, 7),
            Foreground = Theme.Brush(Theme.Foreground)
        };
    }

    public static CheckBox Check(string text)
    {
        return new CheckBox
        {
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Foreground = Theme.Brush(Theme.Foreground)
        };
    }

    public static ComboBox Picker(string name)
    {
        ComboBox combo = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0,
            MaxDropDownHeight = 320,
            Foreground = Theme.Brush(Theme.Foreground),
            PlaceholderText = "Choose an option"
        };
        AutomationProperties.SetName(combo, name);
        return combo;
    }

    public static void ConfigureGrid(Grid grid, int columns, FrameworkElement[] controls)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < (controls.Length + columns - 1) / columns; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < controls.Length; i++)
        {
            Grid.SetColumn(controls[i], i % columns);
            Grid.SetRow(controls[i], i / columns);
            grid.Children.Add(controls[i]);
        }
    }

    public static object? Value(ComboBox picker)
    {
        return (picker.SelectedItem as ComboBoxItem)?.Tag;
    }

    public static void Fill<T>(ComboBox picker, IEnumerable<T> values, Func<T, string> text, Func<T, bool> selected)
    {
        picker.Items.Clear();
        foreach (T value in values)
        {
            ComboBoxItem item = new() { Content = text(value), Tag = value, Foreground = Theme.Brush(Theme.Foreground) };
            picker.Items.Add(item);
            if (selected(value))
                picker.SelectedItem = item;
        }
    }
}
