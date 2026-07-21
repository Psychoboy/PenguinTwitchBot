 using PenguinTwitchBot.Database.Bot.DatabaseTools;
 using PenguinTwitchBot.Database.Bot.Models.Points;
 
 using Microsoft.EntityFrameworkCore;
 
 namespace PenguinTwitchBot.Database.Repository.Repositories
 {
     public class PointTypesRepository(ApplicationDbContext context, IBackupTools? backupTools = null) : 
         GenericRepository<PointType>(context, backupTools), IPointTypesRepository
     {
         private readonly IBackupTools? _backupTools = backupTools;

         public PointTypesRepository(ApplicationDbContext context) : this(context, null) { }

         public override async Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
         {
             IQueryable<PointType> recordsSet = context
                 .Set<PointType>()
                 .AsNoTracking()
                 .Include(x => x.UserPoints)
                 .Include(x => x.PointCommands)
                 .AsSplitQuery();
             var data = await recordsSet.ToListAsync();
             
             if (_backupTools != null)
             {
                 await _backupTools.WriteData<PointType>(backupDirectory, data, logger);
                 return;
             }

             var fileName = System.IO.Path.Combine(backupDirectory, $"{typeof(PointType).Name}.json");
             await using var fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write,
                 System.IO.FileShare.None, bufferSize: 65536, useAsync: true);
             await System.Text.Json.JsonSerializer.SerializeAsync(fileStream, data, new System.Text.Json.JsonSerializerOptions
             {
                 ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                 WriteIndented = true
             });
             logger?.LogDebug("Backed up {Count} records to {Name}", data.Count, typeof(PointType).Name);
         }

         public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
         {
             if (_backupTools != null)
             {
                 await _backupTools.RestoreTable<PointType>(context, backupDirectory, logger);
                 return;
             }

             try
             {
                 var fileName = System.IO.Path.Combine(backupDirectory, $"{typeof(PointType).Name}.json");
                 if (!System.IO.File.Exists(fileName))
                 {
                     return;
                 }

                 var options = new System.Text.Json.JsonSerializerOptions
                 {
                     ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                 };

                 await using var fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.Open, System.IO.FileAccess.Read,
                     System.IO.FileShare.Read, bufferSize: 65536, useAsync: true);
                 var records = await System.Text.Json.JsonSerializer.DeserializeAsync<List<PointType>>(fileStream, options);
                 if (records == null)
                 {
                     throw new Exception($"{typeof(PointType).Name}.json was null");
                 }

                 // FK enforcement is disabled for restore, so parent deletes do not cascade in SQLite.
                 // Explicitly clear dependent rows first to prevent duplicate child rows on reinsert.
                 await context.Set<PointCommand>().ExecuteDeleteAsync();
                 await context.Set<UserPoints>().ExecuteDeleteAsync();
                 await context.Set<PointType>().ExecuteDeleteAsync();

                 context.Set<PointType>().AddRange(records);
                 logger?.LogDebug("Restored {Count} records from {Name}", records.Count, typeof(PointType).Name);
             }
             catch (Exception ex)
             {
                 logger?.LogError(ex, "Failed to restore {Name}", typeof(PointType).Name);
                 throw;
             }
         }
     }
 }