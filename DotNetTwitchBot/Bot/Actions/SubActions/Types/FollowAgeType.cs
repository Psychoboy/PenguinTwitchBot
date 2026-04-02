namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    public class FollowAgeType : SubActionType
    {
        public FollowAgeType()
        {
            SubActionTypes = SubActionTypes.Followage;
        }

        public new string Text { get; set; } = "%targetorself%";
    }
}
