namespace DotNetTwitchBot.Bot.Events
{
    public class CheerEventArgs
    {
        public string? Name { get; set; }
        public string? UserId { get; set; }
        public string? DisplayName { get; set; } = "";
        public string Message { get; set; } = "";
        public int Amount { get; set; }

        public bool IsAnonymous { get; set; }

    }
}