namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    public class WatchTimeType : SubActionType
    {
        public WatchTimeType()
        {
            SubActionTypes = SubActionTypes.WatchTime;
        }

        public new string Text { get; set; } = "%targetorself%";
    }
}
