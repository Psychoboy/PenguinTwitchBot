using System;
using System.Collections.Generic;

namespace DotNetTwitchBot.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Splits the string into parts no longer than <paramref name="partLength"/>.
        /// Prefer to split at the last whitespace within the limit to avoid breaking words.
        /// If a single word is longer than <paramref name="partLength"/>, it will be split.
        /// Consecutive whitespace is collapsed between parts (leading/trailing whitespace removed from parts).
        /// </summary>
        public static List<string> SplitInParts(this string s, int partLength)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));
            if (partLength <= 0) throw new ArgumentException("Part length must be greater than zero.", nameof(partLength));

            var parts = new List<string>();
            var length = s.Length;
            var start = 0;

            while (start < length)
            {
                var remaining = length - start;

                // If the rest fits, take it and finish.
                if (remaining <= partLength)
                {
                    var last = s.Substring(start, remaining).Trim();
                    if (last.Length > 0) parts.Add(last);
                    break;
                }

                var tentativeEnd = start + partLength;
                // Find the last whitespace between start and tentativeEnd-1 (inclusive).
                int lastSpace = -1;
                for (int i = tentativeEnd - 1; i >= start; i--)
                {
                    if (char.IsWhiteSpace(s[i]))
                    {
                        lastSpace = i;
                        break;
                    }
                }

                if (lastSpace > start)
                {
                    // Split at the last whitespace (exclude the whitespace from the part).
                    var part = s.Substring(start, lastSpace - start).Trim();
                    if (part.Length > 0) parts.Add(part);

                    // Move start past the whitespace, and skip any additional whitespace.
                    start = lastSpace + 1;
                    while (start < length && char.IsWhiteSpace(s[start])) start++;
                }
                else
                {
                    // No suitable space found in range -> split at partLength (may break a word).
                    var part = s.Substring(start, partLength);
                    parts.Add(part.Trim());
                    start += partLength;
                }
            }

            return parts;
        }
    }
}
