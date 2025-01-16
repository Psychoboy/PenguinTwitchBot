using MediatR;
using TwitchLib.Api.Helix.Models.Clips.GetClips;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace DotNetTwitchBot.Application.Clips
{
    public class ClipNotification(Clip clip, User user, string gameUrl) : INotification
    {
        public Clip Clip { get; private set; } = clip;
        public User User { get; private set; } = user;
        public string GameUrl { get; private set; } = gameUrl;
    }
}
