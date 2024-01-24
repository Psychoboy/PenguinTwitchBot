namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel GuestStar Settings Update subscription type model
/// <para>Description:</para>
/// <para>The channel.guest_star_settings.update subscription type sends a notification when the host preferences for Guest Star have been updated.</para>
/// </summary>
public sealed class ChannelGuestStarSettingsUpdate
{
    /// <summary>
    /// User ID of the host channel
    /// </summary>
    public string BroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster display name
    /// </summary>
    public string BroadcasterUserName { get; set; } = string.Empty;
    /// <summary>
    /// the broadcaster login
    /// </summary>
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// Flag determining if Guest Star moderators have access to control whether a guest is live once assigned to a slot.
    /// </summary>
    public bool IsModeratorSendLiveEnabled { get; set; }
    /// <summary>
    /// Number of slots the Guest Star call interface will allow the host to add to a call.
    /// </summary>
    public int SlotCount { get; set; }
    /// <summary>
    /// Flag determining if browser sources subscribed to sessions on this channel should output audio
    /// </summary>
    public bool IsBrowserSourceAudioEnabled { get; set; }
    /// <summary>
    /// This setting determines how the guests within a session should be laid out within a group browser source. Can be one of the following values:
    /// <para>tiled — All live guests are tiled within the browser source with the same size.</para>
    /// <para>screenshare — All live guests are tiled within the browser source with the same size. If there is an active screen share, it is sized larger than the other guests.</para>
    /// </summary>
    public string GroupLayout { get; set; } = string.Empty;
}