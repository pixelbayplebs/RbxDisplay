using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Stretcher;

internal static class ProfileRepository
{
    public static string ConfigPath => Path.Combine(Store.AppRoot, "settings.json");

    public static AppSettings Load(out bool migrated)
    {
        return Load(ConfigPath, Store.LegacyConfigPath, out migrated);
    }

    internal static AppSettings Load(string configPath, string legacyPath, out bool migrated)
    {
        migrated = false;
        if (File.Exists(configPath))
        {
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath)))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("SchemaVersion", out _) ||
                    !document.RootElement.TryGetProperty("Games", out _) || !document.RootElement.TryGetProperty("SelectedGameId", out _))
                    throw new InvalidDataException("The Stretcher settings file is invalid. The file has been left unchanged.");
            }

            AppSettings saved = Store.Read<AppSettings>(configPath);
            FillKnownUniverses(saved);
            saved.Validate();
            return saved;
        }

        AppSettings settings = new();
        if (File.Exists(legacyPath))
        {
            Settings old = Store.Read<Settings>(legacyPath);
            old.Validate();
            settings.MonitorDevice = old.MonitorDevice;
            settings.AutoBase = old.AutoBase;
            settings.BaseWidth = old.BaseWidth;
            settings.BaseHeight = old.BaseHeight;
            settings.BaseRefresh = old.BaseRefresh;
            settings.LogsFolder = old.LogsFolder;
            settings.PollMilliseconds = old.PollMilliseconds;
            GameProfile imported = new()
            {
                Name = "Imported game",
                PlaceIds = PlaceInput.Normalize(old.PlaceIds),
                GameWidth = old.GameWidth,
                GameHeight = old.GameHeight,
                SaturationEnabled = old.SaturationEnabled,
                Saturation = old.Saturation,
                ResolutionOnFocusOnly = old.ResolutionOnFocusOnly
            };
            if (old.Places().Contains(GameProfile.BloxStrikePlaceId))
            {
                imported.Id = "bloxstrike";
                imported.Name = "BloxStrike";
                settings.Games[0] = imported;
            }
            else
            {
                settings.Games.Add(imported);
            }

            settings.SelectedGameId = imported.Id;
            migrated = true;
        }

        FillKnownUniverses(settings);
        settings.Validate();
        return settings;
    }

    private static void FillKnownUniverses(AppSettings settings)
    {
        if (settings.Games == null)
            return;
        string bloxStrikePlace = GameProfile.BloxStrikePlaceId.ToString(CultureInfo.InvariantCulture);
        string bloxStrikeUniverse = GameProfile.BloxStrikeUniverseId.ToString(CultureInfo.InvariantCulture);
        foreach (GameProfile game in settings.Games)
        {
            if (game == null || !string.IsNullOrWhiteSpace(game.UniverseIds))
                continue;
            try
            {
                if (PlaceInput.Normalize(game.PlaceIds) == bloxStrikePlace)
                    game.UniverseIds = bloxStrikeUniverse;
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    public static void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        Store.Save(ConfigPath, settings);
    }
}
