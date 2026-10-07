using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Circuit;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.IpLogs;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using Xunit;

namespace PenguinTwitchBot.Test.Circuit
{
    public class IpLogTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly ServiceProvider _serviceProvider;
        private readonly ILogger<IpLog> _logger;
        private readonly IIpLogRetentionSettingsService _retentionSettings;
        private readonly IpLog _ipLog;

        public IpLogTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
            _context.Database.EnsureCreated();

            _logger = Substitute.For<ILogger<IpLog>>();
            _retentionSettings = Substitute.For<IIpLogRetentionSettingsService>();
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

            _ipLog = new IpLog(_logger, scopeFactory, _retentionSettings);
        }

        public void Dispose()
        {
            _context.Dispose();
            _serviceProvider.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task AddLogEntry_CreatesNewEntry_WhenUserIdAndIpAreNew()
        {
            // Act
            await _ipLog.AddLogEntry("CoolUser", "12345", "192.168.1.1");

            // Assert
            var entry = await _context.IpLogEntrys.FirstOrDefaultAsync(x => x.UserId == "12345" && x.Ip == "192.168.1.1");
            Assert.NotNull(entry);
            Assert.Equal("cooluser", entry.Username);
            Assert.Equal("12345", entry.UserId);
            Assert.Equal("192.168.1.1", entry.Ip);
            Assert.Equal(1, entry.Count);
        }

        [Fact]
        public async Task AddLogEntry_IncrementsCount_WhenSameUserIdAndSameIpConnectsAgain()
        {
            // Arrange
            await _ipLog.AddLogEntry("CoolUser", "12345", "192.168.1.1");

            // Act
            await _ipLog.AddLogEntry("CoolUser", "12345", "192.168.1.1");

            // Assert
            var entries = await _context.IpLogEntrys.Where(x => x.UserId == "12345" && x.Ip == "192.168.1.1").ToListAsync();
            Assert.Single(entries);
            Assert.Equal(2, entries[0].Count);
        }

        [Fact]
        public async Task AddLogEntry_UpdatesUsername_WhenUserRenamesOnTwitch()
        {
            // Arrange - original login with old username
            await _ipLog.AddLogEntry("OldUsername", "12345", "192.168.1.1");

            var initialEntry = await _context.IpLogEntrys.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == "12345");
            Assert.NotNull(initialEntry);
            Assert.Equal("oldusername", initialEntry.Username);

            // Act - user renamed on Twitch and connects with new username but same UserId
            await _ipLog.AddLogEntry("NewUsername", "12345", "192.168.1.1");

            // Assert - same row should be updated with new username and incremented count
            var entries = await _context.IpLogEntrys.AsNoTracking().Where(x => x.UserId == "12345").ToListAsync();
            Assert.Single(entries);
            Assert.Equal("newusername", entries[0].Username);
            Assert.Equal(2, entries[0].Count);
        }

        [Fact]
        public async Task AddLogEntry_AllowsMultipleIps_ForSameUserId()
        {
            // Act - same user connects from two different IP addresses
            await _ipLog.AddLogEntry("CoolUser", "12345", "192.168.1.1");
            await _ipLog.AddLogEntry("CoolUser", "12345", "10.0.0.1");

            // Assert - both IP entries should exist for this user
            var entries = await _context.IpLogEntrys.AsNoTracking().Where(x => x.UserId == "12345").ToListAsync();
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, x => x.Ip == "192.168.1.1");
            Assert.Contains(entries, x => x.Ip == "10.0.0.1");
        }

        [Theory]
        [InlineData("", "12345", "192.168.1.1")]
        [InlineData("   ", "12345", "192.168.1.1")]
        [InlineData("anonymous", "12345", "192.168.1.1")]
        [InlineData("Anonymous", "12345", "192.168.1.1")]
        [InlineData("CoolUser", "", "192.168.1.1")]
        [InlineData("CoolUser", "   ", "192.168.1.1")]
        [InlineData("CoolUser", "anonymous", "192.168.1.1")]
        [InlineData("CoolUser", "12345", "")]
        [InlineData("CoolUser", "12345", "   ")]
        [InlineData("CoolUser", "12345", "unknown")]
        public async Task AddLogEntry_Ignores_WhenUserOrIpIsInvalidOrAnonymous(string username, string userId, string ip)
        {
            // Act
            await _ipLog.AddLogEntry(username, userId, ip);

            // Assert
            var count = await _context.IpLogEntrys.AsNoTracking().CountAsync();
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task AddLogEntry_CatchesAndLogsErrors_WithoutThrowing()
        {
            // Arrange - simulate scope factory throwing an exception
            var faultyScopeFactory = Substitute.For<IServiceScopeFactory>();
            faultyScopeFactory.CreateScope().Returns(_ => throw new InvalidOperationException("DB offline"));
            var ipLogWithFault = new IpLog(_logger, faultyScopeFactory, _retentionSettings);

            // Act & Assert - must not throw exception
            var exception = await Record.ExceptionAsync(() => ipLogWithFault.AddLogEntry("ValidUser", "12345", "192.168.1.1"));
            Assert.Null(exception);
        }

        [Fact]
        public async Task AddLogEntry_HandlesMultipleExistingDuplicates_ByMergingCounts()
        {
            // Arrange - simulate pre-existing duplicate entries with the same (UserId, Ip)
            _context.IpLogEntrys.AddRange(
                new IpLogEntry { Id = "id1", UserId = "12345", Username = "oldname", Ip = "1.2.3.4", Count = 3, ConnectedDate = DateTime.UtcNow.AddDays(-2) },
                new IpLogEntry { Id = "id2", UserId = "12345", Username = "oldname2", Ip = "1.2.3.4", Count = 5, ConnectedDate = DateTime.UtcNow.AddDays(-1) }
            );
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            await _ipLog.AddLogEntry("CurrentName", "12345", "1.2.3.4");

            // Assert
            var entries = await _context.IpLogEntrys.AsNoTracking().Where(x => x.UserId == "12345" && x.Ip == "1.2.3.4").ToListAsync();
            Assert.Single(entries);
            Assert.Equal("currentname", entries[0].Username);
            Assert.Equal(9, entries[0].Count); // 3 + 5 + 1
        }
    }
}
