namespace DotNetTwitchBot.Bot.Models.Actions.SubActions
{
    public class ExternalApiType : SubActionType
    {
        public string HttpMethod { get; set; } = "GET";
        public string Headers { get; set; } = "Accept: text/plain";
        //URL is handled by Text property in SubActionType
    }
}
