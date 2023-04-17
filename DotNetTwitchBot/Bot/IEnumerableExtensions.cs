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
#pragma warning disable CS8603 //disable the possible null warning
            if (list.Count() == 0) return default(T);
#pragma warning restore CS8603
            return list.ElementAt(RandomNumberGenerator.GetInt32(list.Count()));
        }
    }
}