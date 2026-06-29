using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Metrics;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Metrics;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Database.Repository.Repositories;
using PenguinTwitchBot.Database.Bot.Models;
using System;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Test.Bot.Commands.Metrics
{
    public class SongRequestsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;

        public SongRequestsTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        private SongRequests CreateSongRequests()
        {
            var logger = Substitute.For<ILogger<SongRequests>>();
            var dispatcher = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();
            var commandHandler = Substitute.For<ICommandHandler>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            var scopeFactory = new TestServiceScopeFactory(_context);
            return new SongRequests(scopeFactory, serviceBackbone, commandHandler, dispatcher, logger);
        }

        [Fact]
        public async Task IncrementSongCount_AddsNewHistoryEntry()
        {
            var songRequests = CreateSongRequests();
            var song = new Song { SongId = "testSongId", Title = "Test Song", Duration = TimeSpan.FromMinutes(3) };

            await songRequests.IncrementSongCount(song);

            var historyCount = await _context.SongRequestHistories.CountAsync();
            Assert.Equal(1, historyCount);
            
            var history = await _context.SongRequestHistories.FirstAsync();
            Assert.Equal("testSongId", history.SongId);
            Assert.Equal("Test Song", history.Title);
        }

        [Fact]
        public async Task GetRequestedCount_ReturnsZero_WhenNoHistory()
        {
            var songRequests = CreateSongRequests();
            var result = await songRequests.GetRequestedCount(new Song { SongId = "nonexistent" });
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GetRequestedCount_ReturnsCorrectCount()
        {
            var songId = "popularSong";
            _context.SongRequestHistories.AddRange(
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Popular Song", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Popular Song", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Popular Song", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "otherSong", Title = "Other Song", Duration = TimeSpan.FromMinutes(4), RequestDate = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync();

            var songRequests = CreateSongRequests();
            var result = await songRequests.GetRequestedCount(new Song { SongId = songId });
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task GetRequestedCount_AllTimeReturnsAllRecords()
        {
            var songId = "recentSong";
            _context.SongRequestHistories.AddRange(
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-10) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-20) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-200) }
            );
            await _context.SaveChangesAsync();

            var songRequests = CreateSongRequests();
            var allTime = await songRequests.GetRequestedCount(new Song { SongId = songId });

            Assert.Equal(3, allTime);
        }

        private class TestServiceScopeFactory : IServiceScopeFactory
        {
            private readonly ApplicationDbContext _context;

            public TestServiceScopeFactory(ApplicationDbContext context)
            {
                _context = context;
            }

            public IServiceScope CreateScope()
            {
                return new TestServiceScope(_context);
            }

            public IServiceScope CreateAsyncScope()
            {
                return new TestServiceScope(_context);
            }
        }

        private class TestServiceScope : IServiceScope
        {
            private readonly ApplicationDbContext _context;
            private readonly UnitOfWork _unitOfWork;
            private readonly IServiceProvider _serviceProvider;

            public TestServiceScope(ApplicationDbContext context)
            {
                _context = context;
                _unitOfWork = new UnitOfWork(_context);
                _serviceProvider = new TestServiceProvider(_unitOfWork);
            }

            public IServiceProvider ServiceProvider => _serviceProvider;

            public void Dispose() { }
        }

        private class TestServiceProvider : IServiceProvider
        {
            private readonly UnitOfWork _unitOfWork;

            public TestServiceProvider(UnitOfWork unitOfWork)
            {
                _unitOfWork = unitOfWork;
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IUnitOfWork))
                    return _unitOfWork;
                return null;
            }
        }
    }
}