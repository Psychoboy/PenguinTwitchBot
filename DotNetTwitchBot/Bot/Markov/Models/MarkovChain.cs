 using System.Collections.Concurrent;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace DotNetTwitchBot.Bot.Markov.Models
{
    public class MarkovChain(IServiceScopeFactory scopeFactory)
    {
        //public MarkovChain(IServiceScopeFactory scopeFactory)
        //{
        //    ChainDictionary = new ConcurrentDictionary<NgramContainer, List<string>>();
        //}

        internal async Task Clear()
        {
            //lock (_lockObj)
            //{
            //    ChainDictionary.Clear();
            //}
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.MarkovValues.ExecuteDeleteAsync();
            await db.SaveChangesAsync();
        }

        //internal ConcurrentDictionary<NgramContainer, List<string>> ChainDictionary { get; }
        //private readonly object _lockObj = new object();

        /// <summary>
        /// The number of states in the chain
        /// </summary>
        //public int Count => ChainDictionary.Count;

        internal async Task<bool> Contains(NgramContainer key)
        {
            //return ChainDictionary.ContainsKey(key);
            //return false;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.MarkovValues.AnyAsync(a => a.KeyIndex == string.Join(' ', key.Ngrams));

        }

        /// <summary>
        /// Add a TGram to the markov models store with a composite key of the previous [Level] number of TGrams
        /// </summary>
        /// <param name="key">The composite key under which to add the TGram value</param>
        /// <param name="value">The value to add to the store</param>
        internal async Task AddOrCreate(NgramContainer key, string? value)
        {
            if (value == null) return;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.MarkovValues.AddAsync(new MarkovValue { KeyIndex = string.Join(' ', key.Ngrams), Value = value });
            await db.SaveChangesAsync();

            //lock (_lockObj)
            //{
            //    if (!ChainDictionary.ContainsKey(key))
            //    {
            //        ChainDictionary.TryAdd(key, new List<string> { value });
            //    }
            //    else
            //    {
            //        ChainDictionary[key].Add(value);
            //    }
            //}
        }

        internal async Task<List<string>> GetValuesForKey(NgramContainer key)
        {
            //return ChainDictionary[key];
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var result = await db.MarkovValues.Where(a => a.KeyIndex == string.Join(' ', key.Ngrams)).Select(a => a.Value).ToListAsync();
            if(result == null)
            {
                return [];
            }
            return result;
        }
    }
}
