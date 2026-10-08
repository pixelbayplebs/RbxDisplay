using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace RbxDisplay;

internal static class ProfileRepository
{
    public static string ConfigPath => Path.Combine(Store.AppRoot, "settings.json");

    public static AppSettings Load()
    {
        return Load(ConfigPath);
    }

    internal static AppSettings Load(string configPath)
    {
        if (File.Exists(configPath))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("SchemaVersion", out JsonElement schemaElement) ||
                !schemaElement.TryGetInt32(out int schema))
                throw new InvalidDataException("The RbxDisplay settings file is invalid. The file has been left unchanged.");
            if (schema != 2 && schema != 3)
                throw new InvalidDataException("This settings version is not supported. The file has been left unchanged.");
            if (!root.TryGetProperty("Games", out _) || !root.TryGetProperty("SelectedGameId", out _))
                throw new InvalidDataException("The RbxDisplay settings file is invalid. The file has been left unchanged.");

            AppSettings saved = Store.Read<AppSettings>(configPath);
            if (schema == 2)
                ApplySchema2Display(root, saved);
            FillKnownUniverses(saved);
            saved.Validate();
            if (schema == 2)
                Store.Save(configPath, saved);
            return saved;
        }

        AppSettings settings = new();
        FillKnownUniverses(settings);
        settings.Validate();
        return settings;
    }

    private static void ApplySchema2Display(JsonElement root, AppSettings settings)
    {
        settings.SchemaVersion = 3;
        ApplyDisplay(settings, Text(root, "MonitorDevice"), Bool(root, "AutoBase", true), Int(root, "BaseWidth", 3440), Int(root, "BaseHeight", 1440), Int(root, "BaseRefresh", 0));
    }

    private static void ApplyDisplay(AppSettings settings, string monitor, bool autoBase, int baseWidth, int baseHeight, int baseRefresh)
    {
        foreach (GameProfile game in settings.Games)
        {
            game.MonitorDevice = monitor;
            game.AutoBase = autoBase;
            game.BaseWidth = baseWidth;
            game.BaseHeight = baseHeight;
            game.BaseRefresh = baseRefresh;
        }
    }

    private static string Text(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    }

    private static bool Bool(JsonElement root, string name, bool fallback)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
            return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    private static int Int(JsonElement root, string name, int fallback)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int number) ? number : fallback;
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
