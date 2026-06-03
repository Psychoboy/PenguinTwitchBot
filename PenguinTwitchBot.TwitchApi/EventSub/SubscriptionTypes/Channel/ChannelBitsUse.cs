using PenguinTwitchBot.TwitchApi.EventSub.Models.Bits;

namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel
{
    public sealed class ChannelBitsUse
    {
        public string BroadcasterUserId { get; set; } = string.Empty;
        public string BroadcasterUserLogin { get; set; } = string.Empty;
        public string BroadcasterUserName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserLogin { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Bits { get; set; }
        /// <summary>
        /// Possible values are: cheer, power_up, custom_power_up
        /// </summary>
        public string Type { get; set; } = string.Empty;
        public BitsMessage? Message { get; set; }
        public PowerUp? PowerUp { get; set; }
        public CustomPowerUp? CustomPowerUp { get; set; }
    }
}