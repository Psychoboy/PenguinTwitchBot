using System.Text.Json;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

internal sealed class InvalidShipRulesException(string message, Exception innerException) : Exception(message, innerException);

public sealed class StarshipExpeditionStateStore(
    IServiceScopeFactory scopeFactory,
    ILogger<StarshipExpeditionStateStore>? logger = null) : IStarshipExpeditionStateStore
{
    public const string SettingKey = "starship_expedition.state.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public async Task<StarshipExpeditionSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingKey)).FirstOrDefault();
            if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting)) return null;

            StarshipExpeditionSnapshot? snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<StarshipExpeditionSnapshot>(setting.StringSetting, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Persisted Starship expedition state is invalid JSON.", ex);
            }

            if (snapshot == null)
                throw new InvalidDataException("Persisted Starship expedition state is empty.");

            var routeNamesNeedMigration = snapshot.RouteNameMigrationVersion < StarshipEventCatalog.CurrentRouteNameMigrationVersion;
            var migratedRoute = routeNamesNeedMigration;
            if (routeNamesNeedMigration)
            {
                if (string.Equals(snapshot.CurrentRoute, StarshipEventCatalog.UnchartedRoute, StringComparison.OrdinalIgnoreCase))
                    snapshot.CurrentRoute = StarshipEventCatalog.StartRoute;
                if (string.Equals(snapshot.CurrentRoute, StarshipEventCatalog.LegacyDirectRoute, StringComparison.OrdinalIgnoreCase))
                    snapshot.CurrentRoute = StarshipEventCatalog.CoreRoute;
                if (string.Equals(snapshot.CurrentRoute, StarshipEventCatalog.LegacySurveyRoute, StringComparison.OrdinalIgnoreCase))
                    snapshot.CurrentRoute = StarshipEventCatalog.FrontierRoute;

                if (snapshot.CurrentEvent != null && snapshot.CurrentEvent.EventType == "RouteChoice")
                {
                    for (var i = 0; i < snapshot.CurrentEvent.Choices.Count; i++)
                    {
                        var choice = snapshot.CurrentEvent.Choices[i];
                        if (string.Equals(choice.ChoiceId, "direct", StringComparison.OrdinalIgnoreCase))
                            snapshot.CurrentEvent.Choices[i] = new()
                            {
                                ChoiceId = "core",
                                Label = choice.Label == "Take the direct route" ? "Take the Core Route" : choice.Label,
                                Story = choice.Story,
                                Hint = choice.Hint,
                                TargetRoute = StarshipEventCatalog.CoreRoute
                            };
                        else if (string.Equals(choice.ChoiceId, "detour", StringComparison.OrdinalIgnoreCase))
                            snapshot.CurrentEvent.Choices[i] = new()
                            {
                                ChoiceId = "frontier",
                                Label = choice.Label == "Take the survey detour" ? "Take the Frontier Route" : choice.Label,
                                Story = choice.Story,
                                Hint = choice.Hint,
                                TargetRoute = StarshipEventCatalog.FrontierRoute
                            };
                    }
                    if (string.Equals(snapshot.CurrentEvent.DefaultChoiceId, "direct", StringComparison.OrdinalIgnoreCase))
                        snapshot.CurrentEvent.DefaultChoiceId = "core";
                    else if (string.Equals(snapshot.CurrentEvent.DefaultChoiceId, "detour", StringComparison.OrdinalIgnoreCase))
                        snapshot.CurrentEvent.DefaultChoiceId = "frontier";

                    foreach (var vote in snapshot.CurrentEvent.Votes)
                    {
                        if (string.Equals(vote.ChoiceId, "direct", StringComparison.OrdinalIgnoreCase))
                            vote.ChoiceId = "core";
                        else if (string.Equals(vote.ChoiceId, "detour", StringComparison.OrdinalIgnoreCase))
                            vote.ChoiceId = "frontier";
                    }
                }

                if (snapshot.EventCatalog != null)
                    StarshipExpeditionSettings.MigrateCatalog(snapshot.EventCatalog);

                snapshot.RouteNameMigrationVersion = StarshipEventCatalog.CurrentRouteNameMigrationVersion;
            }

            try
            {
                Validate(snapshot);
            }
            catch (InvalidShipRulesException ex)
            {
                var hasOutstandingRewards = snapshot.Crew?.Any(member =>
                    member?.UnbankedBalances?.Any(balance => balance?.Amount > 0) == true) == true ||
                    snapshot.BankedPayouts?.Any(payout => payout is { IsPaid: false }) == true;
                if (hasOutstandingRewards)
                {
                    if (migratedRoute) await PersistMigratedSnapshotAsync();
                    logger?.LogWarning(ex, "Retaining persisted Starship expedition state with invalid ship rules because it contains outstanding rewards.");
                    return snapshot;
                }

                logger?.LogWarning(ex, "Discarding persisted Starship expedition state with invalid ship rules; defaults will be recreated on the next voyage.");
                unitOfWork.Settings.Remove(setting);
                await unitOfWork.SaveChangesAsync();
                return null;
            }
            if (migratedRoute) await PersistMigratedSnapshotAsync();
            return snapshot;

            async Task PersistMigratedSnapshotAsync()
            {
                setting.StringSetting = JsonSerializer.Serialize(snapshot, JsonOptions);
                unitOfWork.Settings.Update(setting);
                await unitOfWork.SaveChangesAsync();
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task SaveAsync(StarshipExpeditionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.RouteNameMigrationVersion = StarshipEventCatalog.CurrentRouteNameMigrationVersion;
        Validate(snapshot);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingKey)).FirstOrDefault();
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);

            if (setting == null)
            {
                await unitOfWork.Settings.AddAsync(new Setting
                {
                    Name = SettingKey,
                    DataType = Setting.DataTypeEnum.String,
                    StringSetting = json
                });
            }
            else
            {
                setting.DataType = Setting.DataTypeEnum.String;
                setting.StringSetting = json;
                unitOfWork.Settings.Update(setting);
            }

            await unitOfWork.SaveChangesAsync();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private static void Validate(StarshipExpeditionSnapshot snapshot)
    {
        if (snapshot.ExpeditionId == Guid.Empty)
            throw new InvalidDataException("Starship expedition state must have an expedition ID.");
        if (snapshot.LegCount < 1 || snapshot.CurrentLeg < 0 || snapshot.CurrentLeg > snapshot.LegCount)
            throw new InvalidDataException("Starship expedition leg progress is invalid.");
        if (snapshot.MinimumEventIntervalSeconds < 1 || snapshot.MaximumEventIntervalSeconds < snapshot.MinimumEventIntervalSeconds)
            throw new InvalidDataException("Starship expedition event interval range is invalid.");
        if (snapshot.StationIntervalLegs < 1 || snapshot.EventVoteDurationSeconds < 1 || snapshot.BoardingDurationSeconds < 1)
            throw new InvalidDataException("Starship expedition station or event timing settings are invalid.");
        if (!Enum.IsDefined(snapshot.Status))
            throw new InvalidDataException("Starship expedition status is invalid.");
        if (snapshot.ShipSystems is { } shipSystems)
        {
            try
            {
                StarshipShipSystems.Validate(shipSystems);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidShipRulesException("Persisted Starship ship rules are invalid.", ex);
            }
        }
        if (snapshot.Status == StarshipExpeditionStatus.Boarding && snapshot.BoardingClosesAtUtc == null)
            throw new InvalidDataException("Starship crew boarding must have a closing time.");
        if (snapshot.Crew == null)
            throw new InvalidDataException("Starship expedition crew cannot be null.");
        if (snapshot.Crew.Any(member => string.IsNullOrWhiteSpace(member.UserId)))
            throw new InvalidDataException("Every Starship crew member must have a user ID.");
        if (snapshot.Crew.Select(member => member.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Crew.Count)
            throw new InvalidDataException("Starship expedition crew contains duplicate user IDs.");
        if (snapshot.Crew.Any(member => member.UnbankedBalances == null || member.UnbankedBalances.Any(balance => balance.PointTypeId <= 0 || balance.Amount < 0)))
            throw new InvalidDataException("Starship crew unbanked balances are invalid.");
        if (snapshot.RewardProfiles == null || snapshot.BankedPayouts == null)
            throw new InvalidDataException("Starship reward or payout state cannot be null.");
        if (snapshot.BankedPayouts.Any(payout => string.IsNullOrWhiteSpace(payout.OperationId) || string.IsNullOrWhiteSpace(payout.UserId) || payout.PointTypeId <= 0 || payout.Amount <= 0))
            throw new InvalidDataException("Starship banked payout state is invalid.");
        if (snapshot.BankedPayouts.Select(payout => payout.OperationId).Distinct(StringComparer.Ordinal).Count() != snapshot.BankedPayouts.Count)
            throw new InvalidDataException("Starship banked payouts contain duplicate operation IDs.");

        if (snapshot.CurrentEvent is { } currentEvent &&
            (currentEvent.EventId == Guid.Empty || string.IsNullOrWhiteSpace(currentEvent.EventType) || currentEvent.Sequence < 1))
        {
            throw new InvalidDataException("Starship expedition event state is invalid.");
        }
        foreach (var expeditionEvent in snapshot.CompletedEvents.Concat(snapshot.CurrentEvent == null ? [] : new[] { snapshot.CurrentEvent }))
        {
            try
            {
                if (expeditionEvent.StepDefinitions.Count > 0)
                {
                    StarshipEventGraphValidator.Validate(new StarshipEventDefinition(
                        expeditionEvent.EventType, expeditionEvent.Title, expeditionEvent.EventDescription, [], string.Empty)
                    {
                        StartStepId = expeditionEvent.StartStepId,
                        DefaultStepSuppliesCost = expeditionEvent.DefaultStepSuppliesCost,
                        Steps = expeditionEvent.StepDefinitions
                    });
                    if (expeditionEvent == snapshot.CurrentEvent &&
                        !expeditionEvent.StepDefinitions.Any(step => step.StepId == expeditionEvent.CurrentStepId))
                        throw new InvalidDataException("The active Starship event step does not exist in its graph.");
                }
                expeditionEvent.ShipContext?.Validate();
                foreach (var choice in expeditionEvent.Choices.Concat(expeditionEvent.ChoicePool)
                             .Concat(expeditionEvent.StepDefinitions.SelectMany(step => step.Choices)))
                    choice.ShipEffect?.Validate();
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException("Persisted ship event effects are invalid.", ex);
            }
        }
        if (snapshot.CurrentEvent is { } openEvent &&
            (openEvent.PendingRiskConfirmations == null || openEvent.PendingRiskConfirmations.Select(item => item.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != openEvent.PendingRiskConfirmations.Count))
            throw new InvalidDataException("Starship risk confirmations are invalid.");
        if (snapshot.CurrentEvent is { } rewardEvent &&
            (rewardEvent.EligibleRewardUserIds.Any(string.IsNullOrWhiteSpace) ||
             rewardEvent.EligibleRewardUserIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != rewardEvent.EligibleRewardUserIds.Count ||
             rewardEvent.RewardOutcomes.Any(outcome => string.IsNullOrWhiteSpace(outcome.UserId) || outcome.PointTypeId <= 0 || outcome.EarnedAmount < 0 || outcome.LostAmount < 0)))
            throw new InvalidDataException("Starship event reward state is invalid.");
        if (snapshot.CurrentEvent is { } riskEvent && riskEvent.PendingRiskConfirmations.Any(item =>
                string.IsNullOrWhiteSpace(item.UserId) || string.IsNullOrWhiteSpace(item.ChoiceId) || item.PointTypeId <= 0 ||
                item.ExposedBalance < 0 || item.MaximumLoss < 0 || item.MaximumLoss > item.ExposedBalance))
            throw new InvalidDataException("Starship risk confirmation values are invalid.");
    }
}