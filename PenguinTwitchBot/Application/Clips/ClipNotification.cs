using PenguinTwitchBot.Bot.Twitch.Models.Clips;
using PenguinTwitchBot.Bot.Twitch.Models.Users;

namespace PenguinTwitchBot.Application.Clips
{
    public class ClipNotification(Clip clip, User user, string gameUrl) : Notifications.INotification
    {
        public Clip Clip { get; private set; } = clip;
        public User User { get; private set; } = user;
        public string GameUrl { get; private set; } = gameUrl;
    }
}
