using PenguinTwitchBot.Database.Bot.Models.Credits;

namespace PenguinTwitchBot.Bot.Overlay.Credits
{
    public interface ICreditsService
    {
        const string CompletedTriggerNamePrefix = "Credits.Completed.";
        const string AnyProfileTriggerKey = "*";

        Task EnsureSeededAsync(CancellationToken cancellationToken = default);

        Task<List<CreditProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
        Task<CreditProfile?> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<CreditProfile> ResolveProfileAsync(string? profileName = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetProfileNamesAsync(CancellationToken cancellationToken = default);
        Task<CreditProfile> CreateProfileAsync(string name, Guid? themeId = null, CreditsProfileConfig? config = null, bool isDefault = false, CancellationToken cancellationToken = default);
        Task<CreditProfile> SaveProfileAsync(Guid id, string name, Guid? themeId, CreditsProfileConfig config, bool isDefault, CancellationToken cancellationToken = default);
        Task<CreditProfile> CloneProfileAsync(Guid sourceId, string newName, CancellationToken cancellationToken = default);
        Task SetDefaultProfileAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default);

        Task<List<CreditTheme>> GetThemesAsync(CancellationToken cancellationToken = default);
        Task<CreditTheme?> GetThemeByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<CreditTheme> CreateThemeAsync(string name, string? description, CreditsThemeConfig config, CancellationToken cancellationToken = default);
        Task<CreditTheme> SaveThemeAsync(Guid id, string name, string? description, CreditsThemeConfig config, CancellationToken cancellationToken = default);
        Task<CreditTheme> CloneThemeAsync(Guid sourceId, string newName, CancellationToken cancellationToken = default);
        Task<CreditTheme?> ResetBuiltInThemeAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> DeleteThemeAsync(Guid id, CancellationToken cancellationToken = default);

        Task<string?> ExportProfileJsonAsync(Guid id, CancellationToken cancellationToken = default);
        Task<CreditProfile> ImportProfileAsync(string json, CancellationToken cancellationToken = default);
        Task<string?> ExportThemeJsonAsync(Guid id, CancellationToken cancellationToken = default);
        Task<CreditTheme> ImportThemeAsync(string json, CancellationToken cancellationToken = default);

        Task<CreditsRenderPayload> RenderAsync(
            string? profileName = null,
            string? sampleMode = null,
            string? windowOverrideMode = null,
            int? lookbackMinutes = null,
            CancellationToken cancellationToken = default);

        Task PlayAsync(
            string? profileName = null,
            string? windowOverrideMode = null,
            int? lookbackMinutes = null,
            string? sampleMode = null,
            CancellationToken cancellationToken = default);

        Task StopAsync(string? profileName = null);
        Task PauseAsync(string? profileName = null);
        Task ResumeAsync(string? profileName = null);
        Task TogglePauseAsync(string? profileName = null);
        Task RestartAsync(string? profileName = null);
        Task SetSpeedAsync(string? profileName, double multiplier);
        Task NotifyReloadAsync(string? profileName = null);

        Task<List<CreditStreamSession>> GetSessionsAsync(int limit = 50, CancellationToken cancellationToken = default);
        Task<DateTime> MarkManualCheckpointAsync(string? label = null, string? profileName = null, CancellationToken cancellationToken = default);

        Task<List<CreditEvent>> GetEventsAsync(
            DateTime startUtc,
            DateTime endUtc,
            string? eventType = null,
            bool includeHidden = true,
            CancellationToken cancellationToken = default);

        Task<CreditEvent> AddManualEventAsync(
            string username,
            string? displayName,
            decimal amount,
            string? detail,
            string? avatarUrl = null,
            string? eventType = null,
            CancellationToken cancellationToken = default);

        Task<bool> SetEventHiddenAsync(Guid eventId, bool isHidden, CancellationToken cancellationToken = default);
        Task<bool> DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default);

        Task<int> GetRetentionDaysAsync(CancellationToken cancellationToken = default);
        Task SetRetentionDaysAsync(int days, CancellationToken cancellationToken = default);

        Task TriggerCompletedAsync(string profileName, int totalEntries, int totalSections, CancellationToken cancellationToken = default);
    }
}
