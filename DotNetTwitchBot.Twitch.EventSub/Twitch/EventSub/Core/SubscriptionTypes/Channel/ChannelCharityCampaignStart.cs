using DotNetTwitchBot.Twitch.EventSub.Core.Models.Charity;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Charity Campaign Start subscription type model
/// <para>Description:</para>
/// <para>Sends a notification when the broadcaster starts a charity campaign.</para>
/// <para>It’s possible to receive this event after the Progress event.</para>
/// </summary>
public sealed class ChannelCharityCampaignStart : CharityBase
{
    /// <summary>
    /// An ID that identifies the charity campaign.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// A URL to the charity’s website.
    /// </summary>
    public string CharityWebsite { get; set; } = string.Empty;
    /// <summary>
    /// An object that contains the current amount of donations that the campaign has received.
    /// </summary>
    public CharityAmount CurrentAmount { get; set; } = new();
    /// <summary>
    /// An object that contains the campaign’s target fundraising goal.
    /// </summary>
    public CharityAmount TargetAmount { get; set; } = new();
    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the broadcaster started the campaign.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
}