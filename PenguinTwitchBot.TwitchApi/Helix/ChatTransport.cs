using PenguinTwitchBot.TwitchApi.Models.Chat;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChatTransport : IChatTransport
{
    public async Task<SendChatMessageResponse> SendChatMessageAsync(string clientId, string? accessToken, SendChatMessageRequest request)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/messages")
        {
            Content = HelixJson.CreateJsonContent(new SendChatMessageRequestBody(
                BroadcasterId: request.BroadcasterId,
                SenderId: request.SenderId,
                Message: request.Message,
                ForSourceOnly: request.ForSourceOnly,
                ReplyParentMessageId: request.ReplyParentMessageId))
        };

        using var response = await http.SendAsync(message);
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<SendChatMessageApiResponse>(response) ?? new SendChatMessageApiResponse([]);
        return MapToResponse(payload);
    }

    public async Task<GetChattersPageResponse> GetChattersAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string? after)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = $"chat/chatters?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&moderator_id={Uri.EscapeDataString(moderatorId)}";
        if (!string.IsNullOrWhiteSpace(after))
        {
            url += $"&after={Uri.EscapeDataString(after)}";
        }

        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var payload = await HelixJson.DeserializeAsync<GetChattersApiResponse>(response) ?? new GetChattersApiResponse([], null);

        return MapToChattersPageResponse(payload);
    }

    public async Task SendShoutoutAsync(string clientId, string? accessToken, string fromBroadcasterId, string toBroadcasterId, string moderatorId)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url =
            $"chat/shoutouts?from_broadcaster_id={Uri.EscapeDataString(fromBroadcasterId)}" +
            $"&to_broadcaster_id={Uri.EscapeDataString(toBroadcasterId)}" +
            $"&moderator_id={Uri.EscapeDataString(moderatorId)}";

        using var response = await http.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendChatAnnouncementAsync(string clientId, string? accessToken, string broadcasterId, string moderatorId, string message)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url =
            $"chat/announcements?broadcaster_id={Uri.EscapeDataString(broadcasterId)}" +
            $"&moderator_id={Uri.EscapeDataString(moderatorId)}";

        using var response = await http.PostAsync(
            url,
            HelixJson.CreateJsonContent(new SendChatAnnouncementRequestBody(Message: message)));
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ChatBadgeSet>> GetGlobalChatBadgesAsync(string clientId, string? accessToken)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        using var response = await http.GetAsync("chat/badges/global");
        response.EnsureSuccessStatusCode();
        var payload = await HelixJson.DeserializeAsync<ChatBadgesApiResponse>(response) ?? new ChatBadgesApiResponse([]);
        return MapToBadgeSets(payload.Data);
    }

    public async Task<IReadOnlyList<ChatBadgeSet>> GetChannelChatBadgesAsync(string clientId, string? accessToken, string broadcasterId)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        using var response = await http.GetAsync($"chat/badges?broadcaster_id={Uri.EscapeDataString(broadcasterId)}");
        response.EnsureSuccessStatusCode();
        var payload = await HelixJson.DeserializeAsync<ChatBadgesApiResponse>(response) ?? new ChatBadgesApiResponse([]);
        return MapToBadgeSets(payload.Data);
    }

    private static SendChatMessageResponse MapToResponse(SendChatMessageApiResponse source)
    {
        var data = source.Data
            .Select(item => new SendChatMessageResult(
                MessageId: item.MessageId,
                IsSent: item.IsSent,
                DropReason: item.DropReason == null
                    ? null
                    : new SendChatMessageDropReason(item.DropReason.Code, item.DropReason.Message)))
            .ToList();

        return new SendChatMessageResponse(data);
    }

    private static GetChattersPageResponse MapToChattersPageResponse(GetChattersApiResponse source)
    {
        var chatters = source.Data
            .Select(item => new Chatter(item.UserId, item.UserLogin))
            .ToList();

        return new GetChattersPageResponse(
            Data: chatters,
            Cursor: source.Pagination?.Cursor);
    }

    private static IReadOnlyList<ChatBadgeSet> MapToBadgeSets(IReadOnlyList<ChatBadgeSetApiItem> source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        return source.Select(MapToBadgeSet).ToList();
    }

    private static ChatBadgeSet MapToBadgeSet(ChatBadgeSetApiItem source)
    {
        var versions = source.Versions
            .Select(v => new ChatBadgeVersion(
                Id: v.Id,
                ImageUrl1x: v.ImageUrl1x))
            .ToList();

        return new ChatBadgeSet(
            SetId: source.SetId,
            Versions: versions);
    }

    private sealed record SendChatMessageRequestBody(
        [property: JsonPropertyName("broadcaster_id")] string BroadcasterId,
        [property: JsonPropertyName("sender_id")] string SenderId,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("for_source_only")] bool? ForSourceOnly,
        [property: JsonPropertyName("reply_parent_message_id")] string? ReplyParentMessageId);

    private sealed record SendChatAnnouncementRequestBody(
        [property: JsonPropertyName("message")] string Message);

    private sealed record SendChatMessageApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<SendChatMessageApiResult> Data);

    private sealed record SendChatMessageApiResult(
        [property: JsonPropertyName("message_id")] string MessageId,
        [property: JsonPropertyName("is_sent")] bool IsSent,
        [property: JsonPropertyName("drop_reason")] SendChatMessageApiDropReason? DropReason);

    private sealed record SendChatMessageApiDropReason(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record GetChattersApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<GetChattersApiItem> Data,
        [property: JsonPropertyName("pagination")] PaginationApi? Pagination);

    private sealed record GetChattersApiItem(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_login")] string UserLogin);

    private sealed record ChatBadgesApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ChatBadgeSetApiItem> Data);

    private sealed record ChatBadgeSetApiItem(
        [property: JsonPropertyName("set_id")] string SetId,
        [property: JsonPropertyName("versions")] IReadOnlyList<ChatBadgeVersionApiItem> Versions);

    private sealed record ChatBadgeVersionApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("image_url_1x")] string ImageUrl1x);

    private sealed record PaginationApi(
        [property: JsonPropertyName("cursor")] string? Cursor);
}
