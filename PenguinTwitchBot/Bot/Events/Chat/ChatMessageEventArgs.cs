namespace PenguinTwitchBot.Bot.Events.Chat
{
    public class ChatMessageEventArgs : BaseChatEventArgs
    {
        public string Message { get; set; } = "";
    }
}
