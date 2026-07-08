using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services
{
    public interface ICooldownCleanupService
    {
        Task<int> CleanupExpiredCooldownsAsync();
    }

    public class CooldownCleanupService(ILogger<CooldownCleanupService> logger, IServiceScopeFactory scopeFactory) : ICooldownCleanupService
    {
        public async Task<int> CleanupExpiredCooldownsAsync()
        {
            var utcNow = DateTime.UtcNow;

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var removed = await db.Cooldowns
                .Find(x =>
                    (x.IsGlobal && x.NextGlobalCooldownTime <= utcNow) ||
                    (!x.IsGlobal && x.NextUserCooldownTime <= utcNow))
                .ExecuteDeleteAsync();

            logger.LogInformation("Expired cooldown cleanup removed {RemovedCooldowns} rows.", removed);
            return removed;
        }
    }
}