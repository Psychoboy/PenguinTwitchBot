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
            get { return local ?? (local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
        }

        public static void Shuffle<T>(this IList<T> list)
        {
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

        public static T RandomElement<T>(this IEnumerable<T> list)
        {
            return list.ElementAt(CurrentThreadRandom.Next(list.Count()));
        }

        public static string ConvertToCompoundDuration(long seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (seconds == 0) return "0 sec";

            TimeSpan span = TimeSpan.FromSeconds(seconds);
            int[] parts = { span.Days / 7, span.Days % 7, span.Hours, span.Minutes, span.Seconds };
            string[] units = { "w", "d", "h", "m" };

            return string.Join(", ",
                from index in Enumerable.Range(0, units.Length)
                where parts[index] > 0
                select parts[index] + units[index]);
        }
    }
}