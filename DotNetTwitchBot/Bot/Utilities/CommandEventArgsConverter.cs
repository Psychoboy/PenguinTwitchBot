using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Utilities
{
    /// <summary>
    /// Utility class for converting CommandEventArgs to Dictionary for variable substitution
    /// </summary>
    public static class CommandEventArgsConverter
    {
        /// <summary>
        /// Converts CommandEventArgs to a Dictionary with string keys and values
        /// </summary>
        /// <param name="eventArgs">The CommandEventArgs to convert</param>
        /// <returns>Dictionary with property names as keys and property values as strings</returns>
        public static Dictionary<string, string> ToDictionary(CommandEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new Dictionary<string, string>();
            }

            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // BaseChatEventArgs properties
                ["IsSub"] = eventArgs.IsSub.ToString(),
                ["IsMod"] = eventArgs.IsMod.ToString(),
                ["IsVip"] = eventArgs.IsVip.ToString(),
                ["IsBroadcaster"] = eventArgs.IsBroadcaster.ToString(),
                ["DisplayName"] = eventArgs.DisplayName ?? string.Empty,
                ["Name"] = eventArgs.Name ?? string.Empty,
                ["UserId"] = eventArgs.UserId ?? string.Empty,
                ["MessageId"] = eventArgs.MessageId ?? string.Empty,
                ["IsSubOrHigher"] = eventArgs.IsSubOrHigher().ToString(),
                ["IsVipOrHigher"] = eventArgs.IsVipOrHigher().ToString(),
                ["IsModOrHigher"] = eventArgs.IsModOrHigher().ToString(),

                // CommandEventArgs properties
                ["Command"] = eventArgs.Command ?? string.Empty,
                ["Arg"] = eventArgs.Arg ?? string.Empty,
                ["TargetUser"] = eventArgs.TargetUser ?? string.Empty,
                ["IsWhisper"] = eventArgs.IsWhisper.ToString(),
                ["IsDiscord"] = eventArgs.IsDiscord.ToString(),
                ["DiscordMention"] = eventArgs.DiscordMention ?? string.Empty,
                ["FromAlias"] = eventArgs.FromAlias.ToString(),
                ["SkipLock"] = eventArgs.SkipLock.ToString(),
                ["FromOwnChannel"] = eventArgs.FromOwnChannel.ToString()
            };

            // Add Args as indexed items (Args_0, Args_1, etc.)
            if (eventArgs.Args != null)
            {
                for (int i = 0; i < eventArgs.Args.Count; i++)
                {
                    dictionary[$"Args_{i}"] = eventArgs.Args[i] ?? string.Empty;
                }
            }

            return dictionary;
        }

        /// <summary>
        /// Converts CommandEventArgs to a Dictionary and optionally adds custom values
        /// </summary>
        /// <param name="eventArgs">The CommandEventArgs to convert</param>
        /// <param name="additionalValues">Additional key-value pairs to add to the dictionary</param>
        /// <returns>Dictionary with all values combined</returns>
        public static Dictionary<string, string> ToDictionary(
            CommandEventArgs eventArgs, 
            Dictionary<string, string>? additionalValues = null)
        {
            var dictionary = ToDictionary(eventArgs);

            if (additionalValues != null)
            {
                foreach (var kvp in additionalValues)
                {
                    dictionary[kvp.Key] = kvp.Value;
                }
            }

            return dictionary;
        }
    }
}
