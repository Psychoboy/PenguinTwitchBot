using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.DatabaseTools;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Models.IpLogs;
using PenguinTwitchBot.Database.Bot.Models.Points;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Security.Cryptography;
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

    #region Backup Tests

    [Fact]
    public async Task BackupDatabase_CreatesZipViaZipService_AndCleansTempDir()
    {
        var action = new ActionType { Id = Guid.CreateVersion7(), Name = "SampleAction" };
        _context.Actions.Add(action);
        await _context.SaveChangesAsync();

        _zipMock.Setup(z => z.CreateFromDirectory(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((src, dst) =>
            {
                // Verify manifest.json exists inside the temp dir during zip
                Assert.True(_fs.File.Exists(_fs.Path.Combine(src, "manifest.json")));
            });

        var zipPath = await _sut.BackupDatabase(_context, _backupDir, _logger);

        Assert.NotNull(zipPath);
        _zipMock.Verify(z => z.CreateFromDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        // Temp directories should be deleted
        var tempDirs = _fs.Directory.GetDirectories(_backupDir, "temp-*");
        Assert.Empty(tempDirs);
    }

    [Fact]
    public async Task BackupDatabase_ExcludesTables_WithExcludeFromBackupAttribute()
    {
        _context.IpLogEntrys.Add(new IpLogEntry { Id = Guid.CreateVersion7().ToString(), Username = "user1", UserId = "1", Ip = "127.0.0.1", ConnectedDate = DateTime.UtcNow });
        _context.SongRequestViewItems.Add(new SongRequestViewItem { Id = Guid.CreateVersion7(), SongId = "abc", Title = "Song 1" });
        _context.Cooldowns.Add(new CurrentCooldowns { Id = Guid.CreateVersion7(), CommandName = "!test", UserName = "user1", IsGlobal = true });
        await _context.SaveChangesAsync();

        _zipMock.Setup(z => z.CreateFromDirectory(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((src, dst) =>
            {
                Assert.False(_fs.File.Exists(_fs.Path.Combine(src, "IpLogEntry.json")));
                Assert.False(_fs.File.Exists(_fs.Path.Combine(src, "SongRequestViewItem.json")));
                Assert.False(_fs.File.Exists(_fs.Path.Combine(src, "CurrentCooldowns.json")));

                var manifestContent = _fs.File.ReadAllText(_fs.Path.Combine(src, "manifest.json"));
                var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestContent);
                Assert.NotNull(manifest);
                Assert.False(manifest.Tables.ContainsKey(nameof(IpLogEntry)));
                Assert.False(manifest.Tables.ContainsKey(nameof(SongRequestViewItem)));
                Assert.False(manifest.Tables.ContainsKey(nameof(CurrentCooldowns)));
            });

        await _sut.BackupDatabase(_context, _backupDir, _logger);
    }

    [Fact]
    public async Task BackupDatabase_FiltersViewerChatHistoryByRetention()
    {
        // Add retention setting: 1 month
        _context.Settings.Add(new Setting
        {
            Name = "BackupChatHistoryMonthsToKeep",
            DataType = Setting.DataTypeEnum.Int,
            IntSetting = 1
        });

        // 1 recent message (kept) and 1 older message (filtered out)
        _context.ViewerChatHistories.Add(new ViewerChatHistory
        {
            Id = Guid.CreateVersion7().ToString(),
            Message = "Recent",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            Username = "user1",
            DisplayName = "User 1"
        });
        _context.ViewerChatHistories.Add(new ViewerChatHistory
        {
            Id = Guid.CreateVersion7().ToString(),
            Message = "Old",
            CreatedAt = DateTime.UtcNow.AddDays(-45),
            Username = "user2",
            DisplayName = "User 2"
        });
        await _context.SaveChangesAsync();

        _zipMock.Setup(z => z.CreateFromDirectory(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((src, dst) =>
            {
                var chatJson = _fs.File.ReadAllText(_fs.Path.Combine(src, "ViewerChatHistory.json"));
                var items = JsonSerializer.Deserialize<List<ViewerChatHistory>>(chatJson);
                Assert.NotNull(items);
                Assert.Single(items);
                Assert.Equal("Recent", items[0].Message);
            });

        await _sut.BackupDatabase(_context, _backupDir, _logger);
    }

    #endregion

    #region Restore & Validation Tests

    [Fact]
    public void ValidateBackupArchive_ThrowsWhenManifestMissing()
    {
        var zipBytes = CreateZipArchiveWithEntries([("somefile.txt", "content")]);
        _fs.File.WriteAllBytes("/backups/invalid.zip", zipBytes);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _sut.ValidateBackupArchive("/backups/invalid.zip"));

        Assert.Contains("manifest.json", ex.Message);
    }

    [Fact]
    public void ValidateBackupArchive_ThrowsWhenChecksumMismatch()
    {
        var manifest = new BackupManifest
        {
            FormatVersion = BackupManifest.CurrentFormatVersion,
            Tables = new Dictionary<string, BackupTableManifestEntry>
            {
                ["ActionType"] = new()
                {
                    EntityTypeName = "ActionType",
                    TableName = "Actions",
                    FileName = "ActionType.json",
                    RowCount = 0,
                    Sha256 = "0000000000000000000000000000000000000000000000000000000000000000" // Corrupt hash
                }
            }
        };

        var zipBytes = CreateZipArchiveWithEntries([
            ("manifest.json", JsonSerializer.Serialize(manifest)),
            ("ActionType.json", "[]")
        ]);
        _fs.File.WriteAllBytes("/backups/corrupt.zip", zipBytes);

        var ex = Assert.Throws<InvalidDataException>(() =>
            _sut.ValidateBackupArchive("/backups/corrupt.zip"));

        Assert.Contains("Checksum mismatch", ex.Message);
    }

    [Fact]
    public async Task RestoreDatabase_CreatesPreRestoreSnapshot_BeforeRestoring()
    {
        // Put data in DB
        _context.Actions.Add(new ActionType { Id = Guid.CreateVersion7(), Name = "BeforeRestoreAction" });
        await _context.SaveChangesAsync();

        var restoreDir = "/restore-stage";
        _fs.AddDirectory(restoreDir);

        var actionAfter = new List<ActionType>
        {
            new() { Id = Guid.CreateVersion7(), Name = "AfterRestoreAction" }
        };
        var actionBytes = JsonSerializer.SerializeToUtf8Bytes(actionAfter);
        _fs.File.WriteAllBytes(_fs.Path.Combine(restoreDir, "ActionType.json"), actionBytes);

        var hash = Convert.ToHexStringLower(SHA256.HashData(actionBytes));
        var manifest = new BackupManifest
        {
            FormatVersion = BackupManifest.CurrentFormatVersion,
            Tables = new Dictionary<string, BackupTableManifestEntry>
            {
                ["ActionType"] = new()
                {
                    EntityTypeName = "ActionType",
                    TableName = "Actions",
                    FileName = "ActionType.json",
                    RowCount = 1,
                    Sha256 = hash
                }
            }
        };
        _fs.File.WriteAllText(_fs.Path.Combine(restoreDir, "manifest.json"), JsonSerializer.Serialize(manifest));

        bool preRestoreSnapshotTaken = false;
        _zipMock.Setup(z => z.CreateFromDirectory(It.IsAny<string>(), It.Is<string>(s => s.Contains("pre_restore"))))
            .Callback<string, string>((src, dst) =>
            {
                preRestoreSnapshotTaken = true;
            });

        await _sut.RestoreDatabase(_context, restoreDir, _logger);

        Assert.True(preRestoreSnapshotTaken);
        var restored = await _context.Actions.ToListAsync();
        Assert.Single(restored);
        Assert.Equal("AfterRestoreAction", restored[0].Name);
    }

    [Fact]
    public async Task RestoreDatabase_PreservesForeignKeys_AndSubActionHierarchies()
    {
        var actionId = Guid.CreateVersion7();
        var targetActionId = Guid.CreateVersion7();
        var pointTypeId = Guid.CreateVersion7();

        var pointType = new PointType { Id = pointTypeId, Name = "ChannelPoints" };
        var targetAction = new ActionType { Id = targetActionId, Name = "TargetSubroutine" };
        var mainAction = new ActionType
        {
            Id = actionId,
            Name = "MainPipeline",
            SubActions =
            [
                new ExecuteActionType
                {
                    Id = Guid.CreateVersion7(),
                    ActionTypeId = actionId,
                    ActionId = targetActionId
                }
            ],
            Triggers =
            [
                new TriggerType
                {
                    Id = Guid.CreateVersion7(),
                    ActionId = actionId,
                    Name = "!pipe",
                    Type = TriggerTypes.Command
                }
            ]
        };

        _context.PointTypes.Add(pointType);
        _context.Actions.AddRange(targetAction, mainAction);
        await _context.SaveChangesAsync();

        // Roundtrip using a real temp folder
        var tempFolder = Path.Combine(Path.GetTempPath(), $"backup-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);

        try
        {
            var realFs = new System.IO.Abstractions.FileSystem();
            var realZip = new ZipService();
            var realBackupTools = new BackupTools(realFs, _logger, realZip);

            var zipPath = await realBackupTools.BackupDatabase(_context, tempFolder, _logger);
            Assert.True(File.Exists(zipPath));

            // Clear database
            _context.SubActions.RemoveRange(_context.SubActions);
            _context.Triggers.RemoveRange(_context.Triggers);
            _context.Actions.RemoveRange(_context.Actions);
            _context.PointTypes.RemoveRange(_context.PointTypes);
            await _context.SaveChangesAsync();
            Assert.Empty(_context.Actions);

            // Extract archive into a stage directory for restore
            var extractDir = Path.Combine(tempFolder, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Restore from backup stage directory
            await realBackupTools.RestoreDatabase(_context, extractDir, _logger);

            // Verify
            var restoredActions = await _context.Actions
                .Include(a => a.SubActions)
                .Include(a => a.Triggers)
                .ToListAsync();

            Assert.Equal(2, restoredActions.Count);
            var mainRestored = restoredActions.Single(a => a.Id == actionId);
            Assert.Equal("MainPipeline", mainRestored.Name);
            Assert.Single(mainRestored.SubActions);
            var execSubAction = Assert.IsType<ExecuteActionType>(mainRestored.SubActions[0]);
            Assert.Equal(targetActionId, execSubAction.ActionId);
            Assert.Single(mainRestored.Triggers);
            Assert.Equal("!pipe", mainRestored.Triggers[0].Name);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    #endregion

    #region Helper & Converter Tests

    [Fact]
    public void LegacyIntCompatibleGuidJsonConverter_DeserializesIntAndGuid()
    {
        var options = new JsonSerializerOptions
        {
            Converters =
            {
                new LegacyIntCompatibleGuidJsonConverter(),
                new LegacyIntCompatibleNullableGuidJsonConverter()
            }
        };

        // Integer 1
        var jsonInt = "1";
        var guidFromInt = JsonSerializer.Deserialize<Guid>(jsonInt, options);
        Assert.Equal(Guid.Parse("00000000-0000-7000-8000-000000000001"), guidFromInt);

        // Integer 42
        var jsonInt42 = "42";
        var guidFromInt42 = JsonSerializer.Deserialize<Guid>(jsonInt42, options);
        Assert.Equal(Guid.Parse("00000000-0000-7000-8000-00000000002a"), guidFromInt42);

        // Standard canonical Guid
        var canonical = Guid.NewGuid();
        var jsonCanonical = $"\"{canonical}\"";
        var parsedCanonical = JsonSerializer.Deserialize<Guid>(jsonCanonical, options);
        Assert.Equal(canonical, parsedCanonical);

        // Nullable Guid
        var jsonNull = "null";
        var nullableGuid = JsonSerializer.Deserialize<Guid?>(jsonNull, options);
        Assert.Null(nullableGuid);
    }

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

    private static byte[] CreateZipArchiveWithEntries((string Name, string Content)[] entries)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(content);
            }
        }
        return memoryStream.ToArray();
    }

    public class TestLogger : ILogger<BackupTools>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
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
}
