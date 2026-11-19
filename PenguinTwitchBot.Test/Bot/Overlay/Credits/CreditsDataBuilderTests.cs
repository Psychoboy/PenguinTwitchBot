using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Moderation;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Overlay.Credits;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Giveaway;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Test.Helpers;
using PenguinTwitchBot.TwitchApi.Models.Users;

namespace PenguinTwitchBot.Test.Bot.Overlay.Credits
{
    public class CreditsDataBuilderTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly DateTime _now = new(2026, 11, 19, 21, 0, 0, DateTimeKind.Utc);
        private readonly ICreditsEventCollector _collector;
        private readonly IServiceBackbone _backbone;
        private readonly IKnownBots _knownBots;
        private readonly ITwitchService _twitchService;
        private readonly MemoryCache _cache;
        private readonly CreditsDataBuilder _builder;

        public CreditsDataBuilderTests()
        {
            _connection = SqliteTestDatabase.CreateConnection();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection), ServiceLifetime.Scoped);
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            _provider = services.BuildServiceProvider();

            _collector = Substitute.For<ICreditsEventCollector>();
            _backbone = Substitute.For<IServiceBackbone>();
            _backbone.BroadcasterName.Returns("streamerhost");
            _backbone.BotName.Returns("mybotaccount");

            _knownBots = Substitute.For<IKnownBots>();
            _knownBots.IsKnownBot(Arg.Any<string>()).Returns(ci => ci.Arg<string>().Equals("nightbot", StringComparison.OrdinalIgnoreCase));

            _twitchService = Substitute.For<ITwitchService>();
            _twitchService.GetUsersByIdsAsync(Arg.Any<IEnumerable<string>>())
                .Returns(ci =>
                {
                    var dict = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
                    foreach (var id in ci.Arg<IEnumerable<string>>())
                    {
                        dict[id] = new User(id, $"user{id}", $"User{id}", "", _now, $"https://cdn.example/{id}.png");
                    }
                    return dict;
                });
            _twitchService.GetUsersByLoginsAsync(Arg.Any<IEnumerable<string>>())
                .Returns(ci =>
                {
                    var dict = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
                    foreach (var login in ci.Arg<IEnumerable<string>>())
                    {
                        dict[login] = new User($"id_{login}", login, login, "", _now, $"https://cdn.example/{login}.png");
                    }
                    return dict;
                });

            _cache = new MemoryCache(new MemoryCacheOptions());
            var timeResolver = new CreditsTimeWindowResolver(new FixedTimeProvider(_now));
            var avatarResolver = new CreditsAvatarResolver(_twitchService, _cache);

            _builder = new CreditsDataBuilder(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                _collector,
                timeResolver,
                avatarResolver,
                _backbone,
                _knownBots,
                _twitchService);
        }

        public void Dispose()
        {
            _cache.Dispose();
            _provider.Dispose();
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task MultiSourceGrouping_AndEmptyHiding_WorksAsConfigured()
        {
            await using (var scope = _provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.CreditEvents.AddRange(
                    new CreditEvent { EventType = CreditEventTypes.Subscribe, UserId = "u1", Username = "subone", DisplayName = "SubOne", Amount = 1, OccurredAtUtc = _now.AddMinutes(-20) },
                    new CreditEvent { EventType = CreditEventTypes.Resubscribe, UserId = "u2", Username = "resubtwo", DisplayName = "ResubTwo", Amount = 6, OccurredAtUtc = _now.AddMinutes(-15) },
                    new CreditEvent { EventType = CreditEventTypes.SubGift, UserId = "u3", Username = "gifterthree", DisplayName = "GifterThree", Amount = 10, OccurredAtUtc = _now.AddMinutes(-10) });
                await db.SaveChangesAsync();
            }

            var profile = CreateProfile(new CreditsProfileConfig
            {
                TimeWindow = new CreditsTimeWindowConfig { Mode = CreditTimeWindowModes.RollingLookback, LookbackValue = 2, LookbackUnit = CreditLookbackUnits.Hours },
                Sections =
                [
                    new CreditsSectionConfig
                    {
                        Title = "Subscribers",
                        SourceTypes = [CreditSectionSources.NewSubscribers, CreditSectionSources.Resubscribers],
                        HideWhenEmpty = true,
                        EntryFormatTemplate = "{name} x{amount}"
                    },
                    new CreditsSectionConfig
                    {
                        Title = "Sub Gifters",
                        SourceTypes = [CreditSectionSources.SubGifters],
                        HideWhenEmpty = true,
                        EntryFormatTemplate = "{name} x{amount}"
                    },
                    new CreditsSectionConfig
                    {
                        Title = "Incoming Raids",
                        SourceTypes = [CreditSectionSources.IncomingRaids],
                        HideWhenEmpty = true
                    },
                    new CreditsSectionConfig
                    {
                        Title = "Special Thanks",
                        SourceTypes = [CreditSectionSources.NewFollowers],
                        HideWhenEmpty = false,
                        EmptyFallbackText = "No followers yet"
                    }
                ]
            });

            var payload = await _builder.BuildPayloadAsync(profile, new CreditTheme { Name = "Classic Cinema" }, CreditSampleModes.Off);

            Assert.DoesNotContain(payload.Sections, s => s.Title == "Incoming Raids");

            var subsSection = Assert.Single(payload.Sections, s => s.Title == "Subscribers");
            Assert.Equal(2, subsSection.Entries.Count);
            Assert.Contains(subsSection.Entries, e => e.FormattedText == "ResubTwo x6");
            Assert.Contains(subsSection.Entries, e => e.FormattedText == "SubOne x1");

            var giftersSection = Assert.Single(payload.Sections, s => s.Title == "Sub Gifters");
            var gifter = Assert.Single(giftersSection.Entries);
            Assert.Equal("GifterThree x10", gifter.FormattedText);

            var fallbackSection = Assert.Single(payload.Sections, s => s.Title == "Special Thanks");
            Assert.Empty(fallbackSection.Entries);
            Assert.Equal("No followers yet", fallbackSection.EmptyFallbackText);
        }

        [Fact]
        public async Task AggregationSortingExclusions_AndDatabaseSources_FilterAndRankAccurately()
        {
            await using (var scope = _provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var fishType = new FishType { Name = "Goldfish", Rarity = FishRarity.Rare, ImageFileName = "gf.png" };
                db.FishTypes.Add(fishType);

                db.CreditEvents.AddRange(
                    new CreditEvent { EventType = CreditEventTypes.Cheer, UserId = "c1", Username = "whale", DisplayName = "Whale", Amount = 200, Detail = "VIP Cheers", OccurredAtUtc = _now.AddMinutes(-30) },
                    new CreditEvent { EventType = CreditEventTypes.Cheer, UserId = "c1", Username = "whale", DisplayName = "Whale", Amount = 300, Detail = "VIP Cheers", OccurredAtUtc = _now.AddMinutes(-5) },
                    new CreditEvent { EventType = CreditEventTypes.Cheer, UserId = "c2", Username = "tiny", DisplayName = "Tiny", Amount = 10, Detail = "VIP Cheers", OccurredAtUtc = _now.AddMinutes(-10) },
                    new CreditEvent { EventType = CreditEventTypes.Cheer, UserId = "b1", Username = "nightbot", DisplayName = "Nightbot", Amount = 9999, Detail = "VIP Cheers", OccurredAtUtc = _now.AddMinutes(-8) },
                    new CreditEvent { EventType = CreditEventTypes.Cheer, UserId = "h1", Username = "streamerhost", DisplayName = "StreamerHost", Amount = 5000, Detail = "VIP Cheers", OccurredAtUtc = _now.AddMinutes(-7) },
                    new CreditEvent { EventType = CreditEventTypes.Cheer, UserId = "x1", Username = "blockeduser", DisplayName = "BlockedUser", Amount = 5000, Detail = "VIP Cheers", OccurredAtUtc = _now.AddMinutes(-6) });

                db.GiveawayWinners.Add(new GiveawayWinner
                {
                    Username = "LuckyViewer",
                    Prize = "Steam Key",
                    WinningDate = _now.AddMinutes(-12)
                });

                db.FishCatches.AddRange(
                    new FishCatch { UserId = "f1", Username = "anglerone", FishType = fishType, GoldEarned = 120, Weight = 12.5, CaughtAt = _now.AddMinutes(-25) },
                    new FishCatch { UserId = "f1", Username = "anglerone", FishType = fishType, GoldEarned = 80, Weight = 3.2, CaughtAt = _now.AddMinutes(-20) },
                    new FishCatch { UserId = "f2", Username = "anglertwo", FishType = fishType, GoldEarned = 400, Weight = 8.0, CaughtAt = _now.AddMinutes(-15) });

                await db.SaveChangesAsync();
            }

            var profile = CreateProfile(new CreditsProfileConfig
            {
                TimeWindow = new CreditsTimeWindowConfig { Mode = CreditTimeWindowModes.RollingLookback, LookbackValue = 2, LookbackUnit = CreditLookbackUnits.Hours },
                Exclusions = new CreditsExclusionConfig
                {
                    ExcludeKnownBots = true,
                    ExcludeBroadcaster = true,
                    ExcludeBotAccount = true,
                    ExcludedUsernames = ["blockeduser"]
                },
                Sections =
                [
                    new CreditsSectionConfig
                    {
                        Title = "Big Cheers",
                        SourceTypes = [CreditSectionSources.CheersAndBits],
                        AggregationMode = CreditAggregationModes.CombineByUserSum,
                        SortMode = CreditSortModes.HighestAmount,
                        MinAmount = 50,
                        DetailFilter = "VIP",
                        EntryFormatTemplate = "{name} — {amount} bits"
                    },
                    new CreditsSectionConfig
                    {
                        Title = "Giveaway Winners",
                        SourceTypes = [CreditSectionSources.GiveawayWinners],
                        EntryFormatTemplate = "{name} — {detail}"
                    },
                    new CreditsSectionConfig
                    {
                        Title = "Top Anglers",
                        SourceTypes = [CreditSectionSources.FishingTopAnglers],
                        FishingRankBy = "Gold",
                        SortMode = CreditSortModes.HighestAmount,
                        EntryFormatTemplate = "{name} x{amount}"
                    }
                ]
            });

            var payload = await _builder.BuildPayloadAsync(profile, null, CreditSampleModes.Off);

            var cheerSection = payload.Sections.Single(s => s.Title == "Big Cheers");
            var whale = Assert.Single(cheerSection.Entries);
            Assert.Equal("Whale — 500 bits", whale.FormattedText);
            Assert.Equal("https://cdn.example/c1.png", whale.AvatarUrl);

            var gwSection = payload.Sections.Single(s => s.Title == "Giveaway Winners");
            Assert.Single(gwSection.Entries, e => e.FormattedText == "LuckyViewer — Steam Key");

            var fishSection = payload.Sections.Single(s => s.Title == "Top Anglers");
            Assert.Equal(2, fishSection.Entries.Count);
            Assert.Equal("anglertwo x400", fishSection.Entries[0].FormattedText);
            Assert.Equal("anglerone x200", fishSection.Entries[1].FormattedText);

            // Re-running payload build should hit IMemoryCache instead of re-querying Twitch for c1 / f1 / f2
            _twitchService.ClearReceivedCalls();
            _ = await _builder.BuildPayloadAsync(profile, null, CreditSampleModes.Off);
            await _twitchService.DidNotReceive().GetUsersByIdsAsync(Arg.Is<IEnumerable<string>>(ids => ids.Contains("c1")));
        }

        [Fact]
        public async Task TestModes_FakeAndDatabaseViewers_GenerateEntriesWithoutWritingCreditEvents()
        {
            await using (var scope = _provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Viewers.AddRange(
                    new Viewer { UserId = "v1", Username = "realviewer1", DisplayName = "RealViewer1", isMod = false, isVip = true, LastSeen = _now },
                    new Viewer { UserId = "v2", Username = "nightbot", DisplayName = "Nightbot", isMod = true, LastSeen = _now },
                    new Viewer { UserId = "v3", Username = "realviewer2", DisplayName = "RealViewer2", isMod = false, LastSeen = _now });
                await db.SaveChangesAsync();
            }

            var profile = CreateProfile(CreditsBuiltInThemes.CreateDefaultProfileConfig());

            var fakePayload = await _builder.BuildPayloadAsync(profile, null, CreditSampleModes.Fake);
            Assert.NotEmpty(fakePayload.Sections);
            Assert.True(fakePayload.TotalEntries > 0);
            Assert.Equal(CreditSampleModes.Fake, fakePayload.SampleMode);

            var dbPayload = await _builder.BuildPayloadAsync(profile, null, CreditSampleModes.DatabaseViewers);
            Assert.NotEmpty(dbPayload.Sections);
            var allNames = dbPayload.Sections.SelectMany(s => s.Entries).Select(e => e.Username).ToList();
            Assert.Contains("realviewer1", allNames);
            Assert.Contains("realviewer2", allNames);
            Assert.DoesNotContain("nightbot", allNames);

            // Confirm zero rows leaked into CreditEvents table
            await using (var verifyScope = _provider.CreateAsyncScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Empty(await db.CreditEvents.ToListAsync());
            }
        }

        [Fact]
        public void FormatEntryText_CleansDanglingSeparatorsWhenOptionalTokensAreEmpty()
        {
            var sec = new CreditsSectionConfig { EntryFormatTemplate = "{name} — {detail}", ShowAmount = true };
            Assert.Equal("Viewer", CreditsDataBuilder.FormatEntryText(sec, "Viewer", 1, null, null));

            var sec2 = new CreditsSectionConfig { EntryFormatTemplate = "{name} ()", ShowAmount = false };
            Assert.Equal("Viewer", CreditsDataBuilder.FormatEntryText(sec2, "Viewer", 5, null, null));
        }

        [Fact]
        public void FormatEntryText_SinglePassDoesNotReExpandTokensInUserValues()
        {
            var sec = new CreditsSectionConfig
            {
                EntryFormatTemplate = "{name} - {detail} ({amount})",
                ShowAmount = true
            };

            var formatted = CreditsDataBuilder.FormatEntryText(sec, "User{detail}", 42, "Won {amount} coins", null);
            Assert.Equal("User{detail} - Won {amount} coins (42)", formatted);
        }

        [Fact]
        public void DeserializeProfileConfig_NormalizesNullNestedObjectsAndCollections()
        {
            const string json = """
            {
                "timeWindow": null,
                "header": null,
                "footer": { "title": null, "lines": null },
                "playback": null,
                "exclusions": { "excludedUsernames": null },
                "media": null,
                "sections": [
                    null,
                    { "id": "", "title": null, "sourceTypes": null, "staticEntries": [ null, { "name": null, "role": null } ] }
                ]
            }
            """;

            var config = CreditsDataBuilder.DeserializeProfileConfig(json);
            Assert.NotNull(config.TimeWindow);
            Assert.NotNull(config.Header);
            Assert.NotNull(config.Footer);
            Assert.NotNull(config.Footer.Lines);
            Assert.NotNull(config.Playback);
            Assert.NotNull(config.Exclusions);
            Assert.NotNull(config.Exclusions.ExcludedUsernames);
            Assert.NotNull(config.Media);

            var section = Assert.Single(config.Sections);
            Assert.False(string.IsNullOrWhiteSpace(section.Id));
            Assert.NotNull(section.Title);
            Assert.NotNull(section.SourceTypes);
            var staticEntry = Assert.Single(section.StaticEntries);
            Assert.NotNull(staticEntry.Name);
            Assert.NotNull(staticEntry.Role);
        }

        [Fact]
        public async Task BuildPayloadAsync_PreservesDuplicateNameStaticOrder_AndSupportsMaxWeightFishing()
        {
            await using (var scope = _provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var fishType = new FishType { Name = "Trout", Rarity = FishRarity.Common, ImageFileName = "tr.png" };
                db.FishTypes.Add(fishType);
                db.FishCatches.AddRange(
                    new FishCatch { UserId = "f10", Username = "manyfish", FishType = fishType, Weight = 30.0, GoldEarned = 10, CaughtAt = _now.AddMinutes(-30) },
                    new FishCatch { UserId = "f10", Username = "manyfish", FishType = fishType, Weight = 35.0, GoldEarned = 10, CaughtAt = _now.AddMinutes(-20) },
                    new FishCatch { UserId = "f20", Username = "bigfish", FishType = fishType, Weight = 50.0, GoldEarned = 10, CaughtAt = _now.AddMinutes(-10) });
                await db.SaveChangesAsync();
            }

            var profile = CreateProfile(new CreditsProfileConfig
            {
                TimeWindow = new CreditsTimeWindowConfig
                {
                    Mode = CreditTimeWindowModes.RollingLookback,
                    LookbackValue = 2,
                    LookbackUnit = CreditLookbackUnits.Hours
                },
                Sections =
                [
                    new CreditsSectionConfig
                    {
                        Title = "Cast",
                        SourceTypes = [CreditSectionSources.CustomStatic],
                        AggregationMode = CreditAggregationModes.IndividualEvents,
                        SortMode = CreditSortModes.MostRecent,
                        EntryFormatTemplate = "{detail}: {name}",
                        StaticEntries =
                        [
                            new CreditsStaticEntry { Name = "Alex", Role = "Director" },
                            new CreditsStaticEntry { Name = "Sam", Role = "Editor" },
                            new CreditsStaticEntry { Name = "Alex", Role = "Producer" }
                        ]
                    },
                    new CreditsSectionConfig
                    {
                        Title = "Biggest Catch",
                        SourceTypes = [CreditSectionSources.FishingTopAnglers],
                        FishingRankBy = "MaxWeight",
                        SortMode = CreditSortModes.HighestAmount,
                        EntryFormatTemplate = "{name} ({amount})"
                    }
                ]
            });

            var payload = await _builder.BuildPayloadAsync(profile, null, CreditSampleModes.Off);
            var cast = payload.Sections.Single(s => s.Title == "Cast");
            Assert.Equal(["Director: Alex", "Editor: Sam", "Producer: Alex"], cast.Entries.Select(e => e.FormattedText).ToArray());

            var fish = payload.Sections.Single(s => s.Title == "Biggest Catch");
            Assert.Equal("bigfish (50)", fish.Entries[0].FormattedText);
            Assert.Equal("manyfish (35)", fish.Entries[1].FormattedText);
        }

        private static CreditProfile CreateProfile(CreditsProfileConfig cfg) => new()
        {
            Name = "Default",
            ConfigJson = JsonSerializer.Serialize(cfg, CreditsDataBuilder.JsonOptions)
        };

        private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
        }
    }
}
