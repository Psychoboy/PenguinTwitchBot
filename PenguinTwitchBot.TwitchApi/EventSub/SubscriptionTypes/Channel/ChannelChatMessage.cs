using PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

namespace PenguinTwitchBot.TwitchApi.EventSub.SubscriptionTypes.Channel;

public sealed class ChannelChatMessage
{
    //
    // Summary:
    //     The broadcaster user ID.
    public string BroadcasterUserId { get; set; } = string.Empty;

    //
    // Summary:
    //     The broadcaster display name.
    public string BroadcasterUserName { get; set; } = string.Empty;

    //
    // Summary:
    //     The broadcaster login.
    public string BroadcasterUserLogin { get; set; } = string.Empty;

    //
    // Summary:
    //     The user ID of the user that sent the message.
    public string ChatterUserId { get; set; } = string.Empty;

    //
    // Summary:
    //     The user name of the user that sent the message.
    public string ChatterUserName { get; set; } = string.Empty;

    //
    // Summary:
    //     The user login of the user that sent the message.
    public string ChatterUserLogin { get; set; } = string.Empty;

    //
    // Summary:
    //     A UUID that identifies the message.
    public string MessageId { get; set; } = string.Empty;
    // Summary:
    //     The structured chat message
    public ChatMessage Message { get; set; } = new ChatMessage();
    public string Color { get; set; } = string.Empty;

    public ChatBadge[] Badges { get; set; } = [];
    
   //
    // Summary:
    //     The type of message. Possible values:
    public string MessageType { get; set; } = string.Empty;

    //
    // Summary:
    //     Metadata if this message is a cheer.
    public ChatCheer? Cheer { get; set; }

    //
    // Summary:
    //     Metadata if this message is a reply.
    public ChatReply? Reply { get; set; }

    //
    // Summary:
    //     Optional. The ID of a channel points custom reward that was redeemed.
    public string? ChannelPointsCustomRewardId { get; set; }

    //
    // Summary:
    //     Optional. The broadcaster user ID of the channel the message was sent from.
    public string? SourceBroadcasterUserId { get; set; }

    //
    // Summary:
    //     Optional. The user name of the broadcaster of the channel the message was sent
    //     from.
    public string? SourceBroadcasterUserName { get; set; }

    //
    // Summary:
    //     Optional. The login of the broadcaster of the channel the message was sent from.
    public string? SourceBroadcasterUserLogin { get; set; }

    //
    // Summary:
    //     Optional. The UUID that identifies the source message from the channel the message
    //     was sent from.
    public string? SourceMessageId { get; set; }

    //
    // Summary:
    //     Optional. The list of chat badges for the chatter in the channel the message
    //     was sent from.
    public ChatBadge[]? SourceBadges { get; set; }

    //
    // Summary:
    //     Optional. Determines if a message delivered during a shared chat session is only
    //     sent to the source channel. Has no effect if the message is not sent during a
    //     shared chat session.
    public bool? IsSourceOnly { get; set; }

    public string? ChannelPointsAnimationId { get; set; }

    //
    // Summary:
    //     Returns true if viewer is a subscriber
    public bool IsSubscriber => Badges.Any(x => x.SetId.Equals("subscriber", StringComparison.OrdinalIgnoreCase) || x.SetId.Equals("founder", StringComparison.OrdinalIgnoreCase));

    //
    // Summary:
    //     Returns true if viewer is a moderator
    public bool IsModerator => Badges.Any(x => x.SetId.Equals("moderator", StringComparison.OrdinalIgnoreCase) || x.SetId.Equals("lead_moderator", StringComparison.OrdinalIgnoreCase));

    //
    // Summary:
    //     Returns true if viewer is a broadcaster
    public bool IsBroadcaster => Badges.Any(x => x.SetId.Equals("broadcaster", StringComparison.OrdinalIgnoreCase));

    //
    // Summary:
    //     Returns true if viewer is a vip
    public bool IsVip => Badges.Any(x => x.SetId.Equals("vip", StringComparison.OrdinalIgnoreCase));

    //
    // Summary:
    //     Returns true if viewer is a staff member
    public bool IsStaff => Badges.Any(x => x.SetId.Equals("staff", StringComparison.OrdinalIgnoreCase) || x.SetId.Equals("admin", StringComparison.OrdinalIgnoreCase));
}
