 using System.Collections.Concurrent;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace DotNetTwitchBot.Bot.Markov.Models
{
    public class MarkovChain(IServiceScopeFactory scopeFactory, ILogger<GenericMarkov> logger)
    {
        internal async Task Clear()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.MarkovValues.ExecuteDeleteAsync();
            await db.SaveChangesAsync();
        }

        internal async Task<bool> Contains(NgramContainer key)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.MarkovValues.AnyAsync(a => a.KeyIndex == key.ToString());

        }

        /// <summary>
        /// Add a TGram to the markov models store with a composite key of the previous [Level] number of TGrams
        /// </summary>
        /// <param name="key">The composite key under which to add the TGram value</param>
        /// <param name="value">The value to add to the store</param>
        internal async Task AddOrCreate(NgramContainer key, string? value, ApplicationDbContext db)
        {
            try
            {
                if (value == null) return;
                if(key.Ngrams.Length == 0) return;
                var keyValue = key.ToString();
                if(keyValue.Length > 255) return;
                await db.MarkovValues.AddAsync(new MarkovValue { KeyIndex = keyValue, Value = value });
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to add value to markov chain {chain}", key.ToString());
            }
        }

        internal async Task<List<string>> GetValuesForKey(NgramContainer key)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var result = await db.MarkovValues.Where(a => a.KeyIndex == key.ToString()).Select(a => a.Value).ToListAsync();
            if(result == null)
            {
                return [];
            }
            return result;
        }
    }
}
