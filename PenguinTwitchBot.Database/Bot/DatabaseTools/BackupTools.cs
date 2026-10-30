using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Repository.Repositories;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    public class BackupTools : IBackupTools
    {
        private const string BackupChatHistoryMonthsToKeepSetting = "BackupChatHistoryMonthsToKeep";
        private const int RestoreBatchSize = 1000;

        private readonly IFileSystem _fs;
        private readonly ILogger _logger;
        private readonly IZipService _zipService;

        public string BackupDirectory { get; }

        private static readonly string[] WwwrootSubdirectories = ["audio", "fishes", "gifs", "sfx"];

        private static readonly MethodInfo BackupTableGenericMethod = typeof(BackupTools)
            .GetMethod(nameof(BackupEntityTableInternalAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly MethodInfo RestoreTableGenericMethod = typeof(BackupTools)
            .GetMethod(nameof(RestoreEntityTableInternalAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        public BackupTools(IFileSystem fileSystem, ILogger<BackupTools> logger, IZipService zipService)
        {
            _fs = fileSystem;
            _logger = logger;
            _zipService = zipService;
            BackupDirectory = _fs.Path.Combine(_fs.Directory.GetCurrentDirectory(), "Data", "backups");
        }

        public List<FileInfo> GetBackupFiles(string backupDirectory)
        {
            if (!_fs.Directory.Exists(backupDirectory))
            {
                return [];
            }

            return _fs.Directory.GetFiles(backupDirectory, "*.zip")
                .Select(x => new FileInfo(x))
                .ToList();
        }

        public Task DeleteOldBackupsAsync(string directory, int maxCount, int maxDays, ILogger? logger)
        {
            if (!_fs.Directory.Exists(directory))
                return Task.CompletedTask;

            var files = _fs.Directory.GetFiles(directory, "*.zip")
                .Select(x => new FileInfo(x))
                .OrderBy(f => f.CreationTime)
                .ToList();

            var now = DateTime.Now;
            var toDelete = new List<FileInfo>();
            var remaining = new List<FileInfo>();

            foreach (var f in files)
            {
                if (f.CreationTime < now.AddDays(-maxDays))
                    toDelete.Add(f);
                else
                    remaining.Add(f);
            }

            while (remaining.Count > maxCount)
            {
                var oldest = remaining.MinBy(f => f.CreationTime)!;
                toDelete.Add(oldest);
                remaining.Remove(oldest);
            }

            foreach (var file in toDelete)
            {
                try
                {
                    _fs.File.Delete(file.FullName);
                    logger?.LogInformation("Deleted old backup: {Name} ({Date})", file.Name, file.CreationTime);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to delete old backup: {Name}", file.Name);
                }
            }

            return Task.CompletedTask;
        }

        public static JsonSerializerOptions CreateBackupJsonOptions(DbContext? context = null, bool writeIndented = true)
        {
            var resolver = new SubActionTypeResolver();
            if (context != null)
            {
                resolver.Modifiers.Add(typeInfo =>
                {
                    if (typeInfo.Kind != JsonTypeInfoKind.Object)
                        return;

                    var entityType = context.Model.FindEntityType(typeInfo.Type);
                    if (entityType == null)
                        return;

                    var mappedPropertyNames = entityType.GetProperties()
                        .Where(p => !p.IsShadowProperty())
                        .Select(p => p.Name)
                        .ToHashSet(StringComparer.Ordinal);

                    for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
                    {
                        var prop = typeInfo.Properties[i];
                        var clrName = (prop.AttributeProvider as MemberInfo)?.Name ?? prop.Name;
                        if (!mappedPropertyNames.Contains(clrName))
                        {
                            typeInfo.Properties.RemoveAt(i);
                        }
                        else
                        {
                            prop.ShouldSerialize = null;
                        }
                    }
                });
            }

            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                WriteIndented = writeIndented,
                TypeInfoResolver = resolver
            };

            options.Converters.Add(new LegacyIntCompatibleGuidJsonConverter());
            options.Converters.Add(new LegacyIntCompatibleNullableGuidJsonConverter());
            return options;
        }

        /// <summary>
        /// Discovers all concrete, non-excluded, table-mapped entity types in the DbContext model
        /// and orders them topologically so parent tables are inserted before child tables.
        /// </summary>
        public static IReadOnlyList<IEntityType> GetOrderedBackupEntityTypes(DbContext context)
        {
            var eligibleEntities = context.Model.GetEntityTypes()
                .Where(et => !et.IsOwned()
                    && !et.ClrType.IsAbstract
                    && et.FindPrimaryKey() != null
                    && !string.IsNullOrWhiteSpace(et.GetTableName())
                    && et.GetViewName() == null
                    && et.ClrType.GetCustomAttribute<ExcludeFromBackupAttribute>() == null)
                .OrderBy(et => et.ClrType.Name, StringComparer.Ordinal)
                .ToList();

            var eligibleSet = eligibleEntities.ToHashSet();
            var dependents = eligibleEntities.ToDictionary(et => et, _ => new HashSet<IEntityType>());
            var inDegree = eligibleEntities.ToDictionary(et => et, _ => 0);

            foreach (var entityType in eligibleEntities)
            {
                foreach (var fk in entityType.GetForeignKeys())
                {
                    var principal = fk.PrincipalEntityType;
                    if (principal != entityType && eligibleSet.Contains(principal))
                    {
                        if (dependents[principal].Add(entityType))
                        {
                            inDegree[entityType]++;
                        }
                    }
                }
            }

            var ready = new SortedSet<IEntityType>(
                eligibleEntities.Where(et => inDegree[et] == 0),
                Comparer<IEntityType>.Create((a, b) => string.CompareOrdinal(a.ClrType.FullName, b.ClrType.FullName)));

            var sorted = new List<IEntityType>(eligibleEntities.Count);
            while (ready.Count > 0)
            {
                var current = ready.Min!;
                ready.Remove(current);
                sorted.Add(current);

                foreach (var dep in dependents[current])
                {
                    inDegree[dep]--;
                    if (inDegree[dep] == 0)
                    {
                        ready.Add(dep);
                    }
                }
            }

            // If any cyclic FKs exist, append remaining types deterministically
            if (sorted.Count < eligibleEntities.Count)
            {
                var visited = sorted.ToHashSet();
                foreach (var remaining in eligibleEntities.Where(et => !visited.Contains(et)))
                {
                    sorted.Add(remaining);
                }
            }

            return sorted;
        }

        public async Task BackupTable<T>(DbContext context, string backupDirectory, ILogger? logger) where T : class
        {
            var options = CreateBackupJsonOptions(context, writeIndented: true);
            await BackupEntityTableInternalAsync<T>(context, backupDirectory, options, logger);
        }

        public async Task WriteData<T>(string backupDirectory, List<T> records, ILogger? logger)
        {
            var options = CreateBackupJsonOptions(null, writeIndented: true);
            var fileName = _fs.Path.Combine(backupDirectory, $"{typeof(T).Name}.json");
            await using var fileStream = _fs.File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fileStream, records, options);
            logger?.LogDebug("Backed up {Count} records to {Name}", records.Count, typeof(T).Name);
        }

        public async Task RestoreTable<T>(DbContext context, string backupDirectory, ILogger? logger) where T : class
        {
            var options = CreateBackupJsonOptions(context, writeIndented: false);
            await DeleteEntityTableAsync(context, typeof(T));
            context.ChangeTracker.Clear();
            await RestoreEntityTableInternalAsync<T>(context, backupDirectory, options, logger);
        }

        private async Task<BackupTableManifestEntry> BackupEntityTableInternalAsync<T>(
            DbContext context,
            string backupDirectory,
            JsonSerializerOptions options,
            ILogger? logger) where T : class
        {
            var entityType = context.Model.FindEntityType(typeof(T));
            var shortName = $"{typeof(T).Name}.json";
            var fileName = _fs.Path.Combine(backupDirectory, shortName);
            var tempFileName = fileName + ".tmp";

            IQueryable<T> query = context.Set<T>().AsNoTracking().IgnoreAutoIncludes();

            if (typeof(T) == typeof(ViewerChatHistory))
            {
                var monthsToKeep = await context.Set<Setting>()
                    .AsNoTracking()
                    .Where(x => x.Name == BackupChatHistoryMonthsToKeepSetting)
                    .Select(x => (int?)x.IntSetting)
                    .FirstOrDefaultAsync() ?? 12;

                if (monthsToKeep <= 0)
                {
                    query = query.Where(_ => false);
                }
                else
                {
                    var cutoff = DateTime.UtcNow.AddMonths(-monthsToKeep);
                    query = (IQueryable<T>)(object)((IQueryable<ViewerChatHistory>)query)
                        .Where(x => x.CreatedAt != null && x.CreatedAt > cutoff);
                }
            }

            try
            {
                var count = 0;
                await using (var fileStream = _fs.File.Open(tempFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var writer = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = options.WriteIndented }))
                {
                    writer.WriteStartArray();
                    await foreach (var record in query.AsAsyncEnumerable())
                    {
                        JsonSerializer.Serialize(writer, record, options);
                        count++;
                        if (count % 500 == 0)
                        {
                            await writer.FlushAsync();
                        }
                    }
                    writer.WriteEndArray();
                    await writer.FlushAsync();
                    await fileStream.FlushAsync();
                }

                if (_fs.File.Exists(fileName))
                {
                    _fs.File.Delete(fileName);
                }
                _fs.File.Move(tempFileName, fileName);

                var sha256 = await ComputeFileSha256Async(fileName);
                logger?.LogDebug("Backed up {Count} records to {Name}", count, shortName);

                return new BackupTableManifestEntry
                {
                    EntityTypeName = typeof(T).Name,
                    TableName = entityType?.GetTableName() ?? typeof(T).Name,
                    FileName = shortName,
                    RowCount = count,
                    Sha256 = sha256
                };
            }
            catch
            {
                if (_fs.File.Exists(tempFileName))
                {
                    _fs.File.Delete(tempFileName);
                }
                throw;
            }
        }

        private async Task<int> RestoreEntityTableInternalAsync<T>(
            DbContext context,
            string backupDirectory,
            JsonSerializerOptions options,
            ILogger? logger) where T : class
        {
            var shortName = $"{typeof(T).Name}.json";
            var fileName = _fs.Path.Combine(backupDirectory, shortName);
            if (!_fs.File.Exists(fileName))
            {
                return 0;
            }

            await using var fileStream = _fs.File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var batch = new List<T>(RestoreBatchSize);
            var totalCount = 0;

            await foreach (var record in JsonSerializer.DeserializeAsyncEnumerable<T>(fileStream, options))
            {
                if (record == null)
                {
                    continue;
                }

                if (record is Bot.Actions.SubActions.Types.LogicIfElseType logic)
                {
                    MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(logic.TrueSubActions);
                    MinigameSubActionMigrationHelper.UpgradeLegacySubActionsInList(logic.FalseSubActions);
                }

                batch.Add(record);
                totalCount++;

                if (batch.Count >= RestoreBatchSize)
                {
                    context.Set<T>().AddRange(batch);
                    await context.SaveChangesAsync();
                    context.ChangeTracker.Clear();
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                context.Set<T>().AddRange(batch);
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
                batch.Clear();
            }

            logger?.LogDebug("Restored {Count} records from {Name}", totalCount, shortName);
            return totalCount;
        }

        private static async Task DeleteEntityTableAsync(DbContext context, Type clrType)
        {
            var entityType = context.Model.FindEntityType(clrType);
            var tableName = entityType?.GetTableName();

            if (!string.IsNullOrWhiteSpace(tableName))
            {
                var sqlHelper = context.Database.GetService<ISqlGenerationHelper>();
                var delimitedTable = sqlHelper != null
                    ? sqlHelper.DelimitIdentifier(tableName, entityType?.GetSchema())
                    : $"\"{tableName.Replace("\"", "\"\"")}\"";

#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync($"DELETE FROM {delimitedTable}");
#pragma warning restore EF1002
            }
        }

        public async Task<string> BackupDatabase(DbContext context, string backupDirectory, ILogger logger, string filePrefix = "backup")
        {
            logger.LogInformation("Backing up database");
            var tempDirectory = _fs.Path.Combine(backupDirectory, $"temp-{Guid.NewGuid():N}");
            if (!_fs.Directory.Exists(backupDirectory))
            {
                _fs.Directory.CreateDirectory(backupDirectory);
            }

            _fs.Directory.CreateDirectory(tempDirectory);

            try
            {
                var manifest = new BackupManifest
                {
                    FormatVersion = BackupManifest.CurrentFormatVersion,
                    CreatedAtUtc = DateTime.UtcNow,
                    DatabaseProvider = context.Database.ProviderName
                };

                try
                {
                    var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
                    manifest.LatestMigrationId = appliedMigrations.LastOrDefault();
                }
                catch
                {
                    // In-memory or non-migrated test databases may not have a migrations history table
                }

                var options = CreateBackupJsonOptions(context, writeIndented: true);
                var entityTypes = GetOrderedBackupEntityTypes(context);

                foreach (var entityType in entityTypes)
                {
                    var method = BackupTableGenericMethod.MakeGenericMethod(entityType.ClrType);
                    var task = (Task<BackupTableManifestEntry>)method.Invoke(this, [context, tempDirectory, options, logger])!;
                    var entry = await task;
                    manifest.Tables[entityType.ClrType.Name] = entry;
                }

                await BackupWwwrootDirectoriesAsync(tempDirectory, manifest, logger);

                var manifestPath = _fs.Path.Combine(tempDirectory, BackupManifest.ManifestFileName);
                await using (var manifestStream = _fs.File.Open(manifestPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true });
                    await manifestStream.FlushAsync();
                }

                var zipPath = _fs.Path.Combine(
                    backupDirectory,
                    $"{filePrefix}-{DateTime.Now:yyyy-MM-dd--HH-mm-ss}-{Guid.NewGuid().ToString("N")[..6]}.zip");
                _zipService.CreateFromDirectory(tempDirectory, zipPath);
                logger.LogInformation("Backup created at {ZipPath}", zipPath);
                return zipPath;
            }
            finally
            {
                if (_fs.Directory.Exists(tempDirectory))
                {
                    try
                    {
                        _fs.Directory.Delete(tempDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to clean up temporary backup directory {TempDirectory}", tempDirectory);
                    }
                }
            }
        }

        public BackupManifest ValidateBackupDirectory(string backupDirectory)
        {
            if (!_fs.Directory.Exists(backupDirectory))
            {
                throw new DirectoryNotFoundException($"Backup directory not found: {backupDirectory}");
            }

            var manifestPath = _fs.Path.Combine(backupDirectory, BackupManifest.ManifestFileName);
            if (!_fs.File.Exists(manifestPath))
            {
                throw new InvalidOperationException(
                    "Backup archive does not contain a manifest.json file. Legacy integer-ID (v1) backups cannot be restored into a UUIDv7 (v2) database.");
            }

            using var manifestStream = _fs.File.Open(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestStream)
                ?? throw new InvalidDataException("Failed to deserialize manifest.json.");

            if (manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported backup format version {manifest.FormatVersion}. Expected version {BackupManifest.CurrentFormatVersion}.");
            }

            foreach (var (_, tableEntry) in manifest.Tables)
            {
                var tableFilePath = _fs.Path.Combine(backupDirectory, tableEntry.FileName);
                if (!_fs.File.Exists(tableFilePath))
                {
                    throw new FileNotFoundException(
                        $"Backup is missing expected table file '{tableEntry.FileName}'.", tableFilePath);
                }

                if (!string.IsNullOrWhiteSpace(tableEntry.Sha256))
                {
                    var actualHash = ComputeFileSha256(tableFilePath);
                    if (!string.Equals(actualHash, tableEntry.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"Checksum mismatch for '{tableEntry.FileName}'. Expected {tableEntry.Sha256}, got {actualHash}.");
                    }
                }
            }

            foreach (var (_, mediaEntry) in manifest.MediaFiles)
            {
                var normalizedRelative = mediaEntry.RelativePath.Replace('/', _fs.Path.DirectorySeparatorChar);
                var mediaFilePath = _fs.Path.Combine(backupDirectory, normalizedRelative);
                if (!_fs.File.Exists(mediaFilePath))
                {
                    throw new FileNotFoundException(
                        $"Backup is missing expected media file '{mediaEntry.RelativePath}'.", mediaFilePath);
                }

                if (!string.IsNullOrWhiteSpace(mediaEntry.Sha256))
                {
                    var actualHash = ComputeFileSha256(mediaFilePath);
                    if (!string.Equals(actualHash, mediaEntry.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"Checksum mismatch for media file '{mediaEntry.RelativePath}'. Expected {mediaEntry.Sha256}, got {actualHash}.");
                    }
                }
            }

            return manifest;
        }

        // Reads only manifest.json (no checksum or entry verification) for fast listing.
        public BackupManifest ReadBackupManifest(string zipFilePath)
        {
            using var fileStream = _fs.File.Open(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

            var manifestEntry = archive.GetEntry(BackupManifest.ManifestFileName)
                ?? throw new InvalidOperationException("Backup archive does not contain a manifest.json file.");

            using var entryStream = manifestEntry.Open();
            var manifest = JsonSerializer.Deserialize<BackupManifest>(entryStream)
                ?? throw new InvalidDataException("Failed to deserialize manifest.json from archive.");

            if (manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
            {
                throw new InvalidOperationException($"Unsupported backup format version {manifest.FormatVersion}.");
            }

            return manifest;
        }

        public BackupManifest ValidateBackupArchive(string zipFilePath)
        {
            if (!_fs.File.Exists(zipFilePath))
            {
                throw new FileNotFoundException("Backup archive file not found.", zipFilePath);
            }

            using var fileStream = _fs.File.Open(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

            var manifestEntry = archive.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName, BackupManifest.ManifestFileName, StringComparison.OrdinalIgnoreCase));

            if (manifestEntry == null)
            {
                throw new InvalidOperationException(
                    "Backup archive does not contain a manifest.json file. Legacy integer-ID (v1) backups cannot be restored into a UUIDv7 (v2) database.");
            }

            BackupManifest manifest;
            using (var entryStream = manifestEntry.Open())
            {
                manifest = JsonSerializer.Deserialize<BackupManifest>(entryStream)
                    ?? throw new InvalidDataException("Failed to deserialize manifest.json from archive.");
            }

            if (manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported backup format version {manifest.FormatVersion}. Expected version {BackupManifest.CurrentFormatVersion}.");
            }

            var entryLookup = archive.Entries.ToDictionary(
                e => e.FullName.Replace('\\', '/'),
                e => e,
                StringComparer.OrdinalIgnoreCase);

            foreach (var (_, tableEntry) in manifest.Tables)
            {
                var key = tableEntry.FileName.Replace('\\', '/');
                if (!entryLookup.TryGetValue(key, out var zipEntry))
                {
                    throw new FileNotFoundException($"Backup archive is missing expected table file '{tableEntry.FileName}'.");
                }

                if (!string.IsNullOrWhiteSpace(tableEntry.Sha256))
                {
                    using var stream = zipEntry.Open();
                    var actualHash = Convert.ToHexStringLower(SHA256.HashData(stream));
                    if (!string.Equals(actualHash, tableEntry.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"Checksum mismatch in archive for '{tableEntry.FileName}'. Expected {tableEntry.Sha256}, got {actualHash}.");
                    }
                }
            }

            return manifest;
        }

        public async Task RestoreDatabase(DbContext context, string backupDirectory, ILogger? logger)
        {
            await RestoreDatabase(context, backupDirectory, logger, createPreRestoreBackup: true);
        }

        public async Task RestoreDatabase(DbContext context, string backupDirectory, ILogger? logger, bool createPreRestoreBackup = true)
        {
            logger?.LogInformation("Validating backup directory before restore: {BackupDirectory}", backupDirectory);

            var manifestPath = _fs.Path.Combine(backupDirectory, BackupManifest.ManifestFileName);
            if (_fs.File.Exists(manifestPath))
            {
                ValidateBackupDirectory(backupDirectory);
            }
            else
            {
                EnsureDirectoryFilesAreValidJson(backupDirectory);
            }

            if (createPreRestoreBackup && !string.IsNullOrWhiteSpace(BackupDirectory))
            {
                try
                {
                    logger?.LogInformation("Creating automatic pre-restore safety backup...");
                    await BackupDatabase(context, BackupDirectory, logger ?? _logger, filePrefix: "pre_restore");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Could not create automatic pre-restore safety backup before restore");
                }
            }

            logger?.LogInformation("Restoring database inside atomic transaction");

            await context.Database.OpenConnectionAsync();
            await SetForeignKeysAsync(context, false, logger);

            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    await RestoreDatabaseCoreAsync(context, backupDirectory, logger);
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Database restore failed; rolling back transaction");
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                try
                {
                    await SetForeignKeysAsync(context, true, logger);
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }
            }

            RestoreWwwrootDirectories(backupDirectory, logger);
            logger?.LogInformation("Database restored successfully");
        }

        private void EnsureDirectoryFilesAreValidJson(string backupDirectory)
        {
            if (!_fs.Directory.Exists(backupDirectory))
            {
                throw new DirectoryNotFoundException($"Backup directory not found: {backupDirectory}");
            }

            foreach (var jsonFile in _fs.Directory.GetFiles(backupDirectory, "*.json"))
            {
                using var stream = _fs.File.Open(jsonFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException($"Backup file '{jsonFile}' does not contain a root JSON array.");
                }
            }
        }

        private async Task RestoreDatabaseCoreAsync(DbContext context, string backupDirectory, ILogger? logger)
        {
            var options = CreateBackupJsonOptions(context, writeIndented: false);
            var insertOrder = GetOrderedBackupEntityTypes(context);
            var deleteOrder = insertOrder.Reverse().ToList();

            context.ChangeTracker.Clear();

            // Phase 1: Delete all backup-eligible tables in reverse topological order (children before parents)
            foreach (var entityType in deleteOrder)
            {
                await DeleteEntityTableAsync(context, entityType.ClrType);
            }

            context.ChangeTracker.Clear();

            // Phase 2: Stream-insert all backup-eligible tables in topological order (parents before children)
            foreach (var entityType in insertOrder)
            {
                var method = RestoreTableGenericMethod.MakeGenericMethod(entityType.ClrType);
                var task = (Task<int>)method.Invoke(this, [context, backupDirectory, options, logger])!;
                await task;
            }
        }

        private async Task BackupWwwrootDirectoriesAsync(string tempDirectory, BackupManifest manifest, ILogger? logger)
        {
            var wwwrootSource = _fs.Path.Combine(_fs.Directory.GetCurrentDirectory(), "wwwroot");
            foreach (var subDir in WwwrootSubdirectories)
            {
                try
                {
                    var sourcePath = _fs.Path.Combine(wwwrootSource, subDir);
                    if (!_fs.Directory.Exists(sourcePath))
                    {
                        logger?.LogDebug("Skipping wwwroot/{SubDir} (does not exist)", subDir);
                        continue;
                    }
                    var destPath = _fs.Path.Combine(tempDirectory, "wwwroot", subDir);
                    await CopyDirectoryWithManifestAsync(sourcePath, destPath, $"wwwroot/{subDir}", manifest, logger);
                    logger?.LogDebug("Backed up wwwroot/{SubDir}", subDir);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to backup wwwroot/{SubDir}", subDir);
                }
            }
        }

        private void RestoreWwwrootDirectories(string backupDirectory, ILogger? logger)
        {
            var wwwrootBackup = _fs.Path.Combine(backupDirectory, "wwwroot");
            if (!_fs.Directory.Exists(wwwrootBackup))
            {
                logger?.LogDebug("No wwwroot directory found in backup, skipping media restore");
                return;
            }
            var wwwrootDest = _fs.Path.Combine(_fs.Directory.GetCurrentDirectory(), "wwwroot");
            foreach (var subDir in WwwrootSubdirectories)
            {
                try
                {
                    var sourcePath = _fs.Path.Combine(wwwrootBackup, subDir);
                    if (!_fs.Directory.Exists(sourcePath))
                    {
                        logger?.LogDebug("No wwwroot/{SubDir} found in backup, skipping", subDir);
                        continue;
                    }
                    var destPath = _fs.Path.Combine(wwwrootDest, subDir);
                    if (_fs.Directory.Exists(destPath))
                    {
                        try
                        {
                            _fs.Directory.Delete(destPath, true);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Could not clear wwwroot/{SubDir}, overwriting in place", subDir);
                        }
                    }
                    CopyDirectory(sourcePath, destPath, logger);
                    logger?.LogDebug("Restored wwwroot/{SubDir}", subDir);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to restore wwwroot/{SubDir}", subDir);
                }
            }
        }

        private async Task CopyDirectoryWithManifestAsync(
            string sourceDir,
            string destDir,
            string relativePrefix,
            BackupManifest manifest,
            ILogger? logger)
        {
            _fs.Directory.CreateDirectory(destDir);
            foreach (var file in _fs.Directory.GetFiles(sourceDir))
            {
                try
                {
                    var fileName = _fs.Path.GetFileName(file);
                    var destFile = _fs.Path.Combine(destDir, fileName);
                    _fs.File.Copy(file, destFile, overwrite: true);

                    var relativePath = $"{relativePrefix}/{fileName}";
                    var fileInfo = _fs.FileInfo.New(destFile);
                    var sha256 = await ComputeFileSha256Async(destFile);
                    manifest.MediaFiles[relativePath] = new BackupMediaManifestEntry
                    {
                        RelativePath = relativePath,
                        SizeBytes = fileInfo.Length,
                        Sha256 = sha256
                    };
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to copy file {File}, skipping", file);
                }
            }
        }

        private void CopyDirectory(string sourceDir, string destDir, ILogger? logger)
        {
            _fs.Directory.CreateDirectory(destDir);
            foreach (var file in _fs.Directory.GetFiles(sourceDir))
            {
                try
                {
                    var destFile = _fs.Path.Combine(destDir, _fs.Path.GetFileName(file));
                    _fs.File.Copy(file, destFile, overwrite: true);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to copy file {File}, skipping", file);
                }
            }
        }

        private async Task<string> ComputeFileSha256Async(string filePath)
        {
            await using var stream = _fs.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hashBytes = await SHA256.HashDataAsync(stream);
            return Convert.ToHexStringLower(hashBytes);
        }

        private string ComputeFileSha256(string filePath)
        {
            using var stream = _fs.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hashBytes = SHA256.HashData(stream);
            return Convert.ToHexStringLower(hashBytes);
        }

        private static async Task SetForeignKeysAsync(DbContext context, bool enable, ILogger? logger)
        {
            try
            {
                var isPostgres = context.Database.ProviderName?.Contains("Npgsql") == true;
                var sql = isPostgres
                    ? "SET session_replication_role = 'replica';"
                    : "PRAGMA foreign_keys = OFF;";

                if (isPostgres)
                {
                    await context.Database.ExecuteSqlRawAsync(enable ? "SET session_replication_role = 'origin';" : sql);
                }
                else
                {
                    await context.Database.ExecuteSqlRawAsync(enable ? "PRAGMA foreign_keys = ON;" : sql);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to {State} foreign keys during restore", enable ? "enable" : "disable");
            }
        }
    }
}
