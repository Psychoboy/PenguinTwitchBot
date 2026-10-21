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
            ShipSystems = new() { StartingHull = 80, StartingSupplies = 60, TravelSuppliesCost = 4, StationRepairAmount = 25 },
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
        Assert.Equal(80, loaded.ShipSystems.StartingHull);
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
    public async Task Load_ImportsLegacyBlobOnceAndThenUsesCanonicalRows()
    {
        var legacy = new StarshipExpeditionSettings();
        legacy.RewardProfiles[0].Name = "My original reward";
        var json = JsonSerializer.SerializeToNode(legacy, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsObject();
        json.Remove("eventCatalog");
        json.Remove("catalogStorageVersion");
        json.Remove("disabledBuiltInSectors");
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Settings.Add(new Setting { Name = StarshipExpeditionSettingsStore.SettingKey, StringSetting = json.ToJsonString(), DataType = Setting.DataTypeEnum.String });
            await context.SaveChangesAsync();
        }
        var imported = await CreateStore().LoadAsync();
        Assert.Equal(1, imported.CatalogStorageVersion);
        Assert.Empty(imported.DisabledBuiltInSectors);
        Assert.Equal(StarshipExpeditionSettings.BuiltInSectors, imported.GetEffectiveSectors());
        Assert.Equal("My original reward", imported.RewardProfiles.Single(profile => profile.EventType == "RouteChoice").Name);
        imported.EventCatalog.Add(new StarshipEventDefinition("custom-event", "Custom Event", "A custom event.",
            [new() { ChoiceId = "yes", Label = "Yes" }, new() { ChoiceId = "no", Label = "No" }], "yes"));
        await CreateStore().SaveAsync(imported);

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
        Assert.Equal(1, settings.StarterEventsVersion);
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
    public async Task StarterEvents_UpgradeExistingCatalogWithoutDuplicatesOrOverwritingNames()
    {
        var settings = await CreateStore().LoadAsync();
        var index = settings.EventCatalog.FindIndex(definition => definition.EventType == "courier-relay");
        settings.EventCatalog[index] = settings.EventCatalog[index] with { Title = "Existing Relay" };
        await CreateStore().SaveAsync(settings);
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await context.Settings.SingleAsync();
            var json = JsonNode.Parse(row.StringSetting!)!;
            json.AsObject().Remove("starterEventsVersion");
            row.StringSetting = json.ToJsonString();
            await context.SaveChangesAsync();
        }
        var upgraded = await CreateStore().LoadAsync();
        Assert.Equal(8, upgraded.EventCatalog.Count);
        Assert.Equal("Existing Relay", upgraded.EventCatalog.Single(definition => definition.EventType == "courier-relay").Title);
        Assert.Equal(1, (await CreateStore().LoadAsync()).StarterEventsVersion);
    }

    [Fact]
    public async Task ReleaseGate_PersistedCatalogEditsAffectNextVoyageButNotRecoveredVoyage()
    {
        var settingsStore = CreateStore();
        var settings = await settingsStore.LoadAsync();
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
        edited.EventCatalog[index] = edited.EventCatalog[index] with { Title = "Edited Cache", SubjectNames = ["the New Archive"] };
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
    }

    [Fact]
    public async Task StarterEvents_InvalidCandidateDoesNotDiscardValidStartersOrBlockSavedVersion()
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
            Assert.Equal(StarshipStarterEvents.CurrentVersion, (await store.LoadAsync()).StarterEventsVersion);
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
