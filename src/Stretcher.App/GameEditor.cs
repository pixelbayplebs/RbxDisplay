using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Stretcher;

internal interface IGameHost
{
    bool IsBusy { get; }
    string? ConfigError { get; set; }
    AppSettings Settings { get; }
    XamlRoot XamlRoot { get; }
    AppSettings ReadEditors(bool requireModes);
    void Commit(AppSettings next, string status, bool refreshMonitors);
    void UpdateControls();
    void ReportError(Exception exception);
}

internal sealed class GameEditor
{
    private readonly IGameHost host;

    public GameEditor(IGameHost host)
    {
        this.host = host;
    }

    public bool DialogOpen { get; private set; }

    public async Task EditAsync(bool editing)
    {
        if (DialogOpen || host.IsBusy || host.ConfigError != null)
            return;
        DialogOpen = true;
        host.UpdateControls();
        try
        {
            GameProfile game = editing ? host.Settings.SelectedGame().Copy() : new GameProfile();
            TextBox name = new() { Header = "Game name", Text = game.Name, MaxLength = 60, PlaceholderText = "My game" };
            TextBox places = new()
            {
                Header = "Place ID or Roblox game link",
                Text = game.PlaceIds,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 76,
                PlaceholderText = "https://www.roblox.com/games/123456789/Game"
            };
            TextBlock error = Controls.Note("");
            error.Foreground = Theme.Brush(Theme.Accent);
            StackPanel body = new()
            {
                Spacing = 12,
                Children =
                {
                    name,
                    places,
                    Controls.Note("One Place ID or Roblox game link is enough. Stretcher looks up the Universe ID when you save, so teleports to other places in that experience stay on this profile. Saving requires an internet connection."),
                    error
                }
            };
            ContentDialog dialog = Dialog(editing ? "Edit game" : "Add game", body, "Save game");
            dialog.PrimaryButtonClick += async (_, args) =>
            {
                ContentDialogButtonClickDeferral deferral = args.GetDeferral();
                try
                {
                    game.Name = name.Text.Trim();
                    string normalized = PlaceInput.Normalize(places.Text);
                    bool unchanged = normalized == game.PlaceIds && !string.IsNullOrWhiteSpace(game.UniverseIds);
                    if (!unchanged)
                        game.UniverseIds = await UniverseLookup.ResolveAsync(normalized);
                    game.PlaceIds = normalized;
                    game.Validate();
                    if (host.Settings.Games.Any(item => item.Id != game.Id && string.Equals(item.Name, game.Name, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("A game with this name already exists.");
                }
                catch (Exception ex)
                {
                    error.Text = ex.Message;
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
            AppSettings next = host.ReadEditors(false);
            if (editing)
            {
                GameProfile previous = next.SelectedGame();
                game.GameWidth = previous.GameWidth;
                game.GameHeight = previous.GameHeight;
                game.SaturationEnabled = previous.SaturationEnabled;
                game.Saturation = previous.Saturation;
                game.ResolutionOnFocusOnly = previous.ResolutionOnFocusOnly;
                next.Games[next.Games.FindIndex(item => item.Id == game.Id)] = game;
            }
            else
            {
                next.Games.Add(game);
            }

            next.SelectedGameId = game.Id;
            ProfileRepository.Save(next);
            host.Commit(next, game.Name + " saved to your game list.", false);
        }
        catch (Exception ex)
        {
            host.ReportError(ex);
        }
        finally
        {
            DialogOpen = false;
            host.UpdateControls();
        }
    }

    public async Task RemoveAsync()
    {
        if (DialogOpen || host.IsBusy || host.Settings.Games.Count <= 1 || host.ConfigError != null)
            return;
        DialogOpen = true;
        host.UpdateControls();
        try
        {
            GameProfile selected = host.Settings.SelectedGame();
            if (await Dialog("Remove game", Controls.Note("Remove " + selected.Name + " and its saved profile?"), "Remove").ShowAsync() != ContentDialogResult.Primary)
                return;
            AppSettings next = host.ReadEditors(false);
            next.Games.RemoveAll(game => game.Id == selected.Id);
            next.SelectedGameId = next.Games[0].Id;
            ProfileRepository.Save(next);
            host.Commit(next, "Game removed. Selected " + next.SelectedGame().Name + ".", false);
        }
        catch (Exception ex)
        {
            host.ReportError(ex);
        }
        finally
        {
            DialogOpen = false;
            host.UpdateControls();
        }
    }

    public async Task ResetAsync()
    {
        if (DialogOpen || host.ConfigError == null)
            return;
        DialogOpen = true;
        host.UpdateControls();
        try
        {
            if (await Dialog("Reset settings", Controls.Note("A backup of your existing Stretcher settings will be kept. Start again with the BloxStrike preset?"), "Reset").ShowAsync() != ContentDialogResult.Primary)
                return;
            if (File.Exists(ProfileRepository.ConfigPath))
                File.Copy(ProfileRepository.ConfigPath, ProfileRepository.ConfigPath + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
            AppSettings defaults = new();
            ProfileRepository.Save(defaults);
            host.ConfigError = null;
            host.Commit(defaults, "Settings reset. Choose a supported game resolution before testing.", true);
        }
        catch (Exception ex)
        {
            host.ReportError(ex);
        }
        finally
        {
            DialogOpen = false;
            host.UpdateControls();
        }
    }

    private ContentDialog Dialog(string title, object content, string primary)
    {
        return new ContentDialog
        {
            XamlRoot = host.XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primary,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ElementTheme.Dark,
            Foreground = Theme.Brush(Theme.Foreground)
        };
    }
}
