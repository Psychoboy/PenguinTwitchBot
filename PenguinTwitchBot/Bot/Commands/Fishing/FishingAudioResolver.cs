using PenguinTwitchBot.Database.Bot.Models.Fishing;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    /// <summary>
    /// Resolves fishing overlay audio URLs with zero browser HEAD probing.
    /// Priority for catches: per-fish custom audio in /fishes/ > global FishingSettings override > bundled /audio/fishing/*.mp3.
    /// </summary>
    public class FishingAudioResolver : IFishingAudioResolver
    {
        public const string DefaultCastSoundUrl = "/media/fishing_cast.mp3";
        public const string DefaultBiteSoundUrl = "/media/fishing_bite.mp3";
        public const string DefaultCatchCommonSoundUrl = "/media/fishing_catch_common.mp3";
        public const string DefaultCatchRareSoundUrl = "/media/fishing_catch_rare.mp3";
        public const string DefaultCatchLegendarySoundUrl = "/media/fishing_catch_legendary.mp3";
        public const string DefaultAccidentSoundUrl = "/media/fishing_accident.mp3";
        public const string DefaultFrenzySoundUrl = "/media/fishing_frenzy.mp3";

        private static readonly string[] SupportedAudioExtensions = [".mp3", ".wav", ".ogg", ".webm"];
        private static readonly string[] ImageSizeSuffixes = ["_thumbnail", "_small", "_medium", "_large"];

        private readonly string _fishesDirectory;
        private readonly ConcurrentDictionary<string, string?> _customAudioCache = new(StringComparer.OrdinalIgnoreCase);

        public FishingAudioResolver(IWebHostEnvironment env)
            : this(Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "fishes"))
        {
        }

        internal FishingAudioResolver(string fishesDirectory)
        {
            _fishesDirectory = fishesDirectory;
        }

        public ResolvedFishingAudioUrls ResolveForCatch(FishingSettings? settings, FishRarity rarity, string? fishImageFileName)
        {
            var soundEnabled = settings?.SoundEnabled ?? true;
            var soundVolume = Math.Clamp(settings?.SoundVolume ?? 0.8, 0.0, 1.0);

            var castUrl = NormalizeSoundPath(settings?.CastSoundPath, DefaultCastSoundUrl);
            var biteUrl = NormalizeSoundPath(settings?.BiteSoundPath, DefaultBiteSoundUrl);
            var accidentUrl = NormalizeSoundPath(settings?.AccidentSoundPath, DefaultAccidentSoundUrl);
            var frenzyUrl = ResolveFrenzySoundUrl(settings);

            var customFishAudio = FindCustomFishAudioUrl(fishImageFileName);
            string catchUrl;
            if (!string.IsNullOrWhiteSpace(customFishAudio))
            {
                catchUrl = customFishAudio;
            }
            else
            {
                catchUrl = rarity switch
                {
                    FishRarity.Legendary or FishRarity.Mythical =>
                        NormalizeSoundPath(settings?.CatchLegendarySoundPath, DefaultCatchLegendarySoundUrl),
                    FishRarity.Rare or FishRarity.Epic =>
                        NormalizeSoundPath(settings?.CatchRareSoundPath, DefaultCatchRareSoundUrl),
                    _ =>
                        NormalizeSoundPath(settings?.CatchCommonSoundPath, DefaultCatchCommonSoundUrl)
                };
            }

            return new ResolvedFishingAudioUrls(
                soundEnabled,
                soundVolume,
                castUrl,
                biteUrl,
                catchUrl,
                accidentUrl,
                frenzyUrl);
        }

        public string ResolveFrenzySoundUrl(FishingSettings? settings) =>
            NormalizeSoundPath(settings?.FrenzySoundPath, DefaultFrenzySoundUrl);

        public string? FindCustomFishAudioUrl(string? fishImageFileName)
        {
            if (string.IsNullOrWhiteSpace(fishImageFileName))
            {
                return null;
            }

            var baseName = ExtractBaseFileName(fishImageFileName);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return null;
            }

            return _customAudioCache.GetOrAdd(baseName, LookupOnDisk);
        }

        public void InvalidateCache() => _customAudioCache.Clear();

        private string? LookupOnDisk(string baseName)
        {
            if (!Directory.Exists(_fishesDirectory))
            {
                return null;
            }

            foreach (var ext in SupportedAudioExtensions)
            {
                var candidate = Path.Combine(_fishesDirectory, baseName + ext);
                if (File.Exists(candidate))
                {
                    return "/fishes/" + baseName + ext;
                }
            }

            return null;
        }

        internal static string ExtractBaseFileName(string imageFileName)
        {
            var fileName = Path.GetFileName(imageFileName.Trim());
            var dotIdx = fileName.LastIndexOf('.');
            var noExt = dotIdx > 0 ? fileName[..dotIdx] : fileName;

            foreach (var suffix in ImageSizeSuffixes)
            {
                if (noExt.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return noExt[..^suffix.Length];
                }
            }

            return noExt;
        }

        private static string NormalizeSoundPath(string? customPath, string fallback)
        {
            if (string.IsNullOrWhiteSpace(customPath))
            {
                return fallback;
            }

            var trimmed = customPath.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith('/'))
            {
                return trimmed;
            }

            return "/" + trimmed;
        }
    }
}
