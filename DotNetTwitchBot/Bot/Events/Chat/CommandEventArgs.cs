using System.ComponentModel;

namespace DotNetTwitchBot.Bot.Events.Chat
{
    public sealed class CommandEventArgs : BaseChatEventArgs
    {
        public string Command { get; set; } = "";
        public string Arg { get; set; } = "";
        public List<string> Args { get; set; } = new List<string>();
        public string TargetUser { get; set; } = "";
        public bool IsWhisper { get; set; } = false;
        public bool IsDiscord { get; set; } = false;
        public string DiscordMention { get; set; } = "";

        public bool FromAlias { get; set; } = false;
        public bool SkipLock { get; set; } = false;
    }
}
