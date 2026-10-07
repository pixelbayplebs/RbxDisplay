using System.IO;
using Xunit;

namespace Stretcher;

public sealed class ProfileRepositoryTests
{
    [Fact(DisplayName = "first run creates defaults in memory without changing any file")]
    public void FirstRunStaysInMemory()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            AppSettings loaded = ProfileRepository.Load(dir.Config, dir.Legacy, out bool migrated);
            Assert.False(migrated);
            Assert.Single(loaded.Games);
            Assert.False(File.Exists(dir.Config));
        }
    }

    [Fact(DisplayName = "legacy BloxStrike settings migrate into default preset")]
    public void LegacyBloxStrikeMigratesIntoThePreset()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            Settings old = LegacyBloxStrike();
            Store.Save(dir.Legacy, old);
            string original = File.ReadAllText(dir.Legacy);
            AppSettings loaded = ProfileRepository.Load(dir.Config, dir.Legacy, out bool migrated);
            Assert.True(migrated);
            Assert.Single(loaded.Games);
            Assert.Equal("BloxStrike", loaded.SelectedGame().Name);
            Assert.Equal(GameProfile.BloxStrikeUniverseId.ToString(System.Globalization.CultureInfo.InvariantCulture), loaded.SelectedGame().UniverseIds);
            Assert.Equal(1280, loaded.SelectedGame().GameWidth);
            Assert.Equal(1.5, loaded.SelectedGame().Saturation);
            Assert.Equal(180, loaded.BaseRefresh);
            Assert.Equal("display-A", loaded.MonitorDevice);
            Assert.Equal(original, File.ReadAllText(dir.Legacy));
            Assert.False(File.Exists(dir.Config));
        }
    }

    [Fact(DisplayName = "custom legacy target becomes independent imported game")]
    public void CustomLegacyTargetBecomesAnImportedGame()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            Settings old = LegacyBloxStrike();
            old.PlaceIds = "123,456";
            Store.Save(dir.Legacy, old);
            AppSettings loaded = ProfileRepository.Load(dir.Config, dir.Legacy, out bool migrated);
            Assert.True(migrated);
            Assert.Equal(2, loaded.Games.Count);
            Assert.Equal("Imported game", loaded.SelectedGame().Name);
            Assert.Equal("BloxStrike", loaded.Games[0].Name);
            Assert.Equal("123,456", loaded.SelectedGame().PlaceIds);
            Assert.True(string.IsNullOrEmpty(loaded.SelectedGame().UniverseIds));
        }
    }

    [Fact(DisplayName = "profile list and selection survive save and restart")]
    public void SavedSelectionSurvivesRestart()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            Settings old = LegacyBloxStrike();
            old.PlaceIds = "123,456";
            Store.Save(dir.Legacy, old);
            AppSettings loaded = ProfileRepository.Load(dir.Config, dir.Legacy, out _);
            Store.Save(dir.Config, loaded);
            AppSettings roundTrip = ProfileRepository.Load(dir.Config, dir.Legacy, out bool migrated);
            Assert.False(migrated);
            Assert.Equal(loaded.SelectedGameId, roundTrip.SelectedGameId);
            Assert.Equal(2, roundTrip.Games.Count);
            Assert.Equal("123,456", roundTrip.SelectedGame().PlaceIds);
            Assert.Empty(roundTrip.Runtime(roundTrip.SelectedGame()).Targets());
            Assert.Equal(GameProfile.BloxStrikeUniverseId.ToString(System.Globalization.CultureInfo.InvariantCulture), roundTrip.Games[0].UniverseIds);
        }
    }

    [Fact(DisplayName = "new settings take precedence over old settings")]
    public void NewSettingsWinOverLegacy()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            Settings old = LegacyBloxStrike();
            old.PlaceIds = "123,456";
            Store.Save(dir.Legacy, old);
            AppSettings loaded = ProfileRepository.Load(dir.Config, dir.Legacy, out _);
            Store.Save(dir.Config, loaded);
            File.WriteAllText(dir.Legacy, "invalid legacy");
            AppSettings current = ProfileRepository.Load(dir.Config, dir.Legacy, out bool migrated);
            Assert.False(migrated);
            Assert.Equal(2, current.Games.Count);
        }
    }

    [Fact(DisplayName = "unknown saved version rejected")]
    public void UnknownSavedVersionIsRejectedAndLeftInPlace()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Config, "{\"SchemaVersion\":99}");
            string broken = File.ReadAllText(dir.Config);
            Assert.Throws<InvalidDataException>(() => ProfileRepository.Load(dir.Config, dir.Legacy, out _));
            Assert.Equal(broken, File.ReadAllText(dir.Config));
        }
    }

    [Fact(DisplayName = "missing schema rejected")]
    public void MissingSchemaIsRejected()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Config, "{}");
            Assert.Throws<InvalidDataException>(() => ProfileRepository.Load(dir.Config, dir.Legacy, out _));
        }
    }

    [Fact(DisplayName = "incomplete profile settings are not replaced with defaults")]
    public void IncompleteSettingsAreRejected()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Config, "{\"SchemaVersion\":2}");
            Assert.Throws<InvalidDataException>(() => ProfileRepository.Load(dir.Config, dir.Legacy, out _));
        }
    }

    [Fact(DisplayName = "truncated JSON rejected")]
    public void TruncatedJsonIsRejected()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Config, "{");
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => ProfileRepository.Load(dir.Config, dir.Legacy, out _));
        }
    }

    private static Settings LegacyBloxStrike()
    {
        return new Settings
        {
            GameWidth = 1280,
            GameHeight = 960,
            Saturation = 1.5,
            AutoBase = false,
            BaseWidth = 3440,
            BaseHeight = 1440,
            BaseRefresh = 180,
            MonitorDevice = "display-A"
        };
    }

    private sealed class ConfigDir : System.IDisposable
    {
        public ConfigDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "Stretcher-profiles-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string Config => Path.Combine(Root, "settings.json");
        public string Legacy => Path.Combine(Root, "config.json");

        public void Dispose()
        {
            Directory.Delete(Root, true);
        }
    }
}
