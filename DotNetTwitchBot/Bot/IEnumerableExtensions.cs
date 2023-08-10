using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace DotNetTwitchBot.Bot
{
    public static class IEnumerableExtensions
    {
        public static T RandomElement<T>(this IEnumerable<T> list)
        {
            return RandomElement(list, null);
        }
        public static T RandomElement<T>(this IEnumerable<T> list, ILogger? logger)
        {
#pragma warning disable CS8603 //disable the possible null warning
            if (list.Count() == 0) return default;
#pragma warning restore CS8603
            var elementNum = RandomNumberGenerator.GetInt32(list.Count());
            logger?.LogInformation("Got ElementNum: {0} from total: {1}", elementNum, list.Count());
            return list.ElementAt(elementNum);
        }
    }
}