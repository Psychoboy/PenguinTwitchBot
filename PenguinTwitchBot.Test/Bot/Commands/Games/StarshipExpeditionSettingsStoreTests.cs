using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models;
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
    public async Task SaveAndLoad_PreservesSettingsAndNormalizedCustomSectors()
    {
        var store = CreateStore();
        var settings = new StarshipExpeditionSettings
        {
            LegCount = 8,
            MinimumEventIntervalMinutes = 20,
            MaximumEventIntervalMinutes = 35,
            BoardingDurationSeconds = 45,
            ActiveCrewRewardsOnly = false,
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

        await store.SaveAsync(settings);
        var loaded = await CreateStore().LoadAsync();

        Assert.Equal(8, loaded.LegCount);
        Assert.Equal(20, loaded.MinimumEventIntervalMinutes);
        Assert.Equal(35, loaded.MaximumEventIntervalMinutes);
        Assert.Equal(45, loaded.BoardingDurationSeconds);
        Assert.False(loaded.ActiveCrewRewardsOnly);
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
        Assert.Equal(5, context.StarshipEvents.Count());
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
        Assert.True(settings.ActiveCrewRewardsOnly);
        Assert.True(settings.AnnounceSessionStatusInChat);
        Assert.All(settings.RewardProfiles, profile => Assert.False(profile.Enabled));
        Assert.Empty(settings.CustomSectors);
        Assert.Equal(5, settings.GetEffectiveSectors().Count);
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
    public void NormalizeAndValidate_RejectsInvalidRewardOddsAndUnboundedRanges()
    {
        var wrongOdds = new StarshipExpeditionSettings
        {
            RewardProfiles = [new StarshipRewardProfile { EventType = "SignalDetected", Enabled = true, PointTypeId = 1, CommonWeightPercent = 50 }]
        };
        Assert.Throws<ArgumentException>(() => StarshipExpeditionSettings.NormalizeAndValidate(wrongOdds));

        var unbounded = new StarshipExpeditionSettings
        {
            RewardProfiles = [new StarshipRewardProfile
            {
                EventType = "SignalDetected",
                Enabled = true,
                PointTypeId = 1,
                CommonMinimum = StarshipExpeditionSettings.MaximumConfiguredReward + 1,
                CommonMaximum = StarshipExpeditionSettings.MaximumConfiguredReward + 1
            }]
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipExpeditionSettings.NormalizeAndValidate(unbounded));
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
        Assert.Equal(2, profile.Caps.Count);
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
    public async Task Load_ImportsLegacyBlobOnceAndThenUsesCanonicalRows()
    {
        var legacy = new StarshipExpeditionSettings();
        legacy.RewardProfiles[0].Name = "My original reward";
        var json = JsonSerializer.SerializeToNode(legacy, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsObject();
        json.Remove("eventCatalog");
        json.Remove("catalogStorageVersion");
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Settings.Add(new Setting { Name = StarshipExpeditionSettingsStore.SettingKey, StringSetting = json.ToJsonString(), DataType = Setting.DataTypeEnum.String });
            await context.SaveChangesAsync();
        }
        var imported = await CreateStore().LoadAsync();
        Assert.Equal(1, imported.CatalogStorageVersion);
        Assert.Equal("My original reward", imported.RewardProfiles.Single(profile => profile.EventType == "RouteChoice").Name);
        imported.EventCatalog.Add(new StarshipEventDefinition("custom-event", "Custom Event", "A custom event.",
            [new() { ChoiceId = "yes", Label = "Yes" }, new() { ChoiceId = "no", Label = "No" }], "yes"));
        await CreateStore().SaveAsync(imported);

        var loaded = await CreateStore().LoadAsync();
        Assert.Contains(loaded.EventCatalog, definition => definition.EventType == "custom-event");
        using var check = _services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(6, await database.StarshipEvents.CountAsync());
        Assert.Equal(4, await database.StarshipRewardProfiles.CountAsync());
        loaded.EventCatalog.RemoveAll(definition => definition.EventType == "custom-event");
        await CreateStore().SaveAsync(loaded);
        Assert.DoesNotContain((await CreateStore().LoadAsync()).EventCatalog, definition => definition.EventType == "custom-event");
    }

    [Fact]
    public async Task FailedCatalogImport_RollsBackRowsAndLeavesLegacyBlobIntact()
    {
        var json = JsonSerializer.Serialize(new StarshipExpeditionSettings(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Settings.Add(new Setting { Name = StarshipExpeditionSettingsStore.SettingKey, StringSetting = json, DataType = Setting.DataTypeEnum.String });
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_starship_profiles BEFORE INSERT ON starship_reward_profiles BEGIN SELECT RAISE(ABORT, 'test import failure'); END;");
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => CreateStore().LoadAsync());

        using var check = _services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(database.StarshipEvents);
        Assert.Empty(database.StarshipRewardProfiles);
        Assert.Equal(json, (await database.Settings.SingleAsync()).StringSetting);
    }

    [Fact]
    public async Task Load_RejectsUnsupportedCatalogDocumentAndMissingRows()
    {
        await CreateStore().LoadAsync();
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await context.StarshipEvents.FirstAsync();
            var document = JsonNode.Parse(row.DocumentJson)!;
            document["schemaVersion"] = 99;
            row.DocumentJson = document.ToJsonString();
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

    private StarshipExpeditionSettingsStore CreateStore() => new(_services.GetRequiredService<IServiceScopeFactory>());
}
