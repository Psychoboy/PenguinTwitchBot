using DotNetTwitchBot.Twitch.EventSub.Core.Models.ShieldMode;

namespace DotNetTwitchBot.Twitch.EventSub.Core.SubscriptionTypes.Channel;

/// <summary>
/// Channel Shield Mode End subscription type model
/// <para>Description:</para>
/// <para>Defines the Shield Mode data that you receive when the channel.shield_mode.end event occurs.</para>
/// </summary>
public sealed class ChannelShieldModeEnd : ShieldModeBase
{
    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the moderator deactivated Shield Mode.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.MinValue;
}