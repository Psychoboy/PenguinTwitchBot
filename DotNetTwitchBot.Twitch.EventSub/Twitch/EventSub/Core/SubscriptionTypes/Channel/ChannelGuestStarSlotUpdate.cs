using DotNetTwitchBot.Twitch.EventSub.Core.Models.GuestStar;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel
{
    /// <summary>
    /// Channel GuestStar Slot Update subscription type model
    /// <para>Description:</para>
    /// <para>The channel.guest_star_slot.update subscription type sends a notification when a slot setting is updated in an active Guest Star session.</para>
    /// </summary>
    public class ChannelGuestStarSlotUpdate : ChannelGuestStarBase
    {
        /// <summary>
        /// Flag that signals whether the host is allowing the slot’s video to be seen by participants within the session.
        /// </summary>
        public bool HostVideoEnabled { get; set; }
        /// <summary>
        /// Flag that signals whether the host is allowing the slot’s audio to be heard by participants within the session.
        /// </summary>
        public bool HostAudioEnabled { get; set; }
        /// <summary>
        /// Value between 0-100 that represents the slot’s audio level as heard by participants within the session.
        /// </summary>
        public int HostVolume { get; set; }
    }
}