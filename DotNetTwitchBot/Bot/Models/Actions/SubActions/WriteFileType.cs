namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
{
    public class WriteFileType : SubActionType
    {
        public WriteFileType()
        {
            SubActionTypes = SubActionTypes.WriteFile;
        }

        public bool Append { get; set; } = true;
    }
}
