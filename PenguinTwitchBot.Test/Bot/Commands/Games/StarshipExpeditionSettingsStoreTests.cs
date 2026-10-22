using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Notifications;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Games;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Migrations.Sqlite;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipExpeditionSettingsStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public StarshipExpeditionSettingsStoreTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection,
                sqlite => sqlite.MigrationsAssembly(typeof(SqliteDesignTimeDbContextFactory).Assembly.GetName().Name)))
            .AddScoped<IUnitOfWork, UnitOfWork>().BuildServiceProvider();
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void LegacySettingsJson_DefaultsGameToDisabled()
    {
        var legacySettings = JsonSerializer.Deserialize<StarshipExpeditionSettings>("{}")!;

        Assert.False(legacySettings.GameEnabled);
        Assert.True(legacySettings.EmergencyReturnSound.Enabled);
        Assert.Equal("/media/starship_emergency_return.wav", legacySettings.EmergencyReturnSound.AudioFile);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesSettingsAndNormalizedCustomSectors()
    {
        var store = CreateStore();
        var settings = new StarshipExpeditionSettings
        {
            GameEnabled = true,
            LegCount = 8,
            MinimumEventIntervalMinutes = 20,
            MaximumEventIntervalMinutes = 35,
            BoardingDurationSeconds = 45,
            ActiveCrewRewardsOnly = false,
            BoardingOpenSound = new() { Enabled = true, AudioFile = "/media/custom_boarding.wav", Volume = 48 },
            EmergencyReturnSound = new() { Enabled = true, AudioFile = "/media/custom_emergency.wav", Volume = 42 },
            EventTypeOpenSounds = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SignalDetected"] = new() { Enabled = true, AudioFile = "/media/custom_type_signal.wav", Volume = 36 }
            },
            ShipSystems = new() { MaximumHull = 90, StartingHull = 80, StartingSupplies = 60, TravelSuppliesCost = 4, StationRepairAmount = 25 },
            AnnounceSessionStatusInChat = false,
            SessionStartChatMessageTemplate = "Boarding {duration}s",
            SessionEndChatMessageTemplate = "Returned {count} in {duration}s",
            RewardProfiles =
            [
                new StarshipRewardProfile
                {
                    EventType = "SalvageSite",
                    Enabled = true,
                    PointTypeId = 7,
                    PointTypeName = "Salvage Credits",
                    SuccessChancePercent = 75,
                    CommonWeightPercent = 60,
                    UncommonWeightPercent = 30,
                    RareWeightPercent = 10,
                    CommonMinimum = 5,
                    CommonMaximum = 12,
                    RiskLossPercent = 25,
                    MaximumRiskLossPerEvent = 40
                }
            ],
            CustomSectors = ["  Delta Reach  ", "Glass Nebula"]
        };
        var signalIndex = settings.EventCatalog.FindIndex(definition => definition.EventType == "SignalDetected");
        settings.EventCatalog[signalIndex] = settings.EventCatalog[signalIndex] with
        {
            OpenSound = new() { Override = true, Enabled = true, AudioFile = "/media/custom_signal.wav", Volume = 33 }
        };

        await store.SaveAsync(settings);
        var loaded = await CreateStore().LoadAsync();

        Assert.Equal(8, loaded.LegCount);
        Assert.True(loaded.GameEnabled);
        Assert.Equal("/media/custom_emergency.wav", loaded.EmergencyReturnSound.AudioFile);
        Assert.Equal(42, loaded.EmergencyReturnSound.Volume);
        Assert.Equal("/media/custom_boarding.wav", loaded.BoardingOpenSound.AudioFile);
        Assert.Equal(48, loaded.BoardingOpenSound.Volume);
        Assert.Equal("/media/custom_type_signal.wav", loaded.GetEventTypeOpenSound("SignalDetected").AudioFile);
        Assert.Equal(36, loaded.GetEventTypeOpenSound("SignalDetected").Volume);
        var signalSound = loaded.EventCatalog.Single(definition => definition.EventType == "SignalDetected").OpenSound;
        Assert.True(signalSound.Override);
        Assert.Equal("/media/custom_signal.wav", signalSound.AudioFile);
        Assert.Equal(33, signalSound.Volume);
        Assert.Equal(20, loaded.MinimumEventIntervalMinutes);
        Assert.Equal(35, loaded.MaximumEventIntervalMinutes);
        Assert.Equal(45, loaded.BoardingDurationSeconds);
        Assert.False(loaded.ActiveCrewRewardsOnly);
        Assert.Equal(80, loaded.ShipSystems.StartingHull);
        Assert.Equal(90, loaded.ShipSystems.MaximumHull);
        Assert.Equal(60, loaded.ShipSystems.StartingSupplies);
        Assert.Equal(4, loaded.ShipSystems.TravelSuppliesCost);
        Assert.Equal(25, loaded.ShipSystems.StationRepairAmount);
        Assert.False(loaded.AnnounceSessionStatusInChat);
        Assert.Equal("Boarding {duration}s", loaded.SessionStartChatMessageTemplate);
        Assert.Equal("Returned {count} in {duration}s", loaded.SessionEndChatMessageTemplate);
        var rewardProfile = loaded.RewardProfiles.Single(profile => profile.EventType == "SalvageSite");
        Assert.Equal("SalvageSite", rewardProfile.EventType);
        Assert.Equal(7, rewardProfile.PointTypeId);
        Assert.Equal(75, rewardProfile.SuccessChancePercent);
        Assert.Equal([60, 30, 10], new[] { rewardProfile.CommonWeightPercent, rewardProfile.UncommonWeightPercent, rewardProfile.RareWeightPercent });
        Assert.Equal(40, rewardProfile.MaximumRiskLossPerEvent);
        Assert.Equal(["Delta Reach", "Glass Nebula"], loaded.CustomSectors);
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = Assert.Single(context.Settings);
        Assert.Equal(Setting.DataTypeEnum.String, saved.DataType);
        Assert.DoesNotContain("\"rewardProfiles\"", saved.StringSetting);
        Assert.DoesNotContain("\"eventCatalog\"", saved.StringSetting);
        Assert.Equal(8, context.StarshipEvents.Count());
        Assert.Equal(4, context.StarshipRewardProfiles.Count());
    }

    [Fact]
    public async Task Load_WhenNoSavedRow_ReturnsDefaults()
    {
        var settings = await CreateStore().LoadAsync();

        Assert.Equal(StarshipExpeditionService.DefaultLegCount, settings.LegCount);
        Assert.Equal(StarshipExpeditionService.DefaultMinimumEventIntervalMinutes, settings.MinimumEventIntervalMinutes);
        Assert.Equal(StarshipExpeditionService.DefaultMaximumEventIntervalMinutes, settings.MaximumEventIntervalMinutes);
        Assert.Equal(StarshipExpeditionService.DefaultBoardingDurationSeconds, settings.BoardingDurationSeconds);
        Assert.Equal(6, settings.LegCount);
        Assert.Equal(80, settings.ShipSystems.StartingHull);
        Assert.Equal(55, settings.ShipSystems.StartingSupplies);
        Assert.Equal(10, settings.ShipSystems.TravelSuppliesCost);
        Assert.Equal(3, settings.StationIntervalLegs);
        Assert.True(settings.ActiveCrewRewardsOnly);
        Assert.True(settings.AnnounceSessionStatusInChat);
        Assert.All(settings.RewardProfiles, profile => Assert.False(profile.Enabled));
        Assert.Empty(settings.CustomSectors);
        Assert.Equal(5, settings.GetEffectiveSectors().Count);
    }

    [Fact]
    public async Task Load_InvalidPersistedShipSystemsResetsOnlyShipDefaultsAndPersistsRepair()
    {
        var store = CreateStore();
        await store.SaveAsync(new StarshipExpeditionSettings
        {
            GameEnabled = true,
            LegCount = 9,
            CustomSectors = ["Delta Reach"]
        });
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var saved = context.Settings.Single(setting => setting.Name == StarshipExpeditionSettingsStore.SettingKey);
            var json = JsonNode.Parse(saved.StringSetting!)!.AsObject();
            json["shipSystems"]!["maximumHull"] = 0;
            saved.StringSetting = json.ToJsonString();
            context.SaveChanges();
        }

        var loaded = await CreateStore().LoadAsync();

        Assert.Equal(80, loaded.ShipSystems.MaximumHull);
        Assert.Equal(80, loaded.ShipSystems.StartingHull);
        Assert.Equal(55, loaded.ShipSystems.StartingSupplies);
        Assert.True(loaded.GameEnabled);
        Assert.Equal(9, loaded.LegCount);
        Assert.Equal(["Delta Reach"], loaded.CustomSectors);
        using var verifyScope = _services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repaired = JsonNode.Parse(verifyContext.Settings.Single(setting => setting.Name == StarshipExpeditionSettingsStore.SettingKey).StringSetting!)!;
        Assert.Equal(80, (int)repaired["shipSystems"]!["maximumHull"]!);
    }

    [Fact]
    public async Task Load_CatalogDocumentWithNullEventTypeRepairsFromRowId()
    {
        await CreateStore().SaveAsync(new StarshipExpeditionSettings { GameEnabled = true });
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = context.StarshipEvents.Single(item => item.Id == "SignalDetected");
            var json = JsonNode.Parse(row.DocumentJson)!.AsObject();
            json["eventType"] = null;
            row.DocumentJson = json.ToJsonString();
            context.SaveChanges();
        }

        var loaded = await CreateStore().LoadAsync();

        Assert.True(loaded.GameEnabled);
        Assert.Contains(loaded.EventCatalog, definition => definition.EventType == "SignalDetected");
        using var verifyScope = _services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repaired = JsonNode.Parse(verifyContext.StarshipEvents.Single(item => item.Id == "SignalDetected").DocumentJson)!;
        Assert.Equal("SignalDetected", (string?)repaired["eventType"]);
    }

    [Fact]
    public async Task Load_CatalogDocumentWithBlankProfileNameRepairsFromRowName()
    {
        var settings = new StarshipExpeditionSettings();
        settings.RewardProfiles.Add(new StarshipRewardProfile
        {
            Id = "custom-profile", Name = "Custom Profile", Tiers = [new() { Name = "Common", Weight = 1 }]
        });
        await CreateStore().SaveAsync(settings);
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = context.StarshipRewardProfiles.Single(item => item.Id == "custom-profile");
            var json = JsonNode.Parse(row.DocumentJson)!.AsObject();
            json["name"] = "";
            row.DocumentJson = json.ToJsonString();
            context.SaveChanges();
        }

        var loaded = await CreateStore().LoadAsync();

        var repaired = loaded.RewardProfiles.Single(profile => profile.Id == "custom-profile");
        Assert.Equal("Custom Profile", repaired.Name);
        Assert.Equal("Common", Assert.Single(repaired.Tiers).Name);
        using var verifyScope = _services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repairedDocument = JsonNode.Parse(verifyContext.StarshipRewardProfiles.Single(item => item.Id == "custom-profile").DocumentJson)!;
        Assert.Equal("Custom Profile", (string?)repairedDocument["name"]);
    }

    [Fact]
    public async Task ResetToDefaults_RemovesCustomCatalogAndRecoversFromDuplicateProfileRows()
    {
        var settings = new StarshipExpeditionSettings { GameEnabled = true, CustomSectors = ["Delta Reach"] };
        settings.EventCatalog.Add(new StarshipEventDefinition("custom-event", "Custom Event", "A custom event.",
            [new() { ChoiceId = "continue", Label = "Continue" }, new() { ChoiceId = "pass", Label = "Pass", EndsWithoutReward = true }], "continue"));
        settings.RewardProfiles.Add(new StarshipRewardProfile
        {
            Id = "custom-profile", Name = "Custom Profile", Tiers = [new() { Name = "Common", Weight = 1 }]
        });
        await CreateStore().SaveAsync(settings);
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = context.StarshipRewardProfiles.Single(item => item.Id == "custom-profile");
            context.StarshipRewardProfiles.Add(new StarshipRewardProfileRow
            {
                Id = "CUSTOM-PROFILE", Name = "Duplicate Profile", Enabled = profile.Enabled,
                Weight = profile.Weight, DocumentJson = profile.DocumentJson.Replace("custom-profile", "CUSTOM-PROFILE", StringComparison.Ordinal)
            });
            context.SaveChanges();
        }

        var reset = await CreateStore().ResetToDefaultsAsync();

        Assert.False(reset.GameEnabled);
        Assert.Equal(6, reset.LegCount);
        Assert.Empty(reset.CustomSectors);
        Assert.DoesNotContain(reset.EventCatalog, definition => definition.EventType == "custom-event");
        Assert.DoesNotContain(reset.RewardProfiles, profile => profile.Id.Contains("custom-profile", StringComparison.OrdinalIgnoreCase));
        using var verifyScope = _services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(reset.EventCatalog.Count, verifyContext.StarshipEvents.Count());
        Assert.Equal(reset.RewardProfiles.Count, verifyContext.StarshipRewardProfiles.Count());
    }

    [Fact]
    public async Task ResetToDefaults_RollsBackCatalogAndSettingsWhenReseedingFails()
    {
        var store = CreateStore();
        await store.SaveAsync(new StarshipExpeditionSettings { GameEnabled = true, LegCount = 11 });
        string originalSettings;
        int originalEventCount;
        int originalProfileCount;
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            originalSettings = context.Settings.Single(item => item.Name == StarshipExpeditionSettingsStore.SettingKey).StringSetting!;
            originalEventCount = context.StarshipEvents.Count();
            originalProfileCount = context.StarshipRewardProfiles.Count();
            context.Database.ExecuteSqlRaw("CREATE TRIGGER fail_starship_event_insert BEFORE INSERT ON starship_events BEGIN SELECT RAISE(ABORT, 'forced reset failure'); END;");
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => CreateStore().ResetToDefaultsAsync());

        using var verifyScope = _services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(originalSettings, verifyContext.Settings.Single(item => item.Name == StarshipExpeditionSettingsStore.SettingKey).StringSetting);
        Assert.Equal(originalEventCount, verifyContext.StarshipEvents.Count());
        Assert.Equal(originalProfileCount, verifyContext.StarshipRewardProfiles.Count());
    }

    [Theory]
    [InlineData("Kepler Reach")]
    [InlineData("My Sector")]
    public void NormalizeAndValidate_RejectsDuplicateSectorNames(string customSector)
    {
        var settings = new StarshipExpeditionSettings { CustomSectors = [customSector, customSector.ToUpperInvariant()] };

        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
    }

    [Fact]
    public void NormalizeAndValidate_RejectsBlankOrOversizedSectorNames()
    {
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(
            new StarshipExpeditionSettings { CustomSectors = ["  "] }));
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(
            new StarshipExpeditionSettings { CustomSectors = [new string('x', StarshipExpeditionSettings.MaximumSectorNameLength + 1)] }));
    }

    [Fact]
    public void NormalizeAndValidate_RejectsInvalidRewardOddsAndRanges()
    {
        var wrongOdds = new StarshipExpeditionSettings
        {
            RewardProfiles = [new StarshipRewardProfile { EventType = "SignalDetected", Enabled = true, PointTypeId = 1, CommonWeightPercent = 50 }]
        };
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(wrongOdds));

        var invertedRange = new StarshipExpeditionSettings
        {
            RewardProfiles = [new StarshipRewardProfile
            {
                EventType = "SignalDetected",
                Enabled = false,
                PointTypeId = 1,
                CommonMinimum = 20,
                CommonMaximum = 10
            }]
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipExpeditionSettings.NormalizeAndValidate(invertedRange));
    }

    [Fact]
    public void NormalizeAndValidate_AllowsLongMaxRewardRangesAndOptionalCaps()
    {
        var settings = new StarshipExpeditionSettings
        {
            RewardProfiles =
            [
                new StarshipRewardProfile
                {
                    Id = "large-reward", EventType = "SignalDetected", Enabled = false,
                    Tiers = [new() { Name = "Common", Weight = 1, Rewards = [new() { PointTypeId = 1, Minimum = 0, Maximum = long.MaxValue }] }],
                    Caps = [new() { PointTypeId = 1, MaximumPerEvent = long.MaxValue, MaximumUnbankedPerVoyage = long.MaxValue }],
                    LossRules = [new() { PointTypeId = 1, Percent = 100, MaximumPerEvent = long.MaxValue }]
                }
            ]
        };

        StarshipExpeditionSettings.NormalizeAndValidate(settings);

        var profile = settings.RewardProfiles.Single(item => item.Id == "large-reward");
        Assert.Equal(long.MaxValue, Assert.Single(Assert.Single(profile.Tiers).Rewards).Maximum);
        Assert.Equal(long.MaxValue, Assert.Single(profile.Caps).MaximumPerEvent);
        Assert.Equal(long.MaxValue, Assert.Single(profile.LossRules).MaximumPerEvent);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesReusableProfilesRelativeWeightsAndActionTiming()
    {
        var settings = new StarshipExpeditionSettings();
        settings.RewardProfiles.Add(new StarshipRewardProfile
        {
            Id = "shared", Name = "Shared recovery", Enabled = true,
            Tiers =
            [
                new() { Id = "ordinary", Name = "Ordinary", Weight = 3, Rewards = [new() { PointTypeId = 2, Minimum = 1, Maximum = 5 }] },
                new() { Id = "legendary", Name = "Legendary", Weight = 1, Color = "#ffcc00", Rewards = [new() { PointTypeId = 3, Minimum = 10, Maximum = 15 }], Actions = [new() { ActionId = 42, Timing = StarshipActionTimings.AfterBank }] }
            ]
        });
        settings.EventRewardProfileIds["SalvageSite"] = "shared";
        settings.EventRewardProfileIds["SignalDetected"] = "shared";
        await CreateStore().SaveAsync(settings);
        var loaded = await CreateStore().LoadAsync();

        var profile = loaded.GetRewardProfile("SalvageSite")!;
        Assert.Same(profile, loaded.GetRewardProfile("SignalDetected"));
        Assert.Equal([75, 25], StarshipRewardTiers.DescribeTiers(profile).Select(tier => tier.ChancePercent));
        Assert.Equal(StarshipActionTimings.AfterBank, profile.Tiers[1].Actions.Single().Timing);
        Assert.Empty(profile.Caps);
    }

    [Fact]
    public void NormalizeAndValidate_RejectsMissingProfileBindingsAndDuplicateTierNames()
    {
        var settings = new StarshipExpeditionSettings();
        settings.EventRewardProfileIds["SalvageSite"] = "missing";
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
        settings.EventRewardProfileIds.Clear();
        settings.RewardProfiles[0].Tiers = [new() { Name = "Common" }, new() { Name = "common" }];
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
    }

    [Fact]
    public async Task SaveAndLoad_UsesDatabaseCatalogRowsAsTheSingleCatalogSource()
    {
        var settings = await CreateStore().LoadAsync();
        settings.EventCatalog.Add(new StarshipEventDefinition("custom-event", "Custom Event", "A custom event.",
            [new() { ChoiceId = "yes", Label = "Yes" }, new() { ChoiceId = "no", Label = "No" }], "yes"));
        await CreateStore().SaveAsync(settings);

        var loaded = await CreateStore().LoadAsync();
        Assert.Contains(loaded.EventCatalog, definition => definition.EventType == "custom-event");
        using var check = _services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(9, await database.StarshipEvents.CountAsync());
        Assert.Equal(4, await database.StarshipRewardProfiles.CountAsync());
        loaded.EventCatalog.RemoveAll(definition => definition.EventType == "custom-event");
        await CreateStore().SaveAsync(loaded);
        Assert.DoesNotContain((await CreateStore().LoadAsync()).EventCatalog, definition => definition.EventType == "custom-event");
    }

    [Fact]
    public async Task Load_PreservesUnchartedEventFiltersAsSeparateRoute()
    {
        var settings = await CreateStore().LoadAsync();
        var salvageIndex = settings.EventCatalog.FindIndex(definition => definition.EventType == "SalvageSite");
        settings.EventCatalog[salvageIndex] = settings.EventCatalog[salvageIndex] with
        {
            AllowedRoutes = ["Uncharted", "Survey Route"]
        };
        await CreateStore().SaveAsync(settings);

        var loaded = await CreateStore().LoadAsync();
        using var scope = _services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await database.StarshipEvents.SingleAsync(item => item.Id == "SalvageSite");
        var persisted = JsonSerializer.Deserialize<StarshipEventDefinition>(row.DocumentJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(["Uncharted", "Survey Route"], loaded.EventCatalog.Single(definition => definition.EventType == "SalvageSite").AllowedRoutes);
        Assert.Equal(["Uncharted", "Survey Route"], persisted.AllowedRoutes);
    }

    [Fact]
    public async Task FailedCatalogSave_RollsBackSettingsAndCatalogRows()
    {
        var store = CreateStore();
        var settings = await store.LoadAsync();
        string originalJson;
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            originalJson = (await context.Settings.SingleAsync()).StringSetting!;
            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_starship_profiles BEFORE INSERT ON starship_reward_profiles BEGIN SELECT RAISE(ABORT, 'test import failure'); END;");
        }
        settings.EventCatalog.Add(new StarshipEventDefinition("rollback-event", "Rollback Event", "A custom event.",
            [new() { ChoiceId = "yes", Label = "Yes" }, new() { ChoiceId = "no", Label = "No" }], "yes"));
        settings.RewardProfiles.Add(new StarshipRewardProfile { Id = "rollback-profile", Name = "Rollback Profile" });

        await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(settings));

        using var check = _services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(8, await database.StarshipEvents.CountAsync());
        Assert.Equal(4, await database.StarshipRewardProfiles.CountAsync());
        Assert.Equal(originalJson, (await database.Settings.SingleAsync()).StringSetting);
    }

    [Fact]
    public async Task Load_RejectsMalformedCatalogJsonAndMissingRows()
    {
        await CreateStore().LoadAsync();
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await context.StarshipEvents.FirstAsync();
            row.DocumentJson = "{";
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore().LoadAsync());
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.StarshipEvents.ExecuteDeleteAsync();
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore().LoadAsync());
    }

    [Fact]
    public async Task SaveAndLoad_PreservesSelectedSectorsAndNormalizesDisabledNames()
    {
        var settings = new StarshipExpeditionSettings
        {
            DisabledBuiltInSectors = [" kepler reach ", "VELA EXPANSE", "Kepler Reach"],
            CustomSectors = ["Glass Nebula"]
        };
        await CreateStore().SaveAsync(settings);

        var loaded = await CreateStore().LoadAsync();

        Assert.Equal(["Kepler Reach", "Vela Expanse"], loaded.DisabledBuiltInSectors);
        Assert.Equal(["Ternion Belt", "Asterion Drift", "Pioneer Verge", "Glass Nebula"], loaded.GetEffectiveSectors());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SectorSelection_RejectsFewerThanTwoEffectiveSectors(int customCount)
    {
        var settings = new StarshipExpeditionSettings
        {
            DisabledBuiltInSectors = [.. StarshipExpeditionSettings.BuiltInSectors],
            CustomSectors = Enumerable.Range(0, customCount).Select(index => $"Sector {index}").ToList()
        };
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
    }

    [Fact]
    public void SectorSelection_AllowsCustomOnlyPoolAndRejectsUnknownBuiltIns()
    {
        var settings = new StarshipExpeditionSettings
        {
            DisabledBuiltInSectors = [.. StarshipExpeditionSettings.BuiltInSectors],
            CustomSectors = ["Glass Nebula", "Delta Reach"]
        };
        StarshipExpeditionSettings.NormalizeAndValidate(settings);
        Assert.Equal(settings.CustomSectors, settings.GetEffectiveSectors());
        settings.DisabledBuiltInSectors.Add("Unknown Sector");
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(settings));
    }

    [Fact]
    public async Task PointLimits_DefaultToUnlimitedAndDeletedRowsStayDeletedAfterReload()
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new());
        var profile = settings.RewardProfiles[0];
        Assert.Null(profile.MaximumPerEvent);
        Assert.Null(profile.MaximumUnbankedPerVoyage);
        Assert.Null(profile.MaximumRiskLossPerEvent);
        profile.Tiers[0].Rewards[0].PointTypeId = 7;
        profile.Caps.Add(new() { PointTypeId = 7, MaximumPerEvent = 30, MaximumUnbankedPerVoyage = 90 });
        await CreateStore().SaveAsync(settings);
        var reloaded = await CreateStore().LoadAsync();
        var saved = reloaded.RewardProfiles.Single(item => item.Id == profile.Id);
        Assert.Equal(30, saved.GetCap(7).MaximumPerEvent);
        saved.Caps.Clear();
        await CreateStore().SaveAsync(reloaded);

        var uncapped = (await CreateStore().LoadAsync()).RewardProfiles.Single(item => item.Id == profile.Id);
        Assert.Empty(uncapped.Caps);
        Assert.Null(uncapped.GetCap(7).MaximumPerEvent);
        Assert.Null(uncapped.GetCap(7).MaximumUnbankedPerVoyage);
    }

    [Fact]
    public async Task StarterEvents_AreInstalledOnceAndKeepEditsAndDeletions()
    {
        var settings = await CreateStore().LoadAsync();
        Assert.True(settings.StarterEventsInstalled);
        Assert.Equal(8, settings.EventCatalog.Count);
        var index = settings.EventCatalog.FindIndex(definition => definition.EventType == "courier-relay");
        settings.EventCatalog[index] = settings.EventCatalog[index] with { Title = "My Relay" };
        settings.EventCatalog.RemoveAll(definition => definition.EventType == "debris-crossing");
        await CreateStore().SaveAsync(settings);

        var reloaded = await CreateStore().LoadAsync();
        Assert.Equal(7, reloaded.EventCatalog.Count);
        Assert.Equal("My Relay", reloaded.EventCatalog.Single(definition => definition.EventType == "courier-relay").Title);
        Assert.DoesNotContain(reloaded.EventCatalog, definition => definition.EventType == "debris-crossing");
    }

    [Fact]
    public async Task ReleaseGate_PersistedCatalogEditsAffectNextVoyageButNotRecoveredVoyage()
    {
        var settingsStore = CreateStore();
        var settings = await settingsStore.LoadAsync();
        settings.GameEnabled = true;
        settings.LegCount = 4;
        settings.StationIntervalLegs = 24;
        settings.ActiveCrewRewardsOnly = false;
        settings.EventCatalog = settings.EventCatalog.Where(definition => StarshipEventCatalog.IsStructural(definition.EventType)).ToList();
        settings.RewardProfiles.Add(new StarshipRewardProfile
        {
            Id = "release-profile", Name = "Release Profile", Enabled = true, SuccessChancePercent = 100,
            Tiers = [new() { Id = "common", Name = "Common", Weight = 1, Rewards = [new() { PointTypeId = 8, Minimum = 10, Maximum = 10 }] }]
        });
        settings.EventCatalog.Add(new StarshipEventDefinition("release-cache", "Original Cache", "A cache near {subject}.",
            [new() { ChoiceId = "recover", Label = "Recover the cache" }, new() { ChoiceId = "pass", Label = "Pass", EndsWithoutReward = true }], "recover", "Recovered {loot}.")
        {
            RewardProfileId = "release-profile", SubjectStyle = "custom", SubjectNames = ["the Quiet Archive"], ObjectKind = "beacon",
            LootByTier = new() { ["Common"] = "the original star chart" }
        });
        await settingsStore.SaveAsync(settings);
        var points = new Mock<IPointsSystem>();
        points.Setup(instance => instance.GetPointTypeById(8)).ReturnsAsync(new PenguinTwitchBot.Database.Bot.Models.Points.PointType { Id = 8, Name = "Credits" });
        points.Setup(instance => instance.AddPointsByUserIdOnce(It.IsAny<string>(), 8, It.IsAny<long>(), It.IsAny<string>())).ReturnsAsync(true);
        StarshipExpeditionService CreateService()
        {
            var chat = new Mock<IServiceBackbone>();
            chat.Setup(instance => instance.SendChatMessage(It.IsAny<string>())).Returns(Task.CompletedTask);
            var websocket = new Mock<IWebSocketMessenger>();
            websocket.Setup(instance => instance.AddToQueue(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            return new StarshipExpeditionService(chat.Object, Mock.Of<ICommandHandler>(),
                new StarshipExpeditionStateStore(_services.GetRequiredService<IServiceScopeFactory>()), CreateStore(), websocket.Object,
                NullLogger<StarshipExpeditionService>.Instance, Mock.Of<IPenguinDispatcher>(), pointsSystem: points.Object);
        }
        var viewer = new CommandEventArgs { UserId = "release-pilot", Name = "pilot", DisplayName = "Pilot" };
        var original = CreateService();
        await original.StartExpeditionAsync("moderator", seed: 2468);
        await original.JoinCrewAsync(viewer);
        await original.AdvanceEventAsync();
        await original.AdvanceEventAsync();
        var opened = await original.AdvanceEventAsync();
        Assert.Equal("Original Cache", opened.CurrentEvent!.Title);

        var edited = await CreateStore().LoadAsync();
        var index = edited.EventCatalog.FindIndex(definition => definition.EventType == "release-cache");
        edited.EventCatalog[index] = edited.EventCatalog[index] with
        {
            Title = "Edited Cache", SubjectNames = ["the New Archive"], ShipSuccessEffect = new() { HullDelta = -20 }
        };
        var reward = edited.RewardProfiles.Single(profile => profile.Id == "release-profile").Tiers[0].Rewards[0];
        reward.Minimum = reward.Maximum = 99;
        await CreateStore().SaveAsync(edited);

        var recovered = CreateService();
        Assert.Equal(opened.CurrentEvent.EventId, (await recovered.GetSnapshotAsync())!.CurrentEvent!.EventId);
        var resolved = await recovered.AdvanceEventAsync();
        var completed = resolved.CompletedEvents.Last();
        Assert.Equal("Original Cache", completed.Title);
        Assert.Equal("the Quiet Archive", completed.Subject);
        Assert.Equal(10, Assert.Single(completed.RewardOutcomes).EarnedAmount);
        Assert.NotNull(completed.ShipContext);
        Assert.Equal(80, resolved.ShipSystems!.Hull);
        await recovered.StopExpeditionAsync("moderator");
        points.Verify(instance => instance.AddPointsByUserIdOnce("release-pilot", 8, 10, It.IsAny<string>()), Times.Once);

        var next = CreateService();
        await next.StartExpeditionAsync("moderator", seed: 2468);
        await next.JoinCrewAsync(viewer);
        await next.AdvanceEventAsync();
        await next.AdvanceEventAsync();
        var nextEvent = await next.AdvanceEventAsync();
        Assert.Equal("Edited Cache", nextEvent.CurrentEvent!.Title);
        Assert.Equal("the New Archive", nextEvent.CurrentEvent.Subject);
        var nextResult = await next.AdvanceEventAsync();
        Assert.Equal(99, Assert.Single(nextResult.CompletedEvents.Last().RewardOutcomes).EarnedAmount);
        Assert.Equal(60, nextResult.ShipSystems!.Hull);
        Assert.Equal(80, nextResult.CompletedEvents.Last().ShipContext!.HullBefore);
        Assert.Equal(60, nextResult.CompletedEvents.Last().ShipContext!.HullAfter);
    }

    [Fact]
    public async Task StarterEvents_InvalidCandidateDoesNotDiscardValidStartersOrResetInstalledFlag()
    {
        var root = Directory.CreateTempSubdirectory("starship-starter-review-");
        try
        {
            var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PenguinTwitchBot", "wwwroot", "help", "starship", "examples"));
            var destination = Path.Combine(root.FullName, "help", "starship", "examples");
            Directory.CreateDirectory(destination);
            foreach (var fileName in StarshipStarterEvents.FileNames)
            {
                var document = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(source, fileName)))!;
                if (fileName == "survey-cache.json") document["event"]!["description"] = "!invalid-command";
                await File.WriteAllTextAsync(Path.Combine(destination, fileName), document.ToJsonString());
            }
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(instance => instance.WebRootPath).Returns(root.FullName);
            var store = new StarshipExpeditionSettingsStore(_services.GetRequiredService<IServiceScopeFactory>(), environment.Object);

            var settings = await store.LoadAsync();

            Assert.Equal(7, settings.EventCatalog.Count);
            Assert.Equal(5, settings.EventCatalog.Single(definition => definition.EventType == "courier-relay").SortOrder);
            Assert.Equal(6, settings.EventCatalog.Single(definition => definition.EventType == "debris-crossing").SortOrder);
            Assert.DoesNotContain(settings.EventCatalog, definition => definition.EventType == "survey-cache");
            Assert.True((await store.LoadAsync()).StarterEventsInstalled);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private StarshipExpeditionSettingsStore CreateStore()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(instance => instance.WebRootPath).Returns(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "PenguinTwitchBot", "wwwroot")));
        return new(_services.GetRequiredService<IServiceScopeFactory>(), environment.Object);
    }
}
