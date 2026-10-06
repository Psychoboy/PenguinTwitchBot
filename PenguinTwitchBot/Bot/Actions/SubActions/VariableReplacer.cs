using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using org.mariuszgromada.math.mxparser;
using PenguinTwitchBot.Bot.Core;

namespace PenguinTwitchBot.Bot.Actions.SubActions
{
    public static class VariableReplacer
    {
        private static bool CalledLicense = false;

        /// <summary>
        /// Optional ambient service backbone used to resolve bot and streamer names in system variables.
        /// </summary>
        public static IServiceBackbone? ServiceBackbone { get; set; }

        public static string ReplaceVariables(string input, ConcurrentDictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // 1. Replace variables from dictionary (variables dictionary takes precedence)
            foreach (var variable in variables)
            {
                input = input.Replace($"%{variable.Key}%", variable.Value, StringComparison.OrdinalIgnoreCase);
            }

            // 2. Replace built-in system global variables if still present in input
            input = ReplaceSystemVariables(input, variables);

            // 3. Evaluate math expressions: $math(...)
            var result = Regex.Replace(input, @"\$math\((.+?)\)\$", match =>
            {
                var expression = match.Groups[1].Value;
                return DoMath(expression);
            });

            return result;
        }

        private static string ReplaceSystemVariables(string input, ConcurrentDictionary<string, string> variables)
        {
            if (input.Contains("%bot%", StringComparison.OrdinalIgnoreCase))
            {
                var botName = ServiceBackbone?.BotName ?? "Bot";
                input = Regex.Replace(input, "%bot%", botName, RegexOptions.IgnoreCase);
            }

            if (input.Contains("%streamer%", StringComparison.OrdinalIgnoreCase))
            {
                var streamerName = ServiceBackbone?.BroadcasterName ?? "Streamer";
                input = Regex.Replace(input, "%streamer%", streamerName, RegexOptions.IgnoreCase);
            }

            if (input.Contains("%user%", StringComparison.OrdinalIgnoreCase))
            {
                string userName;
                if (variables.TryGetValue("User", out var u) && !string.IsNullOrEmpty(u))
                {
                    userName = u;
                }
                else if (variables.TryGetValue("Name", out var n) && !string.IsNullOrEmpty(n))
                {
                    userName = n;
                }
                else
                {
                    userName = ServiceBackbone?.BroadcasterName ?? "Streamer";
                }

                input = Regex.Replace(input, "%user%", userName, RegexOptions.IgnoreCase);
            }

            if (input.Contains("%date%", StringComparison.OrdinalIgnoreCase))
            {
                input = Regex.Replace(input, "%date%", DateTime.Now.ToShortDateString(), RegexOptions.IgnoreCase);
            }

            if (input.Contains("%time%", StringComparison.OrdinalIgnoreCase))
            {
                input = Regex.Replace(input, "%time%", DateTime.Now.ToLongTimeString(), RegexOptions.IgnoreCase);
            }

            if (input.Contains("%ticks%", StringComparison.OrdinalIgnoreCase))
            {
                input = Regex.Replace(input, "%ticks%", DateTime.UtcNow.Ticks.ToString(), RegexOptions.IgnoreCase);
            }

            // %random(min, max)%
            input = Regex.Replace(input, @"%random\((\d+),\s*(\d+)\)%", match =>
            {
                if (int.TryParse(match.Groups[1].Value, out var min) &&
                    int.TryParse(match.Groups[2].Value, out var max) &&
                    max >= min)
                {
                    return Random.Shared.Next(min, max + 1).ToString();
                }
                return match.Value;
            }, RegexOptions.IgnoreCase);

            // %random% (1-100)
            if (input.Contains("%random%", StringComparison.OrdinalIgnoreCase))
            {
                input = Regex.Replace(input, "%random%", _ => Random.Shared.Next(1, 101).ToString(), RegexOptions.IgnoreCase);
            }

            return input;
        }

        private static string DoMath(string expression)
        {
            if (!CalledLicense)
            {
                License.iConfirmNonCommercialUse("SuperPenguinTV");
                CalledLicense = true;
            }

            try
            {
                var e = new Expression(expression);
                var result = e.calculate();
                return result.ToString();
            }
            catch
            {
                return expression; // If it's not a valid expression, return the matched input unchanged
            }
        }
    }
}
