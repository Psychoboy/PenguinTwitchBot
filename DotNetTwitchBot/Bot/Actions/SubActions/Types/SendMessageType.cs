namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    public class SendMessageType : SubActionType
    {
        public SendMessageType()
        {
            SubActionTypes = SubActionTypes.SendMessage;
        }

        public bool UseBot { get; set; } = true;
        public bool FallBack { get; set; } = true;
        public bool StreamOnly { get; set; } = true;
    }
}
