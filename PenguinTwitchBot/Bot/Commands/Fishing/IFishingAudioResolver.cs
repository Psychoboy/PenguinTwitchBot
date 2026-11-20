using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public sealed record ResolvedFishingAudioUrls(
        bool SoundEnabled,
        double SoundVolume,
        string CastSoundUrl,
        string BiteSoundUrl,
        string CatchSoundUrl,
        string AccidentSoundUrl,
        string FrenzySoundUrl);

    public interface IFishingAudioResolver
    {
        ResolvedFishingAudioUrls ResolveForCatch(FishingSettings? settings, FishRarity rarity, string? fishImageFileName);
        string ResolveFrenzySoundUrl(FishingSettings? settings);
        string? FindCustomFishAudioUrl(string? fishImageFileName);
        void InvalidateCache();
    }
}
