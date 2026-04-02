namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
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
