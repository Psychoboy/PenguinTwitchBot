namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
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
