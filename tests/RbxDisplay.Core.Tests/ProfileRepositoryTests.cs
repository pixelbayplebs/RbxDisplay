using System.IO;
using Xunit;

namespace RbxDisplay;

public sealed class ProfileRepositoryTests
{
    [Fact(DisplayName = "first run creates defaults in memory without changing any file")]
    public void FirstRunStaysInMemory()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Foreign, "foreign-settings");
            AppSettings loaded = ProfileRepository.Load(dir.Config);
            Assert.Single(loaded.Games);
            Assert.Equal("BloxStrike", loaded.SelectedGame().Name);
            Assert.False(File.Exists(dir.Config));
            Assert.Equal("foreign-settings", File.ReadAllText(dir.Foreign));
        }
    }

    [Fact(DisplayName = "profile list and selection survive save and restart")]
    public void SavedSelectionSurvivesRestart()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            AppSettings loaded = new();
            loaded.Games.Add(new GameProfile
            {
                Id = "custom",
                Name = "Custom",
                PlaceIds = "123,456",
                GameWidth = 1600,
                GameHeight = 1200,
                MonitorDevice = "display-A",
                AutoBase = false,
                BaseRefresh = 180
            });
            loaded.Games[0].MonitorDevice = "display-A";
            loaded.Games[0].AutoBase = false;
            loaded.Games[0].BaseRefresh = 180;
            loaded.SelectedGameId = "custom";
            Store.Save(dir.Config, loaded);
            AppSettings roundTrip = ProfileRepository.Load(dir.Config);
            Assert.Equal(loaded.SelectedGameId, roundTrip.SelectedGameId);
            Assert.Equal(2, roundTrip.Games.Count);
            Assert.Equal("123,456", roundTrip.SelectedGame().PlaceIds);
            Assert.Empty(roundTrip.Runtime(roundTrip.SelectedGame()).Targets());
            Assert.Equal(180, roundTrip.SelectedGame().BaseRefresh);
            Assert.Equal("display-A", roundTrip.Games[0].MonitorDevice);
            Assert.Equal(180, roundTrip.Games[0].BaseRefresh);
            Assert.Equal(GameProfile.BloxStrikeUniverseId.ToString(System.Globalization.CultureInfo.InvariantCulture), roundTrip.Games[0].UniverseIds);
        }
    }

    [Fact(DisplayName = "a foreign settings file is left unread")]
    public void ForeignSettingsFileIsLeftUnread()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Foreign, "invalid legacy");
            AppSettings loaded = ProfileRepository.Load(dir.Config);
            Assert.Single(loaded.Games);
            Assert.Equal("BloxStrike", loaded.SelectedGame().Name);
            Assert.False(File.Exists(dir.Config));
            Assert.Equal("invalid legacy", File.ReadAllText(dir.Foreign));

            Store.Save(dir.Config, loaded);
            File.WriteAllText(dir.Foreign, "still-foreign");
            AppSettings current = ProfileRepository.Load(dir.Config);
            Assert.Single(current.Games);
            Assert.Equal("still-foreign", File.ReadAllText(dir.Foreign));
        }
    }

    [Fact(DisplayName = "unknown saved version rejected")]
    public void UnknownSavedVersionIsRejectedAndLeftInPlace()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Config, "{\"SchemaVersion\":99}");
            string broken = File.ReadAllText(dir.Config);
            Assert.Throws<InvalidDataException>(() => ProfileRepository.Load(dir.Config));
            Assert.Equal(broken, File.ReadAllText(dir.Config));
        }
    }

    [Fact(DisplayName = "missing schema rejected")]
    public void MissingSchemaIsRejected()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Config, "{}");
            Assert.Throws<InvalidDataException>(() => ProfileRepository.Load(dir.Config));
        }
    }

    [Fact(DisplayName = "incomplete profile settings are not replaced with defaults")]
    public void IncompleteSettingsAreRejected()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Config, "{\"SchemaVersion\":2}");
            Assert.Throws<InvalidDataException>(() => ProfileRepository.Load(dir.Config));
        }
    }

    [Fact(DisplayName = "truncated JSON rejected")]
    public void TruncatedJsonIsRejected()
    {
        using (ConfigDir dir = new ConfigDir())
        {
            File.WriteAllText(dir.Config, "{");
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => ProfileRepository.Load(dir.Config));
        }
    }

    private sealed class ConfigDir : System.IDisposable
    {
        public ConfigDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "RbxDisplay-profiles-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string Config => Path.Combine(Root, "settings.json");
        public string Foreign => Path.Combine(Root, "config.json");

        public void Dispose()
        {
            Directory.Delete(Root, true);
        }
    }
}
