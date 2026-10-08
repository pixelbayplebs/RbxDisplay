using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Xunit;

namespace RbxDisplay;

public sealed class GameProfileTests
{
    [Fact(DisplayName = "BloxStrike is a preset in the game list")]
    public void DefaultListContainsBloxStrike()
    {
        AppSettings defaults = ValidDefaults();
        Assert.Single(defaults.Games);
        Assert.Equal("BloxStrike", defaults.SelectedGame().Name);
    }

    [Fact(DisplayName = "default preset tracks the BloxStrike universe and launches its place")]
    public void DefaultPresetTracksTheBloxStrikeUniverse()
    {
        AppSettings defaults = ValidDefaults();
        Assert.Equal(GameProfile.BloxStrikeUniverseId, Assert.Single(defaults.Runtime(defaults.SelectedGame()).Targets()));
        Assert.Equal("roblox://placeId=" + GameProfile.BloxStrikePlaceId, defaults.SelectedGame().LaunchUrl());
    }

    [Fact(DisplayName = "Roblox game link extracts exact place")]
    public void GameLinkExtractsThePlaceId()
    {
        Assert.Equal("123456789", PlaceInput.Normalize("https://www.roblox.com/games/123456789/My-Game?x=y"));
    }

    [Fact(DisplayName = "mixed inputs deduplicate without reordering launch target")]
    public void MixedInputDeduplicatesInOrder()
    {
        Assert.Equal("42,456,789", PlaceInput.Normalize("42, https://roblox.com/games/456/Name\n42;789"));
    }

    [Fact(DisplayName = "large Roblox place remains exact")]
    public void LargePlaceIdStaysExact()
    {
        Assert.Equal("114234929420007", PlaceInput.Normalize("114234929420007"));
    }

    [Fact(DisplayName = "spoofed domain rejected")]
    public void SpoofedDomainIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => PlaceInput.Normalize("https://roblox.com.evil.test/games/123/Game"));
    }

    [Fact(DisplayName = "non-game links rejected")]
    public void NonGameLinksAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => PlaceInput.Normalize("https://www.roblox.com/users/123/profile"));
    }

    [Fact(DisplayName = "unrecognized launch links are not partially parsed")]
    public void UnrecognizedLaunchLinksAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => PlaceInput.Normalize("https://www.roblox.com/games/start?placeId=123"));
    }

    [Theory(DisplayName = "invalid place input")]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("123x")]
    [InlineData("9223372036854775808")]
    [InlineData("roblox://placeId=123")]
    [InlineData("https://other.test/games/123")]
    public void InvalidPlaceInputIsRejected(string input)
    {
        Assert.Throws<InvalidOperationException>(() => PlaceInput.Normalize(input));
    }

    [Fact(DisplayName = "custom games are independent of BloxStrike detection")]
    public void CustomGameDoesNotUseThePresetPlace()
    {
        AppSettings settings = WithCustomGame(out GameProfile custom);
        Settings runtime = settings.Runtime(settings.SelectedGame());
        Assert.Contains(555000111L, runtime.Targets());
        Assert.Contains(555000222L, runtime.Targets());
        Assert.Equal(2, runtime.Targets().Count);
        Assert.DoesNotContain(GameProfile.BloxStrikeUniverseId, runtime.Targets());
        Assert.Equal(custom.Id, settings.SelectedGameId);
    }

    [Fact(DisplayName = "selected game supplies its own display and color settings")]
    public void SelectedGameSuppliesItsOwnSettings()
    {
        AppSettings settings = WithCustomGame(out _);
        Settings runtime = settings.Runtime(settings.SelectedGame());
        Assert.Equal(1600, runtime.GameWidth);
        Assert.Equal(1200, runtime.GameHeight);
        Assert.Equal(1.65, runtime.Saturation);
        Assert.True(runtime.ResolutionOnFocusOnly);
        Assert.Equal("display-B", runtime.MonitorDevice);
        Assert.False(runtime.AutoBase);
        Assert.Equal(2560, runtime.BaseWidth);
        Assert.Equal(1080, runtime.BaseHeight);
        Assert.Equal(120, runtime.BaseRefresh);
    }

    [Fact(DisplayName = "launch uses first explicitly configured place")]
    public void LaunchUsesTheFirstPlace()
    {
        GameProfile custom = Custom();
        custom.Validate();
        Assert.Equal("roblox://placeId=987654321", custom.LaunchUrl());
    }

    [Fact(DisplayName = "switching games does not share profile values")]
    public void SwitchingGamesKeepsIndependentValues()
    {
        AppSettings settings = WithCustomGame(out _);
        Assert.Equal(120, settings.Games[1].BaseRefresh);
        Assert.False(settings.Games[1].AutoBase);
        settings.SelectedGameId = "bloxstrike";
        Assert.Equal(1920, settings.SelectedGame().GameWidth);
        Assert.Equal(1.35, settings.SelectedGame().Saturation);
        Assert.Equal(0, settings.SelectedGame().BaseRefresh);
        Assert.True(settings.SelectedGame().AutoBase);
    }

    [Fact(DisplayName = "editable copies do not mutate saved settings")]
    public void CopiesDoNotMutateTheOriginal()
    {
        AppSettings settings = WithCustomGame(out _);
        settings.SelectedGameId = "bloxstrike";
        AppSettings copy = settings.Copy();
        copy.SelectedGame().GameWidth = 1280;
        copy.Games[1].Name = "Changed";
        Assert.Equal(1920, settings.SelectedGame().GameWidth);
        Assert.Equal("Custom game", settings.Games[1].Name);
    }

    [Fact(DisplayName = "running profile is frozen independently of later edits")]
    public void RuntimeProfileIgnoresLaterEdits()
    {
        AppSettings settings = WithCustomGame(out _);
        Settings runtime = settings.Runtime(settings.SelectedGame());
        settings.Games[1].UniverseIds = "1";
        Assert.Contains(555000111L, runtime.Targets());
    }

    [Fact(DisplayName = "missing selection rejected")]
    public void MissingSelectionIsRejected()
    {
        AppSettings settings = ValidDefaults();
        settings.SelectedGameId = "missing";
        Assert.Throws<InvalidDataException>(settings.Validate);
    }

    [Fact(DisplayName = "null list rejected")]
    public void NullGameListIsRejected()
    {
        AppSettings settings = new AppSettings { Games = null! };
        Assert.Throws<InvalidDataException>(settings.Validate);
    }

    [Fact(DisplayName = "empty list is valid when nothing is selected")]
    public void EmptyGameListIsValid()
    {
        AppSettings settings = ValidDefaults();
        settings.Games.Clear();
        settings.SelectedGameId = "";
        settings.Validate();
        Assert.Empty(settings.Games);
        Assert.Equal("", settings.SelectedGameId);
        Assert.Null(settings.Match(GameProfile.BloxStrikeUniverseId));
    }

    [Fact(DisplayName = "empty list rejects a leftover selection")]
    public void EmptyListRejectsASelection()
    {
        AppSettings settings = ValidDefaults();
        settings.Games.Clear();
        Assert.Throws<InvalidDataException>(settings.Validate);
    }

    [Fact(DisplayName = "a list without BloxStrike is valid")]
    public void ListWithoutBloxStrikeIsValid()
    {
        GameProfile custom = Custom();
        AppSettings settings = new()
        {
            Games = [custom],
            SelectedGameId = custom.Id
        };
        settings.Validate();
        Assert.DoesNotContain(settings.Games, game => game.Id == "bloxstrike");
        Assert.Equal(custom.Id, settings.SelectedGame().Id);
    }

    [Fact(DisplayName = "duplicate names rejected case-insensitively")]
    public void DuplicateNamesAreRejected()
    {
        AppSettings settings = ValidDefaults();
        settings.Games.Add(new GameProfile { Name = "bloxstrike", PlaceIds = "123" });
        Assert.Throws<InvalidDataException>(settings.Validate);
    }

    [Fact(DisplayName = "duplicate profile identifiers rejected")]
    public void DuplicateIdentifiersAreRejected()
    {
        AppSettings settings = ValidDefaults();
        settings.Games.Add(new GameProfile { Id = "bloxstrike", Name = "Other", PlaceIds = "123" });
        Assert.Throws<InvalidDataException>(settings.Validate);
    }

    [Fact(DisplayName = "unknown schema is not reset silently")]
    public void UnknownSchemaIsRejected()
    {
        AppSettings settings = new AppSettings { SchemaVersion = 99 };
        Assert.Throws<InvalidDataException>(settings.Validate);
    }

    [Fact(DisplayName = "per-game nonfinite saturation rejected")]
    public void NonFiniteGameSaturationIsRejected()
    {
        GameProfile game = new GameProfile { Name = "Bad", PlaceIds = "123", Saturation = double.NaN };
        Assert.Throws<InvalidOperationException>(game.Validate);
    }

    [Fact(DisplayName = "literal settings JSON keeps the selected game")]
    public void LiteralSettingsJsonDeserializes()
    {
        using (TempDirectory temp = new TempDirectory("RbxDisplay-profiles-"))
        {
            string config = Path.Combine(temp.Path, "settings.json");
            File.WriteAllText(config, """
                {
                  "SchemaVersion": 2,
                  "SelectedGameId": "custom",
                  "Games": [
                    {
                      "Id": "bloxstrike",
                      "Name": "BloxStrike",
                      "PlaceIds": "114234929420007",
                      "GameWidth": 1920,
                      "GameHeight": 1440,
                      "SaturationEnabled": true,
                      "Saturation": 1.35,
                      "ResolutionOnFocusOnly": false
                    },
                    {
                      "Id": "custom",
                      "Name": "Imported game",
                      "PlaceIds": "123,456",
                      "GameWidth": 1600,
                      "GameHeight": 1200,
                      "SaturationEnabled": true,
                      "Saturation": 1.2,
                      "ResolutionOnFocusOnly": true
                    }
                  ],
                  "MonitorDevice": "display-A",
                  "AutoBase": false,
                  "BaseWidth": 3440,
                  "BaseHeight": 1440,
                  "BaseRefresh": 180,
                  "LogsFolder": "",
                  "PollMilliseconds": 750
                }
                """);
            AppSettings loaded = ProfileRepository.Load(config);
            Assert.Equal(3, loaded.SchemaVersion);
            Assert.Equal("custom", loaded.SelectedGameId);
            Assert.Equal(1600, loaded.SelectedGame().GameWidth);
            Assert.Equal("123,456", loaded.SelectedGame().PlaceIds);
            Assert.Empty(loaded.SelectedGame().UniverseIds);
            Assert.Equal(GameProfile.BloxStrikeUniverseId.ToString(CultureInfo.InvariantCulture), loaded.Games[0].UniverseIds);
            Assert.Equal(1.2, loaded.SelectedGame().Saturation);
            Assert.True(loaded.SelectedGame().ResolutionOnFocusOnly);
            Assert.Equal("display-A", loaded.SelectedGame().MonitorDevice);
            Assert.False(loaded.SelectedGame().AutoBase);
            Assert.Equal(3440, loaded.SelectedGame().BaseWidth);
            Assert.Equal(1440, loaded.SelectedGame().BaseHeight);
            Assert.Equal(180, loaded.SelectedGame().BaseRefresh);
            Assert.Equal("display-A", loaded.Games[0].MonitorDevice);
            Assert.Equal(180, loaded.Games[0].BaseRefresh);
            Assert.Empty(loaded.Runtime(loaded.SelectedGame()).Targets());
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(config));
            Assert.Equal(3, saved.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.False(saved.RootElement.TryGetProperty("MonitorDevice", out _));
            Assert.Equal("display-A", saved.RootElement.GetProperty("Games")[0].GetProperty("MonitorDevice").GetString());
        }
    }

    [Fact(DisplayName = "detection follows the universe id, ignoring the open profile")]
    public void MatchFollowsTheUniverse()
    {
        AppSettings settings = WithCustomGame(out GameProfile custom);
        Assert.Equal("bloxstrike", settings.Match(GameProfile.BloxStrikeUniverseId)!.Id);
        Assert.Equal(custom.Id, settings.Match(555000222L)!.Id);
        Assert.Null(settings.Match(1));
        Assert.Null(settings.Match(null));
        Assert.False(settings.UniversesOverlap());
    }

    [Fact(DisplayName = "the earlier profile wins when universe ids overlap")]
    public void EarlierProfileWinsASharedUniverse()
    {
        AppSettings settings = ValidDefaults();
        settings.Games.Add(new GameProfile
        {
            Name = "Other",
            PlaceIds = "123",
            UniverseIds = GameProfile.BloxStrikeUniverseId.ToString(CultureInfo.InvariantCulture)
        });
        Assert.Equal("bloxstrike", settings.Match(GameProfile.BloxStrikeUniverseId)!.Id);
        Assert.True(settings.UniversesOverlap());
    }

    [Fact(DisplayName = "shortcut defaults survive a copy and a shared shortcut is rejected")]
    public void ShortcutDefaultsStayIndependent()
    {
        AppSettings settings = ValidDefaults();
        Assert.Equal(0x7B, settings.EmergencyKey);
        Assert.Equal(0x3, settings.EmergencyModifiers);
        Assert.Equal(0, settings.MonitorKey);
        AppSettings copy = settings.Copy();
        copy.EmergencyKey = 0x70;
        Assert.Equal(0x7B, settings.EmergencyKey);
        copy.MonitorKey = 0x70;
        copy.MonitorModifiers = copy.EmergencyModifiers;
        Assert.Throws<InvalidOperationException>(copy.Validate);
    }

    [Fact(DisplayName = "monitor labels omit the device path")]
    public void MonitorLabelOmitsTheDevicePath()
    {
        string primary = new MonitorInfo { Device = @"\\.\DISPLAY1", Name = "Dell", Primary = true }.ToString();
        Assert.Equal("Dell", primary);
        Assert.DoesNotContain("primary", primary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\\.\", primary, StringComparison.Ordinal);
        Assert.Equal("Monitor", new MonitorInfo { Device = @"\\.\DISPLAY2" }.ToString());
    }

    [Fact(DisplayName = "monitor labels keep the model name only")]
    public void MonitorLabelKeepsTheModelNameOnly()
    {
        Assert.Equal("Alienware_AW3425DWM", new MonitorInfo
        {
            Device = @"\\.\DISPLAY1",
            Name = "Alienware_AW3425DWM(HDMI1_2.1)",
            Primary = true
        }.ToString());
        Assert.Equal("Alienware_AW3425DWM", MonitorInfo.FriendlyName("Alienware_AW3425DWM(HDMI1_2.1) (primary)"));
        Assert.Equal("Dell", MonitorInfo.FriendlyName("Dell (DisplayPort)"));
        Assert.Equal("Monitor", MonitorInfo.FriendlyName("  "));
        Assert.Equal("(HDMI)", MonitorInfo.FriendlyName("(HDMI)"));
    }

    private static AppSettings ValidDefaults()
    {
        AppSettings defaults = new AppSettings();
        defaults.Validate();
        return defaults;
    }

    private static GameProfile Custom()
    {
        return new GameProfile
        {
            Name = "Custom game",
            PlaceIds = "987654321,123",
            UniverseIds = "555000111,555000222",
            GameWidth = 1600,
            GameHeight = 1200,
            Saturation = 1.65,
            ResolutionOnFocusOnly = true,
            MonitorDevice = "display-B",
            AutoBase = false,
            BaseWidth = 2560,
            BaseHeight = 1080,
            BaseRefresh = 120
        };
    }

    private static AppSettings WithCustomGame(out GameProfile custom)
    {
        AppSettings settings = ValidDefaults();
        custom = Custom();
        custom.Validate();
        settings.Games.Add(custom);
        settings.SelectedGameId = custom.Id;
        settings.Validate();
        return settings;
    }
}
