using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed class StarshipExpeditionStateStore(IServiceScopeFactory scopeFactory) : IStarshipExpeditionStateStore
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

            Validate(snapshot);
            return snapshot;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task SaveAsync(StarshipExpeditionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
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
                throw new InvalidDataException("Persisted Starship ship rules are invalid.", ex);
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
                expeditionEvent.ShipContext?.Validate();
                foreach (var choice in expeditionEvent.Choices.Concat(expeditionEvent.ChoicePool))
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