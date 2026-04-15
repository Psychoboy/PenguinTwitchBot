using DotNetTwitchBot.Bot.Core.Database;
using DotNetTwitchBot.Bot.DatabaseTools;
using DotNetTwitchBot.Bot.Models.Fishing;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DotNetTwitchBot.Repository.Repositories
{
    /// <summary>
    /// FishType repository - This is a master table that must be restored FIRST
    /// since other fishing tables depend on FishType IDs.
    /// </summary>
    public class FishingRepository : GenericRepository<FishType>, IFishingRepository
    {
        private static bool _fishingTablesDeleted = false;

        public FishingRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            try
            {
                // Delete all fishing tables ONCE at the start (in reverse dependency order)
                if (!_fishingTablesDeleted)
                {
                    await DeleteAllFishingTables(context, logger);
                    _fishingTablesDeleted = true;
                }

                var fileName = $"{backupDirectory}/{typeof(FishType).Name}.json";
                if (!File.Exists(fileName))
                {
                    logger?.LogDebug("No backup file found for {Name}", typeof(FishType).Name);
                    return;
                }

                var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                var records = JsonSerializer.Deserialize<List<FishType>>(json, options);
                if (records == null) throw new Exception($"{typeof(FishType).Name}.json was null");

                // CRITICAL: Preserve IDs from backup by explicitly setting them
                // This ensures foreign keys in other tables remain valid
                foreach (var record in records)
                {
                    var entry = context.Entry(record);
                    entry.State = EntityState.Added;
                }

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                logger?.LogDebug("Restored {Count} FishType records with preserved IDs", records.Count);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore {Name}", typeof(FishType).Name);
                throw;
            }
        }

        /// <summary>
        /// Deletes all fishing tables in reverse dependency order to avoid FK constraint violations.
        /// This is called ONCE before any fishing table restoration begins.
        /// </summary>
        private static async Task DeleteAllFishingTables(DbContext context, ILogger? logger)
        {
            logger?.LogInformation("Deleting all fishing tables in reverse dependency order...");

            // Delete in reverse order: children before parents
            // This avoids foreign key constraint violations

            // 1. Delete UserFishingBoosts (depends on FishingShopItems)
            logger?.LogDebug("Deleting UserFishingBoosts...");
            await context.Set<UserFishingBoost>().ExecuteDeleteAsync();

            // 2. Delete FishCatches (depends on FishTypes)
            logger?.LogDebug("Deleting FishCatches...");
            await context.Set<FishCatch>().ExecuteDeleteAsync();

            // 3. Delete FishingShopItems (depends on FishTypes via TargetFishTypeId)
            logger?.LogDebug("Deleting FishingShopItems...");
            await context.Set<FishingShopItem>().ExecuteDeleteAsync();

            // 4. Delete FishTypes (master table, no dependencies)
            logger?.LogDebug("Deleting FishTypes...");
            await context.Set<FishType>().ExecuteDeleteAsync();

            // 5. Delete FishingGold (independent)
            logger?.LogDebug("Deleting FishingGold...");
            await context.Set<FishingGold>().ExecuteDeleteAsync();

            // 6. Delete FishingSettings (independent)
            logger?.LogDebug("Deleting FishingSettings...");
            await context.Set<FishingSettings>().ExecuteDeleteAsync();

            context.ChangeTracker.Clear();
            logger?.LogInformation("All fishing tables deleted successfully");
        }

        /// <summary>
        /// Resets the deletion flag. Call this at the start of a new restore operation.
        /// </summary>
        public static void ResetDeletionFlag()
        {
            _fishingTablesDeleted = false;
        }
    }

    /// <summary>
    /// FishingShopItem repository - This must be restored BEFORE UserFishingBoost
    /// since UserFishingBoost references ShopItemId.
    /// NOTE: Deletion happens in FishingRepository to maintain proper order.
    /// </summary>
    public class FishingShopItemRepository : GenericRepository<FishingShopItem>, IFishingShopItemRepository
    {
        public FishingShopItemRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            try
            {
                var fileName = $"{backupDirectory}/{typeof(FishingShopItem).Name}.json";
                if (!File.Exists(fileName))
                {
                    logger?.LogDebug("No backup file found for {Name}", typeof(FishingShopItem).Name);
                    return;
                }

                var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                var records = JsonSerializer.Deserialize<List<FishingShopItem>>(json, options);
                if (records == null) throw new Exception($"{typeof(FishingShopItem).Name}.json was null");

                // Deletion already handled by FishingRepository
                // CRITICAL: Preserve IDs from backup
                // TargetFishTypeId should already be valid since FishType was restored first
                foreach (var record in records)
                {
                    var entry = context.Entry(record);
                    entry.State = EntityState.Added;
                }

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                logger?.LogDebug("Restored {Count} FishingShopItem records with preserved IDs", records.Count);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore {Name}", typeof(FishingShopItem).Name);
                throw;
            }
        }
    }

    /// <summary>
    /// FishCatch repository - Depends on FishType being restored first.
    /// NOTE: Deletion happens in FishingRepository to maintain proper order.
    /// </summary>
    public class FishCatchRepository : GenericRepository<FishCatch>, IFishCatchRepository
    {
        public FishCatchRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            try
            {
                var fileName = $"{backupDirectory}/{typeof(FishCatch).Name}.json";
                if (!File.Exists(fileName))
                {
                    logger?.LogDebug("No backup file found for {Name}", typeof(FishCatch).Name);
                    return;
                }

                var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                var records = JsonSerializer.Deserialize<List<FishCatch>>(json, options);
                if (records == null) throw new Exception($"{typeof(FishCatch).Name}.json was null");

                // Deletion already handled by FishingRepository
                // CRITICAL: Preserve IDs from backup
                // FishTypeId should be valid since FishType was restored first
                foreach (var record in records)
                {
                    var entry = context.Entry(record);
                    entry.State = EntityState.Added;
                }

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                logger?.LogDebug("Restored {Count} FishCatch records with preserved IDs", records.Count);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore {Name}", typeof(FishCatch).Name);
                throw;
            }
        }
    }

    /// <summary>
    /// UserFishingBoost repository - Depends on FishingShopItem being restored first.
    /// NOTE: Deletion happens in FishingRepository to maintain proper order.
    /// </summary>
    public class UserFishingBoostRepository : GenericRepository<UserFishingBoost>, IUserFishingBoostRepository
    {
        public UserFishingBoostRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            try
            {
                var fileName = $"{backupDirectory}/{typeof(UserFishingBoost).Name}.json";
                if (!File.Exists(fileName))
                {
                    logger?.LogDebug("No backup file found for {Name}", typeof(UserFishingBoost).Name);
                    return;
                }

                var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                var records = JsonSerializer.Deserialize<List<UserFishingBoost>>(json, options);
                if (records == null) throw new Exception($"{typeof(UserFishingBoost).Name}.json was null");

                // Deletion already handled by FishingRepository
                // CRITICAL: Preserve IDs from backup
                // ShopItemId should be valid since FishingShopItem was restored first
                foreach (var record in records)
                {
                    var entry = context.Entry(record);
                    entry.State = EntityState.Added;
                }

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                logger?.LogDebug("Restored {Count} UserFishingBoost records with preserved IDs", records.Count);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore {Name}", typeof(UserFishingBoost).Name);
                throw;
            }
        }
    }

    /// <summary>
    /// FishingGold repository - No foreign key dependencies.
    /// NOTE: Deletion happens in FishingRepository to maintain proper order.
    /// </summary>
    public class FishingGoldRepository : GenericRepository<FishingGold>, IFishingGoldRepository
    {
        public FishingGoldRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            try
            {
                var fileName = $"{backupDirectory}/{typeof(FishingGold).Name}.json";
                if (!File.Exists(fileName))
                {
                    logger?.LogDebug("No backup file found for {Name}", typeof(FishingGold).Name);
                    return;
                }

                var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                var records = JsonSerializer.Deserialize<List<FishingGold>>(json, options);
                if (records == null) throw new Exception($"{typeof(FishingGold).Name}.json was null");

                // Deletion already handled by FishingRepository
                // Add records - no FK dependencies so order doesn't matter
                foreach (var record in records)
                {
                    var entry = context.Entry(record);
                    entry.State = EntityState.Added;
                }

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                logger?.LogDebug("Restored {Count} FishingGold records", records.Count);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore {Name}", typeof(FishingGold).Name);
                throw;
            }
        }
    }

    /// <summary>
    /// FishingSettings repository - No foreign key dependencies.
    /// NOTE: Deletion happens in FishingRepository to maintain proper order.
    /// </summary>
    public class FishingSettingsRepository : GenericRepository<FishingSettings>, IFishingSettingsRepository
    {
        public FishingSettingsRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            try
            {
                var fileName = $"{backupDirectory}/{typeof(FishingSettings).Name}.json";
                if (!File.Exists(fileName))
                {
                    logger?.LogDebug("No backup file found for {Name}", typeof(FishingSettings).Name);
                    return;
                }

                var json = await File.ReadAllTextAsync(fileName, encoding: System.Text.Encoding.UTF8);

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                var records = JsonSerializer.Deserialize<List<FishingSettings>>(json, options);
                if (records == null) throw new Exception($"{typeof(FishingSettings).Name}.json was null");

                // Deletion already handled by FishingRepository
                // Add records - no FK dependencies so order doesn't matter
                foreach (var record in records)
                {
                    var entry = context.Entry(record);
                    entry.State = EntityState.Added;
                }

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                logger?.LogDebug("Restored {Count} FishingSettings records", records.Count);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to restore {Name}", typeof(FishingSettings).Name);
                throw;
            }
        }
    }
}