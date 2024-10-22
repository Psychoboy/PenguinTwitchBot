namespace DotNetTwitchBot.Bot.TwitchServices.TwitchModels
{
    public class OnlineStream
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DisplayName {  get; set; } = string.Empty;
        public string Game {  get; set; } = string.Empty;
    }
}
