using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository.Repositories;
using Xunit;

namespace PenguinTwitchBot.Test.Database.Repositories
{
    public class VoiceTableSeparationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly RegisteredVoiceRepository _registeredVoiceRepo;
        private readonly UserRegisteredVoicesRepository _userRegisteredVoiceRepo;

        public VoiceTableSeparationTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();
            _registeredVoiceRepo = new RegisteredVoiceRepository(_context);
            _userRegisteredVoiceRepo = new UserRegisteredVoicesRepository(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task RegisteredVoices_And_UserRegisteredVoices_AreStrictlyIsolated()
        {
            // Arrange
            var globalVoice = new RegisteredVoice
            {
                Name = "af_heart",
                Type = BaseVoice.VoiceType.Kokoro,
                LanguageCode = "en-US",
                Sex = BaseVoice.SexType.Female
            };

            var userVoice = new UserRegisteredVoice
            {
                Username = "testuser",
                Name = "bm_daniel",
                Type = BaseVoice.VoiceType.Kokoro,
                LanguageCode = "en-GB",
                Sex = BaseVoice.SexType.Male
            };

            await _registeredVoiceRepo.AddAsync(globalVoice);
            await _userRegisteredVoiceRepo.AddAsync(userVoice);
            await _context.SaveChangesAsync();

            // Act
            var registeredVoices = (await _registeredVoiceRepo.GetAllAsync()).ToList();
            var userVoices = (await _userRegisteredVoiceRepo.GetAllAsync()).ToList();

            // Assert
            Assert.Single(registeredVoices);
            Assert.IsType<RegisteredVoice>(registeredVoices[0]);
            Assert.Equal("af_heart", registeredVoices[0].Name);

            Assert.Single(userVoices);
            Assert.IsType<UserRegisteredVoice>(userVoices[0]);
            Assert.Equal("testuser", userVoices[0].Username);
            Assert.Equal("bm_daniel", userVoices[0].Name);
        }

        [Fact]
        public async Task QueryingRegisteredVoices_DoesNotReturnUserVoicesWithSameName()
        {
            // Arrange
            var userVoice = new UserRegisteredVoice
            {
                Username = "alice",
                Name = "af_sky",
                Type = BaseVoice.VoiceType.Kokoro,
                LanguageCode = "en-US",
                Sex = BaseVoice.SexType.Female
            };

            await _userRegisteredVoiceRepo.AddAsync(userVoice);
            await _context.SaveChangesAsync();

            // Act
            var matchInRegisteredVoices = await _registeredVoiceRepo.Find(x => x.Name == "af_sky").FirstOrDefaultAsync();
            var matchInUserVoices = await _userRegisteredVoiceRepo.Find(x => x.Name == "af_sky").FirstOrDefaultAsync();

            // Assert: RegisteredVoices table does not see the user voice
            Assert.Null(matchInRegisteredVoices);
            Assert.NotNull(matchInUserVoices);
            Assert.Equal("alice", matchInUserVoices.Username);
        }

        [Fact]
        public async Task DeletingRegisteredVoice_DoesNotAffectUserRegisteredVoices()
        {
            // Arrange
            var globalVoice = new RegisteredVoice
            {
                Name = "shared_name",
                Type = BaseVoice.VoiceType.Kokoro,
                LanguageCode = "en-US",
                Sex = BaseVoice.SexType.Female
            };

            var userVoice = new UserRegisteredVoice
            {
                Username = "bob",
                Name = "shared_name",
                Type = BaseVoice.VoiceType.Kokoro,
                LanguageCode = "en-US",
                Sex = BaseVoice.SexType.Female
            };

            await _registeredVoiceRepo.AddAsync(globalVoice);
            await _userRegisteredVoiceRepo.AddAsync(userVoice);
            await _context.SaveChangesAsync();

            // Act: delete global voice
            _registeredVoiceRepo.Remove(globalVoice);
            await _context.SaveChangesAsync();

            // Assert: user voice remains intact
            var remainingRegistered = (await _registeredVoiceRepo.GetAllAsync()).ToList();
            var remainingUser = (await _userRegisteredVoiceRepo.GetAllAsync()).ToList();

            Assert.Empty(remainingRegistered);
            Assert.Single(remainingUser);
            Assert.Equal("bob", remainingUser[0].Username);
        }
    }
}

