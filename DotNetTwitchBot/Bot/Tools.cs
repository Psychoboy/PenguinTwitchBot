using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot
{
    public static class Tools
    {
        public static int Next(int max)
        {
            return RandomNumberGenerator.GetInt32(max);
        }

        public static int Next(int min, int max)
        {
            return RandomNumberGenerator.GetInt32(min, max);
        }


        public static int RandomRange(int min, int max)
        {
            return RandomNumberGenerator.GetInt32(min, max + 1);
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = RandomNumberGenerator.GetInt32(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }



        public static string ConvertToCompoundDuration(long seconds)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(seconds);
            if (seconds == 0) return "0 sec";

            TimeSpan span = TimeSpan.FromSeconds(seconds);
            int[] parts = [span.Days / 7, span.Days % 7, span.Hours, span.Minutes, span.Seconds];
            string[] units = ["w", "d", "h", "m"];

            return string.Join(", ",
                from index in Enumerable.Range(0, units.Length)
                where parts[index] > 0
                select parts[index] + units[index]);
        }
        public static string CleanInput(string? strIn)
        {
            // Replace invalid characters with empty strings.
            if (string.IsNullOrWhiteSpace(strIn)) return "";
            try
            {
                return Regex.Replace(strIn, @"[^\w\.@-]", "",
                                     RegexOptions.None, TimeSpan.FromSeconds(1.5));
            }
            // If we timeout when replacing invalid characters,
            // we should return Empty.
            catch (RegexMatchTimeoutException)
            {
                return String.Empty;
            }
        }
        public static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
        {
            foreach (var element in toAdd)
            {
                @this.Add(element);
            }
        }
    }
}