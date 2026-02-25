using Newtonsoft.Json;

namespace DotNetTwitchBot.Bot.KickServices.Models
{
    public class KickUser
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonProperty("profile_pic")]
        public string ProfilePic { get; set; } = string.Empty;

        [JsonProperty("is_staff")]
        public bool IsStaff { get; set; }

        [JsonProperty("is_channel_owner")]
        public bool IsChannelOwner { get; set; }

        [JsonProperty("is_moderator")]
        public bool IsModerator { get; set; }

        [JsonProperty("badges")]
        public List<Badge> Badges { get; set; } = [];

        [JsonProperty("following_since")]
        public DateTime? FollowingSince { get; set; }

        [JsonProperty("subscribed_for")]
        public int SubscribedFor { get; set; }

        [JsonProperty("banned")]
        public DateTime? Banned { get; set; }
    }
}
