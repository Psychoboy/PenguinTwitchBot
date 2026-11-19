using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.Overlay.Credits;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Test.Helpers;

namespace PenguinTwitchBot.Test.Bot.Overlay.Credits
{
    public class CreditsTimeWindowResolverTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly UnitOfWork _uow;
        private readonly DateTime _now = new(2026, 11, 19, 20, 0, 0, DateTimeKind.Utc);
        private readonly CreditsTimeWindowResolver _resolver;

        public CreditsTimeWindowResolverTests()
        {
            _connection = SqliteTestDatabase.CreateConnection();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new ApplicationDbContext(options);
            _uow = new UnitOfWork(_context);
            _resolver = new CreditsTimeWindowResolver(new FixedTimeProvider(_now));
        }

        public void Dispose()
        {
            _uow.Dispose();
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task SinceStreamStart_UsesOpenSessionAndPreStreamGraceMinutes()
        {
            var closed = new CreditStreamSession
            {
                StartedAtUtc = _now.AddDays(-2),
                EndedAtUtc = _now.AddDays(-2).AddHours(4),
                Title = "Older Stream"
            };
            var active = new CreditStreamSession
            {
                StartedAtUtc = _now.AddHours(-3),
                EndedAtUtc = null,
                Title = "Current Live Stream"
            };
            _context.CreditStreamSessions.AddRange(closed, active);
            await _context.SaveChangesAsync();

            var resolved = await _resolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.SinceStreamStart,
                PreStreamGraceMinutes = 20
            });

            Assert.Equal(active.Id, resolved.Session?.Id);
            Assert.Equal(active.StartedAtUtc.AddMinutes(-20), resolved.StartUtc);
            Assert.Equal(_now, resolved.EndUtc);
        }

        [Fact]
        public async Task SinceStreamStart_WhenOffline_FallsBackToMostRecentCompletedSession()
        {
            var lastCompleted = new CreditStreamSession
            {
                StartedAtUtc = _now.AddDays(-1).AddHours(-4),
                EndedAtUtc = _now.AddDays(-1),
                Title = "Yesterday Stream"
            };
            _context.CreditStreamSessions.Add(lastCompleted);
            await _context.SaveChangesAsync();

            var resolved = await _resolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.SinceStreamStart,
                PreStreamGraceMinutes = 15
            });

            Assert.Equal(lastCompleted.Id, resolved.Session?.Id);
            Assert.Equal(lastCompleted.StartedAtUtc.AddMinutes(-15), resolved.StartUtc);
            Assert.Equal(lastCompleted.EndedAtUtc!.Value, resolved.EndUtc);
        }

        [Fact]
        public async Task LastCompletedStream_PicksLatestClosedNonCheckpointSession()
        {
            var older = new CreditStreamSession
            {
                StartedAtUtc = _now.AddDays(-5),
                EndedAtUtc = _now.AddDays(-5).AddHours(3)
            };
            var latestCompleted = new CreditStreamSession
            {
                StartedAtUtc = _now.AddDays(-1),
                EndedAtUtc = _now.AddDays(-1).AddHours(5)
            };
            var currentlyLive = new CreditStreamSession
            {
                StartedAtUtc = _now.AddHours(-2),
                EndedAtUtc = null
            };
            _context.CreditStreamSessions.AddRange(older, latestCompleted, currentlyLive);
            await _context.SaveChangesAsync();

            var resolved = await _resolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.LastCompletedStream,
                PreStreamGraceMinutes = 10
            });

            Assert.Equal(latestCompleted.Id, resolved.Session?.Id);
            Assert.Equal(latestCompleted.StartedAtUtc.AddMinutes(-10), resolved.StartUtc);
            Assert.Equal(latestCompleted.EndedAtUtc!.Value, resolved.EndUtc);
        }

        [Theory]
        [InlineData(45, CreditLookbackUnits.Minutes, 45)]
        [InlineData(6, CreditLookbackUnits.Hours, 360)]
        [InlineData(2, CreditLookbackUnits.Days, 2880)]
        public async Task RollingLookback_SupportsMinutesHoursAndDays(int val, string unit, int expectedMinutes)
        {
            var resolved = await _resolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.RollingLookback,
                LookbackValue = val,
                LookbackUnit = unit
            });

            Assert.Equal(_now.AddMinutes(-expectedMinutes), resolved.StartUtc);
            Assert.Equal(_now, resolved.EndUtc);
        }

        [Fact]
        public async Task SinceManualCheckpoint_UsesLatestOfProfileOrSessionCheckpoint()
        {
            var dbCheckpoint = new CreditStreamSession
            {
                StartedAtUtc = _now.AddHours(-10),
                IsManualCheckpoint = true,
                Title = "DB Checkpoint"
            };
            _context.CreditStreamSessions.Add(dbCheckpoint);
            await _context.SaveChangesAsync();

            var profileCheckpoint = _now.AddHours(-4);
            var resolved = await _resolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.SinceManualCheckpoint,
                ManualCheckpointUtc = profileCheckpoint
            });

            Assert.Equal(profileCheckpoint, resolved.StartUtc);
            Assert.Equal(_now, resolved.EndUtc);
        }

        [Fact]
        public async Task CustomDateRange_AndSpecificSession_ResolveExactBounds()
        {
            var session = new CreditStreamSession
            {
                StartedAtUtc = _now.AddDays(-3),
                EndedAtUtc = _now.AddDays(-3).AddHours(4),
                Title = "Specific Stream"
            };
            _context.CreditStreamSessions.Add(session);
            await _context.SaveChangesAsync();

            var customStart = _now.AddHours(-12);
            var customEnd = _now.AddHours(-2);
            var customResolved = await _resolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.CustomDateRange,
                CustomStartUtc = customStart,
                CustomEndUtc = customEnd
            });
            Assert.Equal(customStart, customResolved.StartUtc);
            Assert.Equal(customEnd, customResolved.EndUtc);

            var sessionResolved = await _resolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.SpecificSession,
                SpecificSessionId = session.Id,
                PreStreamGraceMinutes = 15
            });
            Assert.Equal(session.Id, sessionResolved.Session?.Id);
            Assert.Equal(session.StartedAtUtc.AddMinutes(-15), sessionResolved.StartUtc);
            Assert.Equal(session.EndedAtUtc!.Value, sessionResolved.EndUtc);
        }

        [Fact]
        public async Task InvertedCustomRange_AndLargeRollingLookback_ClampGracefully()
        {
            var later = _now.AddHours(-1);
            var earlier = _now.AddHours(-5);
            var customResolved = await _resolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.CustomDateRange,
                CustomStartUtc = later,
                CustomEndUtc = earlier
            });
            Assert.True(customResolved.EndUtc >= customResolved.StartUtc);
            Assert.Equal(later, customResolved.StartUtc);
            Assert.Equal(later, customResolved.EndUtc);

            var earlyResolver = new CreditsTimeWindowResolver(new FixedTimeProvider(new DateTime(1, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
            var clamped = await earlyResolver.ResolveAsync(_uow, new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.RollingLookback,
                LookbackValue = 30,
                LookbackUnit = CreditLookbackUnits.Days
            });
            Assert.Equal(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), clamped.StartUtc);
        }

        private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
        }
    }
}
