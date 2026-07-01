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

    [Fact]
    public async Task BackupDatabase_WithOrphans_LogsSummaryAndIndividualWarnings()
    {
        var orphan1 = "/backups/temp-orphan1";
        var orphan2 = "/backups/temp-orphan2";
        _fs.AddDirectory(orphan1);
        _fs.AddDirectory(orphan2);

        await _sut.BackupDatabase(_context, _backupDir, _logger);

        Assert.True(HasLog(LogLevel.Warning, "Found 2 orphaned temp directories"));
        Assert.True(HasLog(LogLevel.Warning, "Orphaned temp directory detected: " + orphan1));
        Assert.True(HasLog(LogLevel.Warning, "Orphaned temp directory detected: " + orphan2));
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

public class TestLogger : ILogger<BackupTools>
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state) => null;
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
