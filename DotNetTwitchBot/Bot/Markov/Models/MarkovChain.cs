 using System.Collections.Concurrent;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace DotNetTwitchBot.Bot.Markov.Models
{
    public class MarkovChain
    {
        public MarkovChain()
        {
            ChainDictionary = new ConcurrentDictionary<NgramContainer<string>, List<string>>();
        }

        internal void Clear()
        {
            lock (_lockObj)
            {
                ChainDictionary.Clear();
            }
        }

        internal ConcurrentDictionary<NgramContainer<string>, List<string>> ChainDictionary { get; }
        private readonly object _lockObj = new object();

        /// <summary>
        /// The number of states in the chain
        /// </summary>
        public int Count => ChainDictionary.Count;

        internal bool Contains(NgramContainer<string> key)
        {
            return ChainDictionary.ContainsKey(key);
        }

        /// <summary>
        /// Add a TGram to the markov models store with a composite key of the previous [Level] number of TGrams
        /// </summary>
        /// <param name="key">The composite key under which to add the TGram value</param>
        /// <param name="value">The value to add to the store</param>
        internal void AddOrCreate(NgramContainer<string> key, string? value)
        {
            if (value == null) return;
            lock (_lockObj)
            {
                if (!ChainDictionary.ContainsKey(key))
                {
                    ChainDictionary.TryAdd(key, new List<string> { value });
                }
                else
                {
                    ChainDictionary[key].Add(value);
                }
            }
        }

        internal List<string> GetValuesForKey(NgramContainer<string> key)
        {
            return ChainDictionary[key];
        }
    }
}
