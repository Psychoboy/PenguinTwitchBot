namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
{
    public class RandomIntType : SubActionType
    {
        public RandomIntType()
        {
            SubActionTypes = SubActionTypes.RandomInt;
        }

        public int Min { get; set; } = 0;
        public int Max { get; set; } = 100;
    }
}
