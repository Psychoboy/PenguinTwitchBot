using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public interface IStarshipExpeditionService
{
    Task<StarshipExpeditionSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<StarshipExpeditionEventSnapshot> BuildPreview(StarshipExpeditionSettings settings, int seed);
    Task<StarshipExpeditionSnapshot> StartExpeditionAsync(
        string? startedBy,
        int? legCount = null,
        int? minimumEventIntervalMinutes = null,
        int? maximumEventIntervalMinutes = null,
        int? seed = null,
        CancellationToken cancellationToken = default);
    Task<StarshipExpeditionSnapshot?> StopExpeditionAsync(string? stoppedBy, CancellationToken cancellationToken = default);
    Task<StarshipExpeditionSnapshot> AdvanceEventAsync(CancellationToken cancellationToken = default);
    Task<StarshipExpeditionSnapshot> JoinCrewAsync(CommandEventArgs eventArgs, CancellationToken cancellationToken = default);
    Task<StarshipExpeditionSnapshot> VoteAsync(CommandEventArgs eventArgs, string choiceId, CancellationToken cancellationToken = default);
    Task<StarshipExpeditionSnapshot> ConfirmRiskActionAsync(CommandEventArgs eventArgs, CancellationToken cancellationToken = default);
}