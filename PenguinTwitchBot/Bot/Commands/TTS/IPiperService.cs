using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Bot.Commands.TTS;

public class PiperVoiceInfo
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public BaseVoice.SexType Sex { get; set; } = BaseVoice.SexType.None;
    public string Quality { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
}

public interface IPiperService
{
    Task<bool> EnsurePiperExecutableAsync();
    Task<IReadOnlyList<PiperVoiceInfo>> GetVoicesAsync();
    bool IsModelDownloaded(string modelKey);
    bool IsModelCompatible(string modelKey);
    Task<bool> DownloadVoiceAsync(string modelKey);
    Task<byte[]> SynthesizeAsync(string message, string modelKey);
}

