using System.ComponentModel;

namespace DotNetTwitchBot.Bot.Events
{
    public sealed class CommandEventArgs
    {
        public string Command { get; set; } = "";
        public string Arg { get; set; } = "";
        public List<string> Args { get; set; } = new List<string>();
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";

        public string TargetUser { get; set; } = "";
        public bool IsWhisper { get; set; } = false;
        public bool isSub { get; set; }
        public bool isMod { get; set; }
        public bool isVip { get; set; }
        public bool isBroadcaster { get; set; }
        public bool isDiscord { get; set; } = false;
        public string DiscordMention { get; set; } = "";

        public bool FromAlias { get; set; } = false;

        public bool SubOrHigher()
        {
            return isSub || isMod || isVip || isBroadcaster;
        }
    }
}
