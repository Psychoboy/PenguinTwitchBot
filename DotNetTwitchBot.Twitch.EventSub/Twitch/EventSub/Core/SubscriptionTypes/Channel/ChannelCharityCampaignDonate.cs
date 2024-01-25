using DotNetTwitchBot.Twitch.EventSub.Core.Models.Charity;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Charity Campaign Donate subscription type model
/// <para>Description:</para>
/// <para>Sends an event notification when a user donates to the broadcaster’s charity campaign.</para>
/// </summary>
public sealed class ChannelCharityCampaignDonate : CharityBase
{
    /// <summary>
    /// An ID that uniquely identifies the charity campaign.
    /// </summary>
    public string CampaignId { get; set; } = string.Empty;
    /// <summary>
    /// An ID that uniquely identifies the user that donated to the campaign.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>
    /// The users’s display name.
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>
    /// The users’s login name.
    /// </summary>
    public string UserLogin { get; set; } = string.Empty;

    /// <summary>
    /// An object that contains the amount of the user’s donation.
    /// </summary>
    public CharityAmount Amount { get; set; } = new();

    /// <summary>
    /// The ISO-4217 three-letter currency code that identifies the type of currency in value.
    /// </summary>
    public string Currency { get; set; } = string.Empty;
}