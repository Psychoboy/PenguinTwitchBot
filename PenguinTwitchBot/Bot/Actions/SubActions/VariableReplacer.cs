using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using org.mariuszgromada.math.mxparser;
using PenguinTwitchBot.Bot.Core;

namespace PenguinTwitchBot.Bot.Actions.SubActions
{
    public static class VariableReplacer
    {
        private static bool CalledLicense = false;

        private static readonly Regex SystemTokensRegex = new(
            @"%(?<token>bot|streamer|user|date|time|ticks|random(?:\((?<min>-?\d+),\s*(?<max>-?\d+)\))?)%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MathExpressionRegex = new(
            @"\$math\((.+?)\)\$",
            RegexOptions.Compiled);

        /// <summary>
        /// Optional ambient service backbone used to resolve bot and streamer names in system variables.
        /// </summary>
        public static IServiceBackbone? ServiceBackbone { get; set; }

        private static string? TryGetVariableIgnoreCase(ConcurrentDictionary<string, string> variables, string key)
        {
            if (variables.TryGetValue(key, out var exact))
            {
                return exact;
            }
            foreach (var kvp in variables)
            {
                if (kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        public static string ReplaceVariables(string input, ConcurrentDictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            if (input.Contains('%'))
            {
                // 1. Replace variables from dictionary (variables dictionary takes precedence)
                foreach (var variable in variables)
                {
                    var replacement = variable.Value;
                    if (variable.Key.Equals("User", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(replacement))
                    {
                        var nameVal = TryGetVariableIgnoreCase(variables, "Name");
                        if (!string.IsNullOrEmpty(nameVal))
                        {
                            replacement = nameVal;
                        }
                    }

                    input = input.Replace($"%{variable.Key}%", replacement, StringComparison.OrdinalIgnoreCase);
                }

                // 2. Replace built-in system global variables if still present in input
                if (input.Contains('%'))
                {
                    input = ReplaceSystemVariables(input, variables);
                }
            }

            // 3. Evaluate math expressions: $math(...)
            if (input.Contains("$math(", StringComparison.Ordinal))
            {
                input = MathExpressionRegex.Replace(input, match =>
                {
                    var expression = match.Groups[1].Value;
                    return DoMath(expression);
                });
            }

            return input;
        }

        private static string ReplaceSystemVariables(string input, ConcurrentDictionary<string, string> variables)
        {
            return SystemTokensRegex.Replace(input, match =>
            {
                var token = match.Groups["token"].Value;

                if (token.Equals("bot", StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceBackbone?.BotName ?? "Bot";
                }

                if (token.Equals("streamer", StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceBackbone?.BroadcasterName ?? "Streamer";
                }

                if (token.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    var userVal = TryGetVariableIgnoreCase(variables, "User");
                    if (!string.IsNullOrEmpty(userVal))
                    {
                        return userVal;
                    }

                    var nameVal = TryGetVariableIgnoreCase(variables, "Name");
                    if (!string.IsNullOrEmpty(nameVal))
                    {
                        return nameVal;
                    }

                    return ServiceBackbone?.BroadcasterName ?? "Streamer";
                }

                if (token.Equals("date", StringComparison.OrdinalIgnoreCase))
                {
                    return DateTime.Now.ToShortDateString();
                }

                if (token.Equals("time", StringComparison.OrdinalIgnoreCase))
                {
                    return DateTime.Now.ToLongTimeString();
                }

                if (token.Equals("ticks", StringComparison.OrdinalIgnoreCase))
                {
                    return DateTime.UtcNow.Ticks.ToString();
                }

                if (token.StartsWith("random", StringComparison.OrdinalIgnoreCase))
                {
                    var minGroup = match.Groups["min"];
                    var maxGroup = match.Groups["max"];
                    if (minGroup.Success && maxGroup.Success)
                    {
                        if (int.TryParse(minGroup.Value, out var min) &&
                            int.TryParse(maxGroup.Value, out var max) &&
                            max >= min)
                        {
                            return Random.Shared.NextInt64((long)min, (long)max + 1).ToString();
                        }

                        return match.Value;
                    }

                    return Random.Shared.Next(1, 101).ToString();
                }

                return match.Value;
            });
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
