using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Services.Chat
{
    /// <inheritdoc />
    public class ChatColorService : IChatColorService
    {
        // Colors used when a chatter has no Twitch color set.
        // Chosen to be visible on both light and dark backgrounds.
        private static readonly string[] Palette =
        [
            "#FF4500", "#1E90FF", "#00FF7F", "#9ACD32", "#FF69B4",
            "#5F9EA0", "#FF7F50", "#2E8B57", "#DAA520", "#8A2BE2",
            "#DC143C", "#00CED1", "#FF6347", "#7B68EE", "#3CB371",
            "#BA55D3", "#20B2AA", "#F4A460", "#4169E1", "#FF1493",
        ];

        private readonly ConcurrentDictionary<string, string> _assigned = new();
        private long _nextIndex;

        /// <inheritdoc />
        public string GetOrAssignColor(string userId, string? twitchColor)
        {
            if (!string.IsNullOrWhiteSpace(twitchColor))
                return twitchColor;

            return _assigned.GetOrAdd(userId, _ =>
            {
                var idx = Interlocked.Increment(ref _nextIndex) - 1;
                return Palette[idx % (long)Palette.Length];
            });
        }
    }
}
