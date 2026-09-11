namespace PenguinTwitchBot.Bot.Notifications
{
    /// <summary>Topic names widgets subscribe to on the single /ws endpoint.</summary>
    public static class WsTopics
    {
        public const string Chat = "chat";
        public const string Alerts = "alerts";
        public const string Clips = "clips";
        public const string Fishing = "fishing";
        public const string Wheel = "wheel";
        public const string Overlay = "overlay";
        public const string Events = "events";

        public static readonly IReadOnlyList<string> All = [Chat, Alerts, Clips, Fishing, Wheel, Overlay, Events];

        public static HashSet<string> Parse(IEnumerable<string?>? topics)
        {
            var parsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (topics == null) return parsed;

            foreach (var topic in topics)
            {
                if (string.IsNullOrWhiteSpace(topic)) continue;
                foreach (var part in topic.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    parsed.Add(part);
            }
            return parsed;
        }
    }
}
