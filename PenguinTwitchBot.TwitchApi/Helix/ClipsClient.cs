using TwitchLib.Api.Helix.Models.Clips.GetClips;
using TwitchLibClip = TwitchLib.Api.Helix.Models.Clips.GetClips.Clip;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

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

    /// <summary>
    /// Maps a TwitchLib Clip to the internal domain model
    /// </summary>
    public static Models.Clips.Clip MapToClip(TwitchLibClip source)
    {
        return new Models.Clips.Clip(
            Id: source.Id,
            Url: source.Url,
            EmbedUrl: source.EmbedUrl,
            Title: source.Title,
            ViewCount: source.ViewCount,
            CreatedAt: DateTime.Parse(source.CreatedAt),
            Language: source.Language,
            ThumbnailUrl: source.ThumbnailUrl,
            BroadcasterName: source.BroadcasterName,
            BroadcasterId: source.BroadcasterId,
            CreatorName: source.CreatorName,
            CreatorId: source.CreatorId,
            Duration: source.Duration,
            VideoId: source.VideoId,
            GameId: source.GameId);
    }
}
