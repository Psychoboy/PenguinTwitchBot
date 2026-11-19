using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Overlay.Credits;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.CustomMiddleware;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Overlay.Credits
{
    public class CreditsEventCollectorTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IServiceBackbone _backbone;
        private readonly ITwitchService _twitchService;
        private readonly MutableTestTimeProvider _timeProvider;
        private readonly CreditsEventCollectorService _collector;

        public CreditsEventCollectorTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection), ServiceLifetime.Scoped);
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();

            _backbone = Substitute.For<IServiceBackbone>();
            _twitchService = Substitute.For<ITwitchService>();
            _twitchService.IsStreamOnline().Returns(false);
            _timeProvider = new MutableTestTimeProvider(new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero));

            _collector = new CreditsEventCollectorService(
                _backbone,
                _twitchService,
                _provider.GetRequiredService<IServiceScopeFactory>(),
                Substitute.For<ILogger<CreditsEventCollectorService>>(),
                _timeProvider);
        }

        public void Dispose()
        {
            _collector.Dispose();
            _provider.Dispose();
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task StartAsync_CapturesTwitchAndBotEventsIntoDatabase()
        {
            await _collector.StartAsync(CancellationToken.None);

            await _collector.OnFollow(_backbone, new FollowEventArgs
            {
                UserId = "101",
                Username = "Alice_Follow",
                DisplayName = "Alice"
            });

            await _collector.OnSubscription(_backbone, new SubscriptionEventArgs
            {
                UserId = "102",
                Name = "bob_sub",
                DisplayName = "BobSub",
                Tier = "1000",
                IsRenewal = false,
                IsGift = false
            });

            await _collector.OnSubscription(_backbone, new SubscriptionEventArgs
            {
                UserId = "103",
                Name = "carol_resub",
                DisplayName = "CarolResub",
                Tier = "2000",
                Count = 7,
                IsRenewal = true
            });

            await _collector.OnSubscriptionGift(_backbone, new SubscriptionGiftEventArgs
            {
                UserId = "104",
                Name = "dave_gift",
                DisplayName = "DaveGift",
                GiftAmount = 5
            });

            await _collector.OnCheer(_backbone, new CheerEventArgs
            {
                UserId = "105",
                Name = "eve_bits",
                DisplayName = "EveBits",
                Amount = 500,
                Message = "GG!"
            });

            await _collector.OnIncomingRaid(_backbone, new RaidEventArgs
            {
                UserId = "106",
                Name = "raider_pro",
                DisplayName = "RaiderPro",
                NumberOfViewers = 42
            });

            await _collector.OnChannelPointRedeem(_backbone, new ChannelPointRedeemEventArgs
            {
                UserId = "107",
                Username = "frank_redeem",
                Sender = "FrankRedeem",
                Title = "Hydrate",
                UserInput = "drink water"
            });

            await _collector.RecordCustomEventAsync(CreditEventTypes.DropGameWin, "108", "grace_drop", "GraceDrop", 1, "Parachute");
            await _collector.RecordCustomEventAsync(CreditEventTypes.SongRequest, "109", "henry_song", "HenrySong", 1, "Space Oddity");

            await _collector.StopAsync(CancellationToken.None);

            await using var scope = _provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var events = await db.CreditEvents.OrderBy(e => e.OccurredAtUtc).ToListAsync();

            Assert.Contains(events, e => e.EventType == CreditEventTypes.Follow && e.Username == "alice_follow");
            Assert.Contains(events, e => e.EventType == CreditEventTypes.Subscribe && e.Username == "bob_sub");
            Assert.Contains(events, e => e.EventType == CreditEventTypes.Resubscribe && e.Username == "carol_resub" && e.Amount == 7m);
            Assert.Contains(events, e => e.EventType == CreditEventTypes.SubGift && e.Username == "dave_gift" && e.Amount == 5m);
            Assert.Contains(events, e => e.EventType == CreditEventTypes.Cheer && e.Username == "eve_bits" && e.Amount == 500m);
            Assert.Contains(events, e => e.EventType == CreditEventTypes.IncomingRaid && e.Username == "raider_pro" && e.Amount == 42m);
            Assert.Contains(events, e => e.EventType == CreditEventTypes.ChannelPointRedeem && e.Detail == "Hydrate");
            Assert.Contains(events, e => e.EventType == CreditEventTypes.DropGameWin && e.Detail == "Parachute");
            Assert.Contains(events, e => e.EventType == CreditEventTypes.SongRequest && e.Detail == "Space Oddity");
        }

        [Fact]
        public async Task BitsUse_SkipsStandardCheerDuplicate_CapturesPowerUps()
        {
            await _collector.StartAsync(CancellationToken.None);

            await _collector.OnBitsUse(_backbone, new BitsUseEventArgs
            {
                UserId = "201",
                Name = "cheeruser",
                DisplayName = "CheerUser",
                Amount = 100,
                Type = "cheer"
            });

            await _collector.OnBitsUse(_backbone, new BitsUseEventArgs
            {
                UserId = "202",
                Name = "poweruser",
                DisplayName = "PowerUser",
                Amount = 250,
                Type = "power_up",
                IsPowerUp = true,
                PowerUp = new PowerUp { Type = "gigantify_an_emote" }
            });

            await _collector.StopAsync(CancellationToken.None);

            await using var scope = _provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var events = await db.CreditEvents.ToListAsync();

            Assert.DoesNotContain(events, e => e.Username == "cheeruser");
            var powerUp = Assert.Single(events, e => e.EventType == CreditEventTypes.BitsPowerUp);
            Assert.Equal("poweruser", powerUp.Username);
            Assert.Equal(250m, powerUp.Amount);
            Assert.Equal("gigantify an emote", powerUp.Detail);
        }

        [Fact]
        public async Task ChatNotification_DeduplicatesSubAndRaid_CapturesSpecialNotices()
        {
            await _collector.StartAsync(CancellationToken.None);

            // Should be ignored because dedicated EventSub events handle sub/resub/sub_gift/community_sub_gift/raid
            foreach (var ignored in new[] { "sub", "resub", "sub_gift", "community_sub_gift", "raid", "announcement" })
            {
                await _collector.OnChatNotification(_backbone, new ChatNotificationEventArgs
                {
                    UserId = "300",
                    Name = "dupuser",
                    DisplayName = "DupUser",
                    NoticeType = ignored
                });
            }

            await _collector.OnChatNotification(_backbone, new ChatNotificationEventArgs
            {
                UserId = "301",
                Name = "streaker",
                DisplayName = "Streaker",
                NoticeType = "watch_streak",
                WatchStreak = new ChatNotificationWatchStreakInfo { StreakCount = 6 }
            });

            await _collector.OnChatNotification(_backbone, new ChatNotificationEventArgs
            {
                UserId = "302",
                Name = "donor",
                DisplayName = "Donor",
                NoticeType = "charity_donation",
                CharityDonation = new ChatNotificationCharityDonationInfo
                {
                    CharityName = "Save The Penguins",
                    AmountValue = 2550,
                    AmountDecimalPlaces = 2,
                    AmountCurrency = "USD"
                }
            });

            await _collector.OnChatNotification(_backbone, new ChatNotificationEventArgs
            {
                UserId = "303",
                Name = "upgrader",
                DisplayName = "Upgrader",
                NoticeType = "prime_paid_upgrade",
                PrimePaidUpgrade = new ChatNotificationPrimePaidUpgradeInfo { SubTier = "1000" }
            });

            await _collector.OnChatNotification(_backbone, new ChatNotificationEventArgs
            {
                UserId = "304",
                Name = "forwarder",
                DisplayName = "Forwarder",
                NoticeType = "pay_it_forward"
            });

            await _collector.OnChatNotification(_backbone, new ChatNotificationEventArgs
            {
                UserId = "305",
                Name = "badger",
                DisplayName = "Badger",
                NoticeType = "bits_badge_tier",
                BitsBadgeTier = new ChatNotificationBitsBadgeTierInfo { Tier = 5000 }
            });

            await _collector.StopAsync(CancellationToken.None);

            await using var scope = _provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var events = await db.CreditEvents.ToListAsync();

            Assert.DoesNotContain(events, e => e.Username == "dupuser");
            Assert.Contains(events, e => e.EventType == CreditEventTypes.WatchStreak && e.Amount == 6m);
            Assert.Contains(events, e => e.EventType == CreditEventTypes.CharityDonation && e.Amount == 25.5m && e.Tier == "USD");
            Assert.Contains(events, e => e.EventType == CreditEventTypes.SubUpgrade && e.Username == "upgrader");
            Assert.Contains(events, e => e.EventType == CreditEventTypes.PayItForward && e.Username == "forwarder");
            Assert.Contains(events, e => e.EventType == CreditEventTypes.BitsBadgeTier && e.Amount == 5000m);
        }

        [Fact]
        public async Task ChatterCoalescing_MergesMessagesIntoOneEventPerBucketAndTracksStreamSessions()
        {
            await _collector.StartAsync(CancellationToken.None);

            await _collector.OnStreamStarted(_backbone, EventArgs.Empty);
            Assert.NotNull(_collector.CurrentStreamSessionId);

            for (var i = 0; i < 4; i++)
            {
                _collector.RecordChatter(new ChatMessageEventArgs
                {
                    FromOwnChannel = true,
                    UserId = "401",
                    Name = "ChatterOne",
                    DisplayName = "ChatterOne",
                    IsMod = i == 2,
                    Message = $"Hello #{i}"
                });
            }

            await _collector.FlushPendingChattersAsync();

            // Send one more message in the same session and flush again -> should update existing DB row
            _timeProvider.Advance(TimeSpan.FromMinutes(2));
            _collector.RecordChatter(new ChatMessageEventArgs
            {
                FromOwnChannel = true,
                UserId = "401",
                Name = "ChatterOne",
                DisplayName = "ChatterOne",
                Message = "Hello #5"
            });

            await _collector.OnStreamEnded(_backbone, EventArgs.Empty);
            Assert.Null(_collector.CurrentStreamSessionId);
            await _collector.StopAsync(CancellationToken.None);

            await using var scope = _provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var chatterRows = await db.CreditEvents.Where(e => e.EventType == CreditEventTypes.Chatter).ToListAsync();
            var row = Assert.Single(chatterRows);
            Assert.Equal("chatterone", row.Username);
            Assert.Equal(5m, row.Amount);
            Assert.Contains("\"isMod\":true", row.MetadataJson, StringComparison.Ordinal);

            var session = await db.CreditStreamSessions.SingleAsync(s => !s.IsManualCheckpoint);
            Assert.NotNull(session.EndedAtUtc);
        }

        private sealed class MutableTestTimeProvider(DateTimeOffset initial) : TimeProvider
        {
            private DateTimeOffset _now = initial;
            public override DateTimeOffset GetUtcNow() => _now;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
        }
    }
}
