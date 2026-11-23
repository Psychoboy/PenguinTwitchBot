namespace PenguinTwitchBot.Bot.TwitchServices.TwitchModels
{
    public class StreamTeamMember
    {
        public string UserId { get; set; } = string.Empty;
        public string UserLogin { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> TeamNames { get; set; } = [];
    }
}
