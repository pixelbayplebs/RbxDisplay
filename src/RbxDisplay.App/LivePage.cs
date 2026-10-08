using System;
using System.IO;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace RbxDisplay;

internal sealed class LivePage : UserControl
{
    private readonly Border badge;
    private readonly TextBlock badgeText;
    private readonly TextBlock title;
    private readonly TextBlock message;
    private readonly TextBlock resolution;
    private readonly TextBlock saturation;
    private readonly TextBlock monitor;
    private readonly TextBlock focus;
    private readonly InfoBar banner;
    private readonly Image icon;
    private string? gameId;

    public LivePage()
    {
        badgeText = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.NoWrap };
        badge = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Child = badgeText
        };
        title = new TextBlock
        {
            FontSize = 32,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Theme.Brush(Theme.Foreground)
        };
        message = Controls.Note("");
        message.FontSize = 15;
        resolution = Value();
        saturation = Value();
        monitor = Value();
        focus = Value();
        banner = Controls.Banner(InfoBarSeverity.Informational);

        icon = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Border iconCard = new()
        {
            Background = Theme.Brush(Theme.Card),
            BorderBrush = Theme.Brush(Theme.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(IconRadius),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = icon
        };
        iconCard.SizeChanged += (_, _) => ClipIcon(iconCard);
        StackPanel card = new() { Spacing = 10 };
        card.Children.Add(badge);
        card.Children.Add(title);
        card.Children.Add(message);
        Border status = Controls.Card(card);
        status.VerticalAlignment = VerticalAlignment.Top;
        Grid statusRow = new() { ColumnSpacing = 16 };
        ColumnDefinition iconColumn = new() { Width = GridLength.Auto };
        statusRow.ColumnDefinitions.Add(iconColumn);
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusRow.Children.Add(iconCard);
        Grid.SetColumn(status, 1);
        statusRow.Children.Add(status);
        status.SizeChanged += (_, _) => FitIconColumn(status, iconCard, iconColumn);

        Grid stats = new() { ColumnSpacing = 12, RowSpacing = 12 };
        stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stats.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stats.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Place(stats, Stat("RESOLUTION", resolution), 0, 0);
        Place(stats, Stat("SATURATION", saturation), 1, 0);
        Place(stats, Stat("MONITOR", monitor), 0, 1);
        Place(stats, Stat("FOCUS", focus), 1, 1);

        StackPanel body = new()
        {
            Spacing = 16,
            Children =
            {
                statusRow,
                stats,
                banner
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
        page.Children.Add(Controls.Heading("Live", "A saved profile applies while that game is in the foreground."));
        Grid.SetRow(scroll, 1);
        page.Children.Add(scroll);
        Content = page;
        Show(new LiveState());
    }

    public void Show(LiveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        badgeText.Text = state.Badge;
        title.Text = state.Title;
        message.Text = state.Message;
        resolution.Text = state.Resolution;
        saturation.Text = state.Saturation;
        monitor.Text = MonitorLabel(state.Monitor);
        focus.Text = state.Focus;
        Color fill;
        Color ink;
        if (state.Tone == LiveTone.Active)
        {
            fill = Color.FromArgb(48, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B);
            ink = Theme.Accent;
        }
        else if (state.Tone == LiveTone.Warning)
        {
            fill = Color.FromArgb(48, 0xFF, 0xC2, 0x4B);
            ink = Color.FromArgb(255, 0xFF, 0xC2, 0x4B);
        }
        else
        {
            fill = Color.FromArgb(28, 0xFF, 0xF7, 0xE7);
            ink = Theme.Foreground;
        }

        badge.Background = Theme.Brush(fill);
        badgeText.Foreground = Theme.Brush(ink);
        string? next = string.IsNullOrWhiteSpace(state.GameId) ? null : state.GameId;
        if (next == gameId)
            return;
        gameId = next;
        ApplyIcon();
    }

    public void RefreshIcon()
    {
        ApplyIcon();
    }

    private void ApplyIcon()
    {
        icon.Source = null;
        if (gameId == null)
            return;
        string path = GameIcon.PathFor(gameId);
        if (!File.Exists(path))
            return;
        try
        {
            icon.Source = new BitmapImage(new Uri(Path.GetFullPath(path)));
        }
        catch (Exception ex)
        {
            Store.Log("Loading live icon for " + gameId + ": " + ex.Message);
        }
    }

    public void ShowIssue(string? text, InfoBarSeverity severity)
    {
        banner.Severity = severity;
        banner.Message = text ?? "";
        banner.IsOpen = !string.IsNullOrWhiteSpace(text);
    }

    private const double IconRadius = 12;

    private static string MonitorLabel(string? device)
    {
        if (string.IsNullOrWhiteSpace(device) || device == "—")
            return "—";
        return Native.Monitor(device)?.ToString() ?? "—";
    }

    private static void ClipIcon(FrameworkElement element)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            return;
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Vector2 corner = new((float)IconRadius);
        visual.Clip = visual.Compositor.CreateRectangleClip(0, 0, (float)element.ActualWidth, (float)element.ActualHeight, corner, corner, corner, corner);
    }

    private static void FitIconColumn(FrameworkElement status, FrameworkElement iconCard, ColumnDefinition iconColumn)
    {
        double side = status.ActualHeight;
        if (side <= 0)
            return;
        if (iconColumn.Width.GridUnitType != GridUnitType.Pixel || Math.Abs(iconColumn.Width.Value - side) >= 0.5)
            iconColumn.Width = new GridLength(side);
        if (double.IsNaN(iconCard.Height) || Math.Abs(iconCard.Height - side) >= 0.5)
            iconCard.Height = side;
    }

    private static TextBlock Value()
    {
        return new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Theme.Brush(Theme.Foreground),
            Text = "—"
        };
    }

    private static Border Stat(string label, TextBlock value)
    {
        TextBlock heading = Controls.Note(label);
        heading.FontSize = 12;
        heading.CharacterSpacing = 80;
        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(heading);
        panel.Children.Add(value);
        return Controls.Card(panel);
    }

    private static void Place(Grid grid, FrameworkElement element, int column, int row)
    {
        Grid.SetColumn(element, column);
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }
}
