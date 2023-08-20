using DotNetTwitchBot.Bot.Repository;

namespace DotNetTwitchBot.Bot.Core
{
    public class SubscriptionTracker
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionTracker> _logger;

        public SubscriptionTracker(ILogger<SubscriptionTracker> logger, IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<bool> ExistingSub(string name)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                return await db.SubscriptionHistories.Find(x => x.Username.Equals(name)).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existing sub");
                return false;
            }
        }

        public async Task<List<string>> MissingSubs(IEnumerable<string> names)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var foundNames = await db.SubscriptionHistories.Find(x => names.Contains(x.Username)).Select(x => x.Username).ToListAsync();
                var missingNames = names.Where(x => foundNames.Contains(x) == false).ToList();
                return missingNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking missing subs");
                return new List<string>();
            }
        }

        public async Task AddOrUpdateSubHistory(string name)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
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
                _logger.LogError(ex, "Error Adding or Updating sub history");
            }
        }
    }
}