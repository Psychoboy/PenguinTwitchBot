using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PenguinTwitchBot.Database.Bot.DatabaseTools;
using PenguinTwitchBot.Database.Bot.Models.Actions;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Repository;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;

namespace PenguinTwitchBot.Test.Database;

[Collection("BackupTools")]
public class BackupToolsTests : IDisposable
{
    private readonly MockFileSystem _fs;
    private readonly string _backupDir;
    private readonly Mock<IZipService> _zipMock;
    private readonly TestLogger _logger;
    private readonly BackupTools _sut;
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;

    public BackupToolsTests()
    {
        _fs = new MockFileSystem();
        _backupDir = "/backups";
        _fs.AddDirectory(_backupDir);

        _zipMock = new Mock<IZipService>();
        _logger = new TestLogger();
        _sut = new BackupTools(_fs, _logger, _zipMock.Object);

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

    private bool HasLog(LogLevel level, string message)
    {
        return _logger.Entries.Any(e => e.Level == level && e.Message.Contains(message));
    }

    private int CountLogs(LogLevel level)
    {
        return _logger.Entries.Count(e => e.Level == level);
    }

    #region BackupDatabase

    [Fact]
    public async Task BackupDatabase_CreatesTempDir()
    {
        await _sut.BackupDatabase(_context, _backupDir, _logger);

        var tempDirs = _fs.Directory.GetDirectories(_backupDir, "temp-*");
        Assert.Empty(tempDirs);
    }

    [Fact]
    public async Task BackupDatabase_CallsHandlers()
    {
        bool handlerACalled = false;
        bool handlerBCalled = false;

        var originalA = FakeBackupHandlerA.Callback;
        var originalB = FakeBackupHandlerB.Callback;
        FakeBackupHandlerA.Callback = () => handlerACalled = true;
        FakeBackupHandlerB.Callback = () => handlerBCalled = true;

        try
        {
            await _sut.BackupDatabase(_context, _backupDir, _logger);
        }
        finally
        {
            FakeBackupHandlerA.Callback = originalA;
            FakeBackupHandlerB.Callback = originalB;
        }

        Assert.True(handlerACalled, "FakeBackupHandlerA was not called");
        Assert.True(handlerBCalled, "FakeBackupHandlerB was not called");
    }

    [Fact]
    public async Task BackupDatabase_CreatesZipViaZipService()
    {
        await _sut.BackupDatabase(_context, _backupDir, _logger);

        _zipMock.Verify(z => z.CreateFromDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task BackupDatabase_DeletesTempDirAfterZip()
    {
        await _sut.BackupDatabase(_context, _backupDir, _logger);

        var tempDirs = _fs.Directory.GetDirectories(_backupDir, "temp-*");
        Assert.Empty(tempDirs);
    }

    [Fact]
    public async Task BackupDatabase_CreatesBackupDirectoryIfMissing()
    {
        var newDir = "/newbackups";
        Assert.False(_fs.Directory.Exists(newDir));

        await _sut.BackupDatabase(_context, newDir, _logger);

        Assert.True(_fs.Directory.Exists(newDir));
        _zipMock.Verify(z => z.CreateFromDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task BackupDatabase_NoOrphans_LogsNoWarning()
    {
        await _sut.BackupDatabase(_context, _backupDir, _logger);

        Assert.Equal(0, CountLogs(LogLevel.Warning));
    }


    #endregion

    #region BackupTable

    [Fact]
    public async Task BackupTable_WritesJsonFile()
    {
        var action = new ActionType { Id = 1, Name = "TestAction" };
        _context.Actions.Add(action);
        await _context.SaveChangesAsync();

        var tempDir = "/backuptable";
        _fs.AddDirectory(tempDir);

        await _sut.BackupTable<ActionType>(_context, tempDir, _logger);

        var backupFile = "/backuptable/ActionType.json";
        Assert.True(_fs.File.Exists(backupFile));
    }

    [Fact]
    public async Task BackupTable_LogsRecordCount()
    {
        var action = new ActionType { Id = 1, Name = "TestAction" };
        _context.Actions.Add(action);
        await _context.SaveChangesAsync();

        var tempDir = "/backuptablelog";
        _fs.AddDirectory(tempDir);

        await _sut.BackupTable<ActionType>(_context, tempDir, _logger);

        Assert.True(HasLog(LogLevel.Debug, "Backed up 1 records to ActionType"));
    }

    #endregion

    #region WriteData

    [Fact]
    public async Task WriteData_WritesJsonFile()
    {
        var tempDir = "/writedata";
        _fs.AddDirectory(tempDir);

        var records = new List<ActionType>
        {
            new ActionType { Id = 1, Name = "Test1" },
            new ActionType { Id = 2, Name = "Test2" }
        };

        await _sut.WriteData<ActionType>(tempDir, records, _logger);

        var backupFile = "/writedata/ActionType.json";
        Assert.True(_fs.File.Exists(backupFile));
    }

    [Fact]
    public async Task WriteData_LogsRecordCount()
    {
        var tempDir = "/writedatalog";
        _fs.AddDirectory(tempDir);

        var records = new List<ActionType>
        {
            new ActionType { Id = 1, Name = "Test1" }
        };

        await _sut.WriteData<ActionType>(tempDir, records, _logger);

        Assert.True(HasLog(LogLevel.Debug, "Backed up 1 records to ActionType"));
    }

    #endregion

    #region RestoreTable

    [Fact]
    public async Task RestoreTable_Noop_WhenFileMissing()
    {
        await _sut.RestoreTable<ActionType>(_context, _backupDir, _logger);

        Assert.Empty(_context.Actions);
    }

    [Fact]
    public async Task RestoreTable_RestoresRecords()
    {
        var tempDir = "/temprestore";
        _fs.AddDirectory(tempDir);

        _context.Actions.Add(new ActionType { Id = 1, Name = "Existing" });
        await _context.SaveChangesAsync();

        var records = new List<ActionType>
        {
            new ActionType { Id = 2, Name = "Restored" }
        };
        await _sut.WriteData<ActionType>(tempDir, records, _logger);

        await _sut.RestoreTable<ActionType>(_context, tempDir, _logger);
        await _context.SaveChangesAsync();

        var restored = await _context.Actions.FirstAsync(a => a.Id == 2);
        Assert.Equal("Restored", restored.Name);
    }

    [Fact]
    public async Task RestoreTable_Throws_OnDeserializationFailure()
    {
        var tempDir = "/temprestorebad";
        _fs.AddDirectory(tempDir);
        _fs.File.WriteAllText("/temprestorebad/ActionType.json", "not json");

        await Assert.ThrowsAsync<JsonException>(() => _sut.RestoreTable<ActionType>(_context, tempDir, _logger));
    }

    [Fact]
    public async Task RestoreDatabase_RestoresFishingTournamentTables()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fishtourney-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var realZipMock = new Mock<IZipService>();
            var realBackupTools = new BackupTools(new System.IO.Abstractions.FileSystem(), _logger, realZipMock.Object);

            var pointTypes = new List<PenguinTwitchBot.Database.Bot.Models.Points.PointType>
            {
                new()
                {
                    Id = 1,
                    Name = "Tournament Points",
                    Description = "Primary reward pool"
                }
            };

            var fishTypes = new List<PenguinTwitchBot.Database.Bot.Models.Fishing.FishType>
            {
                new()
                {
                    Id = 2,
                    Name = "Golden Carp",
                    BaseWeight = 12.5,
                    BaseGold = 250,
                    ImageFileName = "golden-carp.png",
                    Enabled = true
                }
            };

            var tournaments = new List<PenguinTwitchBot.Database.Bot.Models.Fishing.FishingTournament>
            {
                new()
                {
                    Id = 3,
                    Name = "Summer Splash Cup",
                    Description = "A restored tournament",
                    Enabled = true,
                    Status = PenguinTwitchBot.Database.Bot.Models.Fishing.FishingTournamentStatus.Scheduled,
                    PrimaryScoreCategory = PenguinTwitchBot.Database.Bot.Models.Fishing.FishingTournamentScoreCategory.Largest,
                    StartsAtUtc = DateTime.UtcNow.AddHours(1),
                    EndsAtUtc = DateTime.UtcNow.AddHours(3),
                    EntryFeeAmount = 50,
                    EntryFeePointTypeId = 1
                }
            };

            var eligibleFish = new List<PenguinTwitchBot.Database.Bot.Models.Fishing.FishingTournamentFishType>
            {
                new()
                {
                    Id = 4,
                    FishingTournamentId = 3,
                    FishTypeId = 2
                }
            };

            var rewardRules = new List<PenguinTwitchBot.Database.Bot.Models.Fishing.FishingTournamentRewardRule>
            {
                new()
                {
                    Id = 5,
                    FishingTournamentId = 3,
                    ScoreCategory = PenguinTwitchBot.Database.Bot.Models.Fishing.FishingTournamentScoreCategory.Largest,
                    RewardKind = PenguinTwitchBot.Database.Bot.Models.Fishing.FishingTournamentRewardKind.Points,
                    Placement = 1,
                    Points = 100,
                    PointTypeId = 1,
                    Enabled = true
                }
            };

            await File.WriteAllTextAsync(System.IO.Path.Combine(tempDir, "PointType.json"), JsonSerializer.Serialize(pointTypes, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(System.IO.Path.Combine(tempDir, "FishType.json"), JsonSerializer.Serialize(fishTypes, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(System.IO.Path.Combine(tempDir, "FishingTournament.json"), JsonSerializer.Serialize(tournaments, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(System.IO.Path.Combine(tempDir, "FishingTournamentFishType.json"), JsonSerializer.Serialize(eligibleFish, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(System.IO.Path.Combine(tempDir, "FishingTournamentRewardRule.json"), JsonSerializer.Serialize(rewardRules, new JsonSerializerOptions { WriteIndented = true }));

            await realBackupTools.RestoreDatabase(_context, tempDir, _logger);

            var restoredTournament = await _context.FishingTournaments
                .Include(x => x.EntryFeePointType)
                .Include(x => x.EligibleFish)
                .Include(x => x.RewardRules)
                .SingleAsync();

            Assert.Equal("Summer Splash Cup", restoredTournament.Name);
            Assert.Equal("Tournament Points", restoredTournament.EntryFeePointType?.Name);
            Assert.Single(restoredTournament.EligibleFish);
            Assert.Single(restoredTournament.RewardRules);
            Assert.Single(_context.FishTypes);
            Assert.Single(_context.Set<PenguinTwitchBot.Database.Bot.Models.Points.PointType>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    #endregion

    #region GetBackupFiles

    [Fact]
    public void GetBackupFiles_ReturnsAllZips()
    {
        _fs.AddFile("/backups/backup1.zip", new MockFileData("zip"));
        _fs.AddFile("/backups/backup2.zip", new MockFileData("zip"));
        _fs.AddFile("/backups/readme.txt", new MockFileData("txt"));

        var result = _sut.GetBackupFiles(_backupDir);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetBackupFiles_EmptyDir_ReturnsEmpty()
    {
        var result = _sut.GetBackupFiles(_backupDir);

        Assert.Empty(result);
    }

    #endregion

    #region Fake IBackupDb handlers for reflection discovery

    public class FakeBackupHandlerA : IBackupDb
    {
        public static Action? Callback { get; set; }

        public FakeBackupHandlerA(DbContext context) { }

        public Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            Callback?.Invoke();
            return Task.CompletedTask;
        }

        public Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }
    }

    public class FakeBackupHandlerB : IBackupDb
    {
        public static Action? Callback { get; set; }

        public FakeBackupHandlerB(DbContext context) { }

        public Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            Callback?.Invoke();
            return Task.CompletedTask;
        }

        public Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            return Task.CompletedTask;
        }
    }

    #endregion
}

[CollectionDefinition("BackupTools", DisableParallelization = true)]
public class BackupToolsCollection : ICollectionFixture<object> { }

public class TestLogger : ILogger<BackupTools>
{
    public List<LogEntry> Entries { get; } = new();

#pragma warning disable CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
#pragma warning disable CS8603 // Possible null reference return.
    public IDisposable BeginScope<TState>(TState state) => null;
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry { Level = logLevel, Message = formatter(state, exception) });
    }
}

public class LogEntry
{
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
}
