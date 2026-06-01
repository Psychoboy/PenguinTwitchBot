using TwitchLib.Api.Helix.Models.Clips.GetClips;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public interface IClipsTransport
{
    Task<GetClipsResponse> GetClipsAsync(string clientId, string? accessToken, string? broadcasterId, string? userId, int first, bool? isFeatured);
    Task<GetClipsResponse> GetClipsByIdAsync(string clientId, string? accessToken, List<string> clipIds);
}
