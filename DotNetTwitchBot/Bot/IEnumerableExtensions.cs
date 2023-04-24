using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace DotNetTwitchBot.Bot
{
    public static class IEnumerableExtensions
    {
        public static T RandomElement<T>(this IEnumerable<T> list, ILogger? logger = null)
        {
#pragma warning disable CS8603 //disable the possible null warning
            if (list.Count() == 0) return default(T);
#pragma warning restore CS8603
            var elementNum = RandomNumberGenerator.GetInt32(list.Count());
            if (logger != null)
            {
                logger.LogInformation("Got ElementNum: {0} from total: {1}", elementNum, list.Count());
            }
            return list.ElementAt(elementNum);
        }
    }
}