using TwitchLib.Api.Helix.Models.Channels.GetChannelEditors;
using TwitchLib.Api.Helix.Models.Channels.GetChannelFollowers;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using TwitchLibChannelInformation = TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation;
using TwitchLibChannelEditor = TwitchLib.Api.Helix.Models.Channels.GetChannelEditors.ChannelEditor;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class ChannelsClient(ILogger<ChannelsClient> logger, IChannelsTransport transport) : TwitchClientRetryBase(logger), IChannelsClient
{
    public Task<GetChannelInformationResponse> GetChannelInformationAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelInformationAsync(clientId, accessToken, broadcasterId), "fetch channel information");
    }

    public Task<GetChannelFollowersResponse> GetChannelFollowersAsync(string clientId, string? accessToken, string broadcasterId, string userId, int first, string? after)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelFollowersAsync(clientId, accessToken, broadcasterId, userId, first, after), "fetch channel followers");
    }

    public Task<GetChannelEditorsResponse> GetChannelEditorsAsync(string clientId, string? accessToken, string broadcasterId)
    {
        return ExecuteWithRetryAsync(() => transport.GetChannelEditorsAsync(clientId, accessToken, broadcasterId), "fetch channel editors");
    }

    /// <summary>
    /// Maps a TwitchLib ChannelInformation to the internal domain model
    /// </summary>
    internal static Models.Channels.ChannelInformation MapToChannelInformation(TwitchLibChannelInformation source)
    {
        return new Models.Channels.ChannelInformation(
            BroadcasterId: source.BroadcasterId,
            BroadcasterLogin: source.BroadcasterLogin,
            BroadcasterName: source.BroadcasterName,
            BroadcasterLanguage: source.BroadcasterLanguage,
            GameId: source.GameId,
            GameName: source.GameName,
            Title: source.Title,
            Delay: source.Delay);
    }

    /// <summary>
    /// Maps a TwitchLib ChannelEditor to the internal domain model
    /// </summary>
    internal static Models.Channels.ChannelEditor MapToChannelEditor(TwitchLibChannelEditor source)
    {
        return new Models.Channels.ChannelEditor(
            UserId: source.UserId,
            UserName: source.UserName,
            UserLogin: source.UserName.ToLower(), // Use lowercased UserName as login
            CreatedAt: source.CreatedAt);
    }
}
