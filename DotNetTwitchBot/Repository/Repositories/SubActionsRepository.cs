using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DotNetTwitchBot.Repository.Repositories
{
    public class SubActionsRepository : GenericRepository<SubActionType>, ISubActionsRepository
    {
        public SubActionsRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<int> GetNextIdAsync()
        {
            var maxId = await _context.SubActions.MaxAsync(s => (int?)s.Id) ?? 0;
            return maxId + 1;
        }

        public override async Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            // SubActions are now backed up as part of ActionType backup
            // This method intentionally does nothing to avoid duplicate backups
            logger?.LogDebug("Skipping SubActionType backup (included in ActionType backup)");
            await Task.CompletedTask;
        }

        public override async Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null)
        {
            // SubActions are now restored as part of ActionType restore
            // This method intentionally does nothing to avoid conflicts
            logger?.LogDebug("Skipping SubActionType restore (included in ActionType restore)");
            await Task.CompletedTask;
        }
    }

    // Custom JsonTypeInfoResolver for SubActionType polymorphism
    public class SubActionTypeResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

            Type baseType = typeof(SubActionType);
            if (jsonTypeInfo.Type == baseType)
            {
                jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type",
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                    DerivedTypes =
                    {
                        new JsonDerivedType(typeof(SendMessageType), "SendMessage"),
                        new JsonDerivedType(typeof(AlertType), "Alert"),
                        new JsonDerivedType(typeof(PlaySoundType), "PlaySound"),
                        new JsonDerivedType(typeof(RandomIntType), "RandomInt"),
                        new JsonDerivedType(typeof(UptimeType), "Uptime"),
                        new JsonDerivedType(typeof(CurrentTimeType), "CurrentTime"),
                        new JsonDerivedType(typeof(FollowAgeType), "FollowAge"),
                        new JsonDerivedType(typeof(WatchTimeType), "WatchTime"),
                        new JsonDerivedType(typeof(ExternalApiType), "ExternalApi"),
                        new JsonDerivedType(typeof(WriteFileType), "WriteFile"),
                        new JsonDerivedType(typeof(GiveawayPrizeType), "GiveawayPrize")
                    }
                };
            }

            return jsonTypeInfo;
        }
    }
}
