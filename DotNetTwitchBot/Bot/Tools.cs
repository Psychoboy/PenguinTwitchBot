using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot
{
    public static class Tools
    {

        [ThreadStatic]
        private static Random? local;
        public static Random CurrentThreadRandom 
        {
            get{ return local ?? (local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));}
        }

        public static void Shuffle<T>(this IList<T> list) {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = CurrentThreadRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static T RandomElement<T>(this IEnumerable<T> list) {
            return list.ElementAt(CurrentThreadRandom.Next(list.Count()));
        }
    }
}