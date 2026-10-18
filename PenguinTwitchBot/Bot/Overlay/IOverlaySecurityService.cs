namespace PenguinTwitchBot.Bot.Overlay
{
    public interface IOverlaySecurityService
    {
        Task<string> GetOverlayKeyAsync();
        Task<string> RegenerateOverlayKeyAsync();
        Task<bool> ValidateKeyAsync(string? providedKey);
    }
}

