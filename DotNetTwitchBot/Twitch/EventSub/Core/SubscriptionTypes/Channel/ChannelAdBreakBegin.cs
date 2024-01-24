namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Ad Break begin subscription type model
/// <para>Description:</para>
/// <para>a User runs a midroll commercial break, either manually or automatically via ads manager.</para>
/// </summary>
public sealed class ChannelAdBreakBegin
{
    /// <summary>
    /// Length in seconds of the mid-roll ad break requested
    /// </summary>
    public int DurationSeconds { get; set; }
    /// <summary>
    /// The UTC timestamp of when the ad break began, in RFC3339 format. Note that there is potential delay between this event, when the streamer requested the ad break, and when the viewers will see ads.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
    /// <summary>
    /// Indicates if the ad was automatically scheduled via Ads Manager
    /// </summary>
    public bool IsAutomatic { get; set; }
    /// <summary>
    /// The broadcaster’s user ID for the channel the ad was run on.
    /// </summary>
    public string BroadcasterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster’s user login for the channel the ad was run on.
    /// </summary>
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster’s user display name for the channel the ad was run on.
    /// </summary>
    public string BroadcasterUserName { get; set; } = string.Empty;
    /// <summary>
    /// The ID of the user that requested the ad. For automatic ads, this will be the ID of the broadcaster.
    /// </summary>
    public string RequesterUserId { get; set; } = string.Empty;
    /// <summary>
    /// The login of the user that requested the ad.
    /// </summary>
    public string RequesterUserLogin { get; set; } = string.Empty;
    /// <summary>
    /// The display name of the user that requested the ad.
    /// </summary>
    public string RequesterUserName { get; set; } = string.Empty;
}