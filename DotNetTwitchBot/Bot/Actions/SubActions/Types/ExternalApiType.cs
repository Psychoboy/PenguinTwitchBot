namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    public class ExternalApiType : SubActionType
    {
        public ExternalApiType()
        {
            SubActionTypes = SubActionTypes.ExternalApi;
        }

        public string HttpMethod { get; set; } = "GET";
        public string Headers { get; set; } = "Accept: text/plain";
        //URL is handled by Text property in SubActionType
    }
}
