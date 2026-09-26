using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Helpers;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Actions.Utilities
{
    /// <summary>
    /// Utility class for converting CommandEventArgs to ConcurrentDictionary for variable substitution
    /// </summary>
    public static class CommandEventArgsConverter
    {
        /// <summary>
        /// Converts CommandEventArgs to a ConcurrentDictionary with string keys and values
        /// </summary>
        /// <param name="eventArgs">The CommandEventArgs to convert</param>
        /// <returns>ConcurrentDictionary with property names as keys and property values as strings</returns>
        public static ConcurrentDictionary<string, string> ToDictionary(CommandEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Add all properties
            dictionary["IsSub"] = eventArgs.IsSub.ToString();
            dictionary["IsMod"] = eventArgs.IsMod.ToString();
            dictionary["IsVip"] = eventArgs.IsVip.ToString();
            dictionary["IsBroadcaster"] = eventArgs.IsBroadcaster.ToString();
            dictionary["DisplayName"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName);
            dictionary["Name"] = ViewerInputSanitizer.Sanitize(eventArgs.Name);
            dictionary["UserId"] = eventArgs.UserId ?? string.Empty;
            dictionary["MessageId"] = eventArgs.MessageId ?? string.Empty;
            dictionary["IsSubOrHigher"] = eventArgs.IsSubOrHigher().ToString();
            dictionary["IsVipOrHigher"] = eventArgs.IsVipOrHigher().ToString();
            dictionary["IsModOrHigher"] = eventArgs.IsModOrHigher().ToString();
            dictionary["User"] = ViewerInputSanitizer.Sanitize(eventArgs.DisplayName);
            dictionary["Message"] = ViewerInputSanitizer.Sanitize(eventArgs.Arg);
            dictionary["Command"] = eventArgs.Command ?? string.Empty;
            dictionary["Arg"] = ViewerInputSanitizer.Sanitize(eventArgs.Arg);
            dictionary["TargetUser"] = ViewerInputSanitizer.Sanitize(eventArgs.TargetUser);
            dictionary["IsWhisper"] = eventArgs.IsWhisper.ToString();
            dictionary["IsDiscord"] = eventArgs.IsDiscord.ToString();
            dictionary["DiscordMention"] = eventArgs.DiscordMention ?? string.Empty;
            dictionary["FromAlias"] = eventArgs.FromAlias.ToString();
            dictionary["SkipLock"] = eventArgs.SkipLock.ToString();
            dictionary["FromOwnChannel"] = eventArgs.FromOwnChannel.ToString();

            if(eventArgs.Args != null && eventArgs.Args.Count > 0)
            {
                dictionary["targetorself"] = ViewerInputSanitizer.Sanitize(eventArgs.Args[0].Replace("@", "").Trim());
                dictionary["target"] = ViewerInputSanitizer.Sanitize(eventArgs.Args[0].Replace("@", "").Trim());
                dictionary["args"] = ViewerInputSanitizer.Sanitize(eventArgs.Arg);
                dictionary["rawInput"] = ViewerInputSanitizer.Sanitize(eventArgs.Arg);
            } else
            {
                dictionary["targetorself"] = ViewerInputSanitizer.Sanitize(eventArgs.Name);
                dictionary["target"] = string.Empty;
                dictionary["args"] = string.Empty;
                dictionary["rawInput"] = string.Empty;
            }

            dictionary["OriginalEventArgs"] = JsonSerializer.Serialize(eventArgs);

            // Add Args as indexed items (Args_0, Args_1, etc.)
            if (eventArgs.Args != null && eventArgs.Args.Count > 0)
            {
                for (int i = 0; i < eventArgs.Args.Count; i++)
                {
                    dictionary[$"Args_{i}"] = ViewerInputSanitizer.Sanitize(eventArgs.Args[i]);
                }
            }

            return dictionary;
        }


        /// <summary>
        /// Creates a new instance of the CommandEventArgs class from the specified dictionary, if possible.
        /// </summary>
        /// <remarks>If the dictionary is null, does not contain the required key, or if deserialization
        /// fails, a default CommandEventArgs instance is returned.</remarks>
        /// <param name="dictionary">A dictionary containing serialized event argument data. The dictionary should include an entry with the key
        /// "OriginalEventArgs" containing a JSON representation of a CommandEventArgs object.</param>
        /// <returns>A CommandEventArgs instance deserialized from the dictionary if possible; otherwise, a new CommandEventArgs
        /// instance.</returns>
        public static CommandEventArgs FromDictionary(ConcurrentDictionary<string, string> dictionary)
        {
            if (dictionary == null || !dictionary.TryGetValue("OriginalEventArgs", out var json))
            {
                return new CommandEventArgs();
            }
            try
            {
                return JsonSerializer.Deserialize<CommandEventArgs>(json) ?? new CommandEventArgs();
            }
            catch
            {
                return new CommandEventArgs();
            }
        }

        public static CommandEventArgs? FromDictionaryOrNull(ConcurrentDictionary<string, string> dictionary)
        {
            if (dictionary == null || !dictionary.TryGetValue("OriginalEventArgs", out var json))
            {
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize<CommandEventArgs>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts CommandEventArgs to a ConcurrentDictionary and optionally adds custom values
        /// </summary>
        /// <param name="eventArgs">The CommandEventArgs to convert</param>
        /// <param name="additionalValues">Additional key-value pairs to add to the dictionary</param>
        /// <returns>ConcurrentDictionary with all values combined</returns>
        public static ConcurrentDictionary<string, string> ToDictionary(
            CommandEventArgs eventArgs, 
            ConcurrentDictionary<string, string>? additionalValues = null)
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
