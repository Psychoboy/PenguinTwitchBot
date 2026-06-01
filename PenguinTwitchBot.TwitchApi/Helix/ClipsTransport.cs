using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Clips.GetClips;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ClipsTransport : IClipsTransport
{
    public Task<GetClipsResponse> GetClipsAsync(string clientId, string? accessToken, string? broadcasterId, string? userId, int first, bool? isFeatured)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Clips.GetClipsAsync(null, null, userId, null, null, null, null, isFeatured, first, accessToken);
    }

    public Task<GetClipsResponse> GetClipsByIdAsync(string clientId, string? accessToken, List<string> clipIds)
    {
        var api = CreateApi(clientId, accessToken);
        return api.Helix.Clips.GetClipsAsync(clipIds, null, null, null, null, null, null, null, 1, accessToken);
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
}
