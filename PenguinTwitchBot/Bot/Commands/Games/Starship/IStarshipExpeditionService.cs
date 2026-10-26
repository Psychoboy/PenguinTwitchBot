using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public interface IStarshipExpeditionService
{
    Task<StarshipExpeditionSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<StarshipExpeditionEventSnapshot> BuildPreview(StarshipExpeditionSettings settings, int seed);
    IReadOnlyList<StarshipExpeditionEventSnapshot> BuildPreview(
        StarshipExpeditionSettings settings,
        int seed,
        StarshipPreviewVoterPolicy voterPolicy);
    Task<StarshipExpeditionSnapshot> StartExpeditionAsync(
        string? startedBy,
        int? legCount = null,
        int? minimumEventIntervalMinutes = null,
        int? maximumEventIntervalMinutes = null,
        int? seed = null,
        CancellationToken cancellationToken = default,
        string? startedByUserId = null);
    Task<StarshipExpeditionSnapshot?> StopExpeditionAsync(string? stoppedBy, CancellationToken cancellationToken = default);
    Task<StarshipExpeditionSnapshot> AdvanceEventAsync(CancellationToken cancellationToken = default);
    Task<StarshipExpeditionSnapshot> JoinCrewAsync(CommandEventArgs eventArgs, CancellationToken cancellationToken = default);
    Task<StarshipExpeditionSnapshot> VoteAsync(CommandEventArgs eventArgs, string choiceId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retained as a backwards-compatible skeleton for the Twitch extension (TwitchExtensionController.cs)
    /// until the extension update without confirm actions is reviewed and deployed.
    /// Can be removed once the Twitch extension no longer sends risk confirmation requests.
    /// </summary>
    Task<StarshipExpeditionSnapshot> ConfirmRiskActionAsync(CommandEventArgs eventArgs, CancellationToken cancellationToken = default);
    /// <summary>Keeps the status widget visible for a period; returns null when there is no active voyage.</summary>
    Task<StarshipExpeditionSnapshot?> ShowStatusWidgetAsync(int durationSeconds, CancellationToken cancellationToken = default);
    /// <summary>Keeps the event widget visible for a period; returns null when there is no expedition snapshot.</summary>
    Task<StarshipExpeditionSnapshot?> ShowEventWidgetAsync(int durationSeconds, CancellationToken cancellationToken = default);
    /// <summary>The saved overlay look, sent to the widgets with every state update.</summary>
    Task<StarshipAppearanceSettings> GetAppearanceAsync(CancellationToken cancellationToken = default);
    /// <summary>Reloads the saved look and pushes it to connected overlays immediately.</summary>
    Task RefreshAppearanceAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns persisted past expeditions ordered by start time descending.</summary>
    Task<List<PenguinTwitchBot.Database.Bot.Models.Games.StarshipExpeditionHistoryRow>> GetExpeditionHistoryAsync(int limit = 50, CancellationToken cancellationToken = default);
    /// <summary>Loads the complete snapshot of a past expedition by ID.</summary>
    Task<StarshipExpeditionSnapshot?> GetExpeditionHistorySnapshotAsync(Guid expeditionId, CancellationToken cancellationToken = default);
}