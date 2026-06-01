using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Chat.Badges.GetChannelChatBadges;
using TwitchLib.Api.Helix.Models.Chat.Badges.GetGlobalChatBadges;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
using PenguinTwitchBot.TwitchApi.Models.Chat;
using TwitchLibSendChatMessageRequest = TwitchLib.Api.Helix.Models.Channels.SendChatMessage.SendChatMessageRequest;
using TwitchLibSendChatMessageResponse = TwitchLib.Api.Helix.Models.Channels.SendChatMessage.SendChatMessageResponse;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChatTransport : IChatTransport
{
    public async Task<SendChatMessageResponse> SendChatMessageAsync(string clientId, string? accessToken, SendChatMessageRequest request)
    {
        var api = CreateApi(clientId, accessToken);
        var twitchResponse = await api.Helix.Chat.SendChatMessage(MapToTwitchRequest(request), accessToken);
        return MapToResponse(twitchResponse);
    }

    public Task<GetChattersResponse> GetChattersAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? after)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Chat.GetChattersAsync(broadcasterId, moderatorId, after: after, accessToken: accessToken);
    }

    public Task SendShoutoutAsync(string clientId, string? accessToken, string fromBroadcasterId, string toBroadcasterId, string moderatorId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Chat.SendShoutoutAsync(fromBroadcasterId, toBroadcasterId, moderatorId, accessToken);
    }

    public Task SendChatAnnouncementAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string message)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Chat.SendChatAnnouncementAsync(broadcasterId, moderatorId, message, accessToken: accessToken);
    }

    public Task<GetGlobalChatBadgesResponse> GetGlobalChatBadgesAsync(string clientId, string? accessToken)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Chat.GetGlobalChatBadgesAsync(accessToken);
    }

    public Task<GetChannelChatBadgesResponse> GetChannelChatBadgesAsync(string clientId, string? accessToken, string broadcasterId)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Chat.GetChannelChatBadgesAsync(broadcasterId, accessToken);
    }

    private static TwitchAPI CreateApi(string clientId, string? accessToken)
    {
        var api = new TwitchAPI();
        api.Settings.ClientId = clientId;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            api.Settings.AccessToken = accessToken;
        }

        return api;
    }

    private static TwitchLibSendChatMessageRequest MapToTwitchRequest(SendChatMessageRequest request)
    {
        return new TwitchLibSendChatMessageRequest
        {
            BroadcasterId = request.BroadcasterId,
            SenderId = request.SenderId,
            Message = request.Message,
            ForSourceOnly = request.ForSourceOnly,
            ReplyParentMessageId = request.ReplyParentMessageId,
        };
    }

    private static SendChatMessageResponse MapToResponse(TwitchLibSendChatMessageResponse source)
    {
        var data = source.Data?
            .Select(item => new SendChatMessageResult(
                MessageId: item.MessageId,
                IsSent: item.IsSent,
                DropReason: item.DropReason == null
                    ? null
                    : new SendChatMessageDropReason(item.DropReason.Code, item.DropReason.Message)))
            .ToList()
            ?? [];

        return new SendChatMessageResponse(data);
    }
}
