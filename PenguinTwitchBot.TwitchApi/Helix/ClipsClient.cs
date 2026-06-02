using PenguinTwitchBot.TwitchApi.Models.Clips;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ClipsClient(ILogger<ClipsClient> logger, IClipsTransport transport) : TwitchClientRetryBase(logger), IClipsClient
{
    public Task<GetClipsResponse> GetClipsAsync(string clientId, string? accessToken, string? broadcasterId, string? userId, int first, bool? isFeatured)
    {
        return ExecuteWithRetryAsync(() => transport.GetClipsAsync(clientId, accessToken, broadcasterId, userId, first, isFeatured), "fetch clips");
    }

    public Task<GetClipsResponse> GetClipsByIdAsync(string clientId, string? accessToken, List<string> clipIds)
    {
        return ExecuteWithRetryAsync(() => transport.GetClipsByIdAsync(clientId, accessToken, clipIds), "fetch clips by id");
    }
}
