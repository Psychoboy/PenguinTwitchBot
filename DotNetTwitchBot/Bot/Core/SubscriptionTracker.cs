using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Core
{
    public class SubscriptionTracker
    {
        private IServiceScopeFactory _scopeFactory;
        private ILogger<SubscriptionTracker> _logger;

        public SubscriptionTracker(ILogger<SubscriptionTracker> logger, IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<bool> ExistingSub(string name)
        {
            try
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    return await db.SubscriptionHistories.Where(x => x.Username.Equals(name)).AnyAsync();
                }
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
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var foundNames = await db.SubscriptionHistories.Where(x => names.Contains(x.Username)).Select(x => x.Username).ToListAsync();
                    var missingNames = names.Where(x => foundNames.Contains(x) == false).ToList();
                    return missingNames;
                }
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
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var subHistory = await db.SubscriptionHistories.Where(x => x.Username.Equals(name)).FirstOrDefaultAsync();
                    if (subHistory == null)
                    {
                        subHistory = new SubscriptionHistory()
                        {
                            Username = name
                        };
                        subHistory.LastSub = DateTime.Now;
                        db.SubscriptionHistories.Update(subHistory);
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Adding or Updating sub history");
            }
        }
    }
}