namespace DotNetTwitchBot.Twitch.EventSub.Twitch.EventSub.Core.Models.Chat
{
    public sealed class ChatReply
    {
        public string ParentMessageId { get; set; } = string.Empty;
        public string ParentMessageBody { get; set; } = string.Empty;
        public string ParentUserId { get; set; } = string.Empty;
        public string ParentUserName { get; set; } = string.Empty;
        public string ParentUserLogin { get; set; } = string.Empty;
        public string ThreadMessageId { get; set; } = string.Empty;
        public string ThreadUserId { get; set; } = string.Empty;
        public string ThreadUserName { get; set; } = string.Empty;
        public string ThreadUserLogin { get; set; } = string.Empty;
    }
}
