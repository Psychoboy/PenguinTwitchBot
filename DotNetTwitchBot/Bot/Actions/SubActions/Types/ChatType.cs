namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    public abstract class ChatType : SubActionType
    {
        public bool UseBot { get; set; } = true;
        public bool FallBack { get; set; } = true;
        public bool StreamOnly { get; set; } = true;
    }
}
