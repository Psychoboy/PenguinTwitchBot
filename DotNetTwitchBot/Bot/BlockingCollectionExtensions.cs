using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot
{
    public static class BlockingCollectionExtensions
    {
        public static void Clear<T>(this BlockingCollection<T> blockingCollection)
        {
            ArgumentNullException.ThrowIfNull(blockingCollection);

            while (blockingCollection.Count > 0)
            {
                blockingCollection.TryTake(out _);
            }
        }
    }
}