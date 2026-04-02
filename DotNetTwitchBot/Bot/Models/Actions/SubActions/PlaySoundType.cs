using DotNetTwitchBot.Bot.Actions.SubActions;

namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
{
    public class PlaySoundType : SubActionType
    {
        public PlaySoundType()
        {
            SubActionTypes = SubActionTypes.PlaySound;
        }

        // File property inherited from base class
    }
}
