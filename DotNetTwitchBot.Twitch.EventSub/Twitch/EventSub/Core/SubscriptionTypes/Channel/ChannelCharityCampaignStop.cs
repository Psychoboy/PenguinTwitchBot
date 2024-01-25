using DotNetTwitchBot.Twitch.EventSub.Core.Models.Charity;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Charity Campaign Stop subscription type model
/// <para>Description:</para>
/// <para>Sends a notification when the broadcaster stops a charity campaign.</para>
/// <para>The event data does not include information about the charity such as its name, description, and logo.</para>
/// <para>To get that information, subscribe to the Start event or call the Get Charity Campaign endpoint.</para>
/// </summary>
public sealed class ChannelCharityCampaignStop
{
    /// <summary>
    /// An ID that identifies the charity campaign.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// An ID that uniquely identifies the broadcaster that’s running the campaign.
    /// </summary>
    public string BroadcasterId { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster’s login name.
    /// </summary>
    public string BroadcasterLogin { get; set; } = string.Empty;
    /// <summary>
    /// The broadcaster’s display name.
    /// </summary>
    public string BroadcasterName { get; set; } = string.Empty;
    /// <summary>
    /// An object that contains the final amount of donations that the campaign received.
    /// </summary>
    public CharityAmount CurrentAmount { get; set; } = new();
    /// <summary>
    /// An object that contains the campaign’s target fundraising goal.
    /// </summary>
    public CharityAmount TargetAmount { get; set; } = new();
    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the broadcaster stopped the campaign.
    /// </summary>
    public DateTimeOffset StoppedAt { get; set; } = DateTimeOffset.MinValue;
}