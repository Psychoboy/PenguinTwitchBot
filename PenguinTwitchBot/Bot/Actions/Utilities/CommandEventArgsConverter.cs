using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Models.Chat;
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
            dictionary["DisplayName"] = eventArgs.DisplayName ?? string.Empty;
            dictionary["Name"] = eventArgs.Name ?? string.Empty;
            dictionary["UserId"] = eventArgs.UserId ?? string.Empty;
            dictionary["MessageId"] = eventArgs.MessageId ?? string.Empty;
            dictionary["IsSubOrHigher"] = eventArgs.IsSubOrHigher().ToString();
            dictionary["IsVipOrHigher"] = eventArgs.IsVipOrHigher().ToString();
            dictionary["IsModOrHigher"] = eventArgs.IsModOrHigher().ToString();
            dictionary["User"] = eventArgs.DisplayName ?? string.Empty;
            dictionary["Message"] = ViewerInputSanitizer.Sanitize(eventArgs.Arg);
            dictionary["Command"] = eventArgs.Command ?? string.Empty;
            dictionary["Arg"] = ViewerInputSanitizer.Sanitize(eventArgs.Arg);
            dictionary["TargetUser"] = eventArgs.TargetUser ?? string.Empty;
            dictionary["IsWhisper"] = eventArgs.IsWhisper.ToString();
            dictionary["IsDiscord"] = eventArgs.IsDiscord.ToString();
            dictionary["DiscordMention"] = eventArgs.DiscordMention ?? string.Empty;
            dictionary["FromAlias"] = eventArgs.FromAlias.ToString();
            dictionary["SkipLock"] = eventArgs.SkipLock.ToString();
            dictionary["FromOwnChannel"] = eventArgs.FromOwnChannel.ToString();
            dictionary["Color"] = eventArgs.Color ?? string.Empty;
            dictionary["ResolvedColor"] = eventArgs.ResolvedColor ?? "#FFFFFF";
            dictionary["EmoteUrl"] = eventArgs.EmoteUrl ?? string.Empty;

            if(eventArgs.Args != null && eventArgs.Args.Count > 0)
            {
                dictionary["targetorself"] = ViewerInputSanitizer.Sanitize(eventArgs.Args[0].Replace("@", "").Trim());
                dictionary["target"] = ViewerInputSanitizer.Sanitize(eventArgs.Args[0].Replace("@", "").Trim());
                dictionary["args"] = ViewerInputSanitizer.Sanitize(eventArgs.Arg);
                dictionary["rawInput"] = ViewerInputSanitizer.Sanitize(eventArgs.Arg);
            } else
            {
                dictionary["targetorself"] = eventArgs.Name ?? string.Empty;
                dictionary["target"] = string.Empty;
                dictionary["args"] = string.Empty;
                dictionary["rawInput"] = string.Empty;
            }

            var fragmentsCopy = eventArgs.Fragments != null ? eventArgs.Fragments.ToList() : [];
            var badgesCopy = eventArgs.Badges != null ? eventArgs.Badges.ToList() : [];

            var sanitizedEventArgs = new CommandEventArgs
            {
                IsSub = eventArgs.IsSub,
                IsMod = eventArgs.IsMod,
                IsVip = eventArgs.IsVip,
                IsBroadcaster = eventArgs.IsBroadcaster,
                DisplayName = eventArgs.DisplayName ?? string.Empty,
                Name = eventArgs.Name ?? string.Empty,
                UserId = eventArgs.UserId ?? string.Empty,
                Color = eventArgs.Color ?? string.Empty,
                ResolvedColor = eventArgs.ResolvedColor ?? "#FFFFFF",
                MessageId = eventArgs.MessageId ?? string.Empty,
                Command = eventArgs.Command ?? string.Empty,
                Arg = ViewerInputSanitizer.Sanitize(eventArgs.Arg),
                Args = eventArgs.Args?.Select(a => ViewerInputSanitizer.Sanitize(a)).ToList() ?? [],
                TargetUser = eventArgs.TargetUser ?? string.Empty,
                IsWhisper = eventArgs.IsWhisper,
                IsDiscord = eventArgs.IsDiscord,
                DiscordMention = eventArgs.DiscordMention ?? string.Empty,
                FromAlias = eventArgs.FromAlias,
                SkipLock = eventArgs.SkipLock,
                FromOwnChannel = eventArgs.FromOwnChannel,
                EmoteUrl = eventArgs.EmoteUrl,
                Fragments = fragmentsCopy,
                Badges = badgesCopy
            };

            dictionary["OriginalEventArgs"] = JsonSerializer.Serialize(sanitizedEventArgs);
            if (fragmentsCopy.Count > 0)
            {
                dictionary["Fragments"] = JsonSerializer.Serialize(fragmentsCopy);
            }

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
        public static CommandEventArgs FromDictionary(ConcurrentDictionary<string, string> dictionary)
        {
            return FromDictionaryOrNull(dictionary) ?? new CommandEventArgs();
        }

        public static CommandEventArgs? FromDictionaryOrNull(ConcurrentDictionary<string, string> dictionary)
        {
            if (dictionary == null)
            {
                return null;
            }

            if (dictionary.TryGetValue("OriginalEventArgs", out var json) && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<CommandEventArgs>(json);
                    if (parsed != null)
                    {
                        parsed.Fragments ??= [];
                        parsed.Badges ??= [];
                        return parsed;
                    }
                }
                catch
                {
                    // Fall through to dictionary key reconstruction
                }
            }

            var hasEmoteUrl = dictionary.TryGetValue("EmoteUrl", out var emoteUrl) && !string.IsNullOrWhiteSpace(emoteUrl);
            var hasFragments = dictionary.TryGetValue("Fragments", out var fragmentsJson) && !string.IsNullOrWhiteSpace(fragmentsJson);
            var hasUser = dictionary.TryGetValue("User", out var user) && !string.IsNullOrWhiteSpace(user);

            if (!hasEmoteUrl && !hasFragments && !hasUser)
            {
                return null;
            }

            var fragments = new List<ChatOverlayFragment>();
            if (hasFragments && !string.IsNullOrWhiteSpace(fragmentsJson))
            {
                try
                {
                    var deserialized = JsonSerializer.Deserialize<List<ChatOverlayFragment>>(fragmentsJson);
                    if (deserialized != null)
                    {
                        fragments = deserialized;
                    }
                }
                catch
                {
                    // Ignore malformed Fragments JSON
                }
            }

            var argText = dictionary.TryGetValue("Arg", out var argVal) && !string.IsNullOrWhiteSpace(argVal)
                ? argVal
                : (dictionary.TryGetValue("UserInput", out var uiVal) && !string.IsNullOrWhiteSpace(uiVal)
                    ? uiVal
                    : (dictionary.TryGetValue("Message", out var msgVal) ? msgVal : string.Empty));

            var displayName = dictionary.TryGetValue("DisplayName", out var dn) && !string.IsNullOrWhiteSpace(dn)
                ? dn
                : (user ?? string.Empty);

            var loginName = dictionary.TryGetValue("Name", out var nm) && !string.IsNullOrWhiteSpace(nm)
                ? nm
                : displayName.ToLowerInvariant();

            var userId = dictionary.TryGetValue("UserId", out var uid) ? uid : string.Empty;
            var color = dictionary.TryGetValue("Color", out var col) ? col : string.Empty;

            return new CommandEventArgs
            {
                UserId = userId,
                Name = loginName,
                DisplayName = displayName,
                Color = color,
                ResolvedColor = !string.IsNullOrWhiteSpace(color) ? color : "#FFFFFF",
                Arg = argText,
                Args = string.IsNullOrWhiteSpace(argText) ? [] : argText.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
                EmoteUrl = hasEmoteUrl ? emoteUrl : null,
                Fragments = fragments,
                FromOwnChannel = true
            };
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
