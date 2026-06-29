using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Metrics;
using PenguinTwitchBot.Database.Repository.Repositories;

namespace PenguinTwitchBot.Test.Database.Repositories
{
    public class SongRequestHistoryRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly SongRequestHistoryRepository _repository;

        public SongRequestHistoryRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();
            _repository = new SongRequestHistoryRepository(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public void SongRequestHistory_HasCompositeIndexOnSongIdAndRequestDate()
        {
            var entityType = _context.Model.FindEntityType(typeof(SongRequestHistory)) ?? _context.Model.FindEntityType("PenguinTwitchBot.Database.Bot.Models.Metrics.SongRequestHistory");
            Assert.NotNull(entityType);

            var indexes = entityType.GetIndexes();
            var songRequestIndex = indexes.FirstOrDefault(i => i.GetDatabaseName() == "IX_SongRequestHistories_SongId_RequestDate");
            
            Assert.NotNull(songRequestIndex);
            Assert.Equal(2, songRequestIndex.Properties.Count);
            Assert.Contains(songRequestIndex.Properties, p => p.Name == "SongId");
            Assert.Contains(songRequestIndex.Properties, p => p.Name == "RequestDate");
        }

        [Fact]
        public async Task GetRequestedCountForSong_ReturnsZero_WhenNoHistory()
        {
            var result = await _repository.GetRequestedCountForSong("nonexistent-song-id");
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GetRequestedCountForSong_ReturnsCorrectCount()
        {
            var songId = "song123";
            var histories = new[]
            {
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song 1", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-1) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song 1", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-2) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song 1", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-3) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "other-song", Title = "Other Song", Duration = TimeSpan.FromMinutes(4), RequestDate = DateTime.UtcNow },
            };

            _context.SongRequestHistories.AddRange(histories);
            await _context.SaveChangesAsync();

            var result = await _repository.GetRequestedCountForSong(songId);
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task GetRequestedCountForSong_RespectsMonthsFilter()
        {
            var songId = "song456";
            var histories = new[]
            {
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song 1", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-1) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song 1", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddMonths(-2) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = songId, Title = "Song 1", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddMonths(-6) },
            };

            _context.SongRequestHistories.AddRange(histories);
            await _context.SaveChangesAsync();

            var resultOneMonth = await _repository.GetRequestedCountForSong(songId, numberOfMonths: 1);
            var resultThreeMonths = await _repository.GetRequestedCountForSong(songId, numberOfMonths: 3);
            var resultZeroMonths = await _repository.GetRequestedCountForSong(songId, numberOfMonths: 0);

            Assert.Equal(1, resultOneMonth);
            Assert.Equal(2, resultThreeMonths);
            Assert.Equal(3, resultZeroMonths);
        }

        [Fact]
        public async Task GetTopRequestedSongs_ReturnsTopRequestedSongs()
        {
            var histories = new[]
            {
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song1", Title = "Song One", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song1", Title = "Song One", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song1", Title = "Song One", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song2", Title = "Song Two", Duration = TimeSpan.FromMinutes(4), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song3", Title = "Song Three", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow },
            };

            _context.SongRequestHistories.AddRange(histories);
            await _context.SaveChangesAsync();

            var result = await _repository.GetTopRequestedSongs(2);

            Assert.Equal(2, result.Count);
            Assert.Equal("song1", result[0].SongId);
            Assert.Equal(3, result[0].RequestedCount);
            Assert.Equal("song2", result[1].SongId);
            Assert.Equal(1, result[1].RequestedCount);
        }

        [Fact]
        public async Task QuerySongRequestHistoryLimitedByMonths_ReturnsWithLimitAndOffset()
        {
            var histories = new[]
            {
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song1", Title = "Song One", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-1) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song1", Title = "Song One", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddDays(-2) },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song2", Title = "Song Two", Duration = TimeSpan.FromMinutes(4), RequestDate = DateTime.UtcNow.AddDays(-3) },
            };

            _context.SongRequestHistories.AddRange(histories);
            await _context.SaveChangesAsync();

            var result = await _repository.QuerySongRequestHistoryLimitedByMonths(limit: 10, offset: 0);

            Assert.True(result.Count > 0);
        }

        [Fact]
        public async Task CountDistinctSongsLimitedByMonths_ReturnsCorrectCount()
        {
            var histories = new[]
            {
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song1", Title = "Song One", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song2", Title = "Song Two", Duration = TimeSpan.FromMinutes(4), RequestDate = DateTime.UtcNow },
                new SongRequestHistory { Id = Guid.NewGuid().ToString(), SongId = "song3", Title = "Song Three", Duration = TimeSpan.FromMinutes(3), RequestDate = DateTime.UtcNow.AddMonths(-2) },
            };

            _context.SongRequestHistories.AddRange(histories);
            await _context.SaveChangesAsync();

            var result = await _repository.CountDistinctSongsLimitedByMonths(numberOfMonths: 1);

            Assert.Equal(2, result);
        }
    }
}