using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot
{
    public static class TimeSpanExtensions
    {
        public static string ToFriendlyString(this TimeSpan timeSpan)
        {
            string result = string.Empty;

            if (Math.Floor(timeSpan.TotalDays) > 0.0d)
                result += string.Format(@"{0:ddd} days, ", timeSpan).TrimFirst('0');
            if (Math.Floor(timeSpan.TotalHours) > 0.0d)
                result += string.Format(@"{0:hh} hours, ", timeSpan).TrimFirst('0');
            if (Math.Floor(timeSpan.TotalMinutes) > 0.0d)
                result += string.Format(@"{0:mm} minutes, ", timeSpan).TrimFirst('0');
            if (Math.Floor(timeSpan.TotalSeconds) > 0.0d)
                result += string.Format(@"{0:ss} seconds ", timeSpan).TrimFirst('0');
            else
                result += "0 seconds";

            return result;
        }
        public static string TrimFirst(this string value, char c)
        {
            if (value[0] == c)
                return value[1..];

            return value;
        }
    }
}
