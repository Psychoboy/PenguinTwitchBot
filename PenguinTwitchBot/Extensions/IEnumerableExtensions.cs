using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace PenguinTwitchBot.Extensions
{
    public static class IEnumerableExtensions
    {
        public static T RandomElementOrDefault<T>(this IEnumerable<T> list)
        {
            return list.RandomElementOrDefault(null);
        }
        public static T RandomElementOrDefault<T>(this IEnumerable<T> list, ILogger? logger)
        {
#pragma warning disable CS8603 //disable the possible null warning
            if (!list.Any()) return default;
#pragma warning restore CS8603
            var elementNum = RandomNumberGenerator.GetInt32(list.Count());
            return list.ElementAt(elementNum);
        }
        public static T RandomElement<T>(this IEnumerable<T> list)
        {
            var elementNum = RandomNumberGenerator.GetInt32(list.Count());
            return list.ElementAt(elementNum);
        }

        /// <summary>
        /// Selects the next element from a keyed shuffle bag (deck randomizer).
        /// Guarantees fair rotation through all candidates before repeating, and prevents consecutive duplicates.
        /// </summary>
        public static T? NextFromShuffleBag<T>(this IEnumerable<T> list, string bagKey) where T : notnull
        {
            return ShuffleBag.Next(bagKey, list);
        }

        /// <summary>
        /// Selects the next element from a keyed shuffle bag (deck randomizer), matching items by key.
        /// Guarantees fair rotation through all candidates before repeating, and prevents consecutive duplicates.
        /// </summary>
        public static T? NextFromShuffleBag<T, TKey>(this IEnumerable<T> list, string bagKey, Func<T, TKey> keySelector) where TKey : notnull
        {
            return ShuffleBag.Next(bagKey, list, keySelector);
        }
    }
}
