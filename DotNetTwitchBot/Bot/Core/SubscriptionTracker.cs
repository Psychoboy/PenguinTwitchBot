using DotNetTwitchBot.Bot.Repository;

namespace DotNetTwitchBot.Bot.Core
{
    public class SubscriptionTracker(ILogger<SubscriptionTracker> logger, IServiceScopeFactory scopeFactory)
    {
        public async Task<bool> ExistingSub(string name)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                return await db.SubscriptionHistories.Find(x => x.Username.Equals(name)).AnyAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking existing sub");
                return false;
            }
        }

        public async Task<DateTime?> LastSub(string name)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var lastSub = await db.SubscriptionHistories.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
                if (lastSub != null)
                {
                    return lastSub.LastSub;
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking existing sub");
                return null;
            }
        }

        public async Task<List<string>> MissingSubs(IEnumerable<string> names)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var foundNames = await db.SubscriptionHistories.Find(x => names.Contains(x.Username)).Select(x => x.Username).ToListAsync();
                var missingNames = names.Where(x => foundNames.Contains(x) == false).ToList();
                return missingNames;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking missing subs");
                return [];
            }
        }

        public async Task AddOrUpdateSubHistory(string name)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var subHistory = await db.SubscriptionHistories.Find(x => x.Username.Equals(name)).FirstOrDefaultAsync();
                subHistory ??= new SubscriptionHistory()
                {
                    Username = name
                };
                subHistory.LastSub = DateTime.Now;
                db.SubscriptionHistories.Update(subHistory);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error Adding or Updating sub history");
            }
        }
    }
}