using PenguinTwitchBot.TwitchApi.Models.Clips;

namespace PenguinTwitchBot.TwitchApi.Helix;

public interface IClipsTransport
{
    Task<GetClipsResponse> GetClipsAsync(string clientId, string? accessToken, string? broadcasterId, string? userId, int first, bool? isFeatured);
    Task<GetClipsResponse> GetClipsByIdAsync(string clientId, string? accessToken, List<string> clipIds);
}
