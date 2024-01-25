using DotNetTwitchBot.Twitch.EventSub.Core.Models.Charity;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Charity Campaign Progress subscription type model
/// <para>Description:</para>
/// <para>Sends notifications when progress is made towards the campaign’s goal or when the broadcaster changes the fundraising goal.</para>
/// <para>It’s possible to receive this event before the Start event.</para>
/// <para>The event data includes the charity’s name and logo but not its description and website.</para>
/// <para>To get that information, subscribe to the Start event or call the Get Charity Campaign endpoint.</para>
/// <para>To get donation information, subscribe to the channel.charity_campaign.donate event.</para>
/// </summary>
public sealed class ChannelCharityCampaignProgress : CharityBase
{
    /// <summary>
    /// An ID that identifies the charity campaign.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// An object that contains the current amount of donations that the campaign has received.
    /// </summary>
    public CharityAmount CurrentAmount { get; set; } = new();
    /// <summary>
    /// An object that contains the campaign’s target fundraising goal.
    /// </summary>
    public CharityAmount TargetAmount { get; set; } = new();
}