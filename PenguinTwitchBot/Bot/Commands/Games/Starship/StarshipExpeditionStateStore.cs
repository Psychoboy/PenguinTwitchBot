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
        if (snapshot.SchemaVersion != StarshipExpeditionSnapshot.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported Starship expedition state version {snapshot.SchemaVersion}.");
        if (snapshot.ExpeditionId == Guid.Empty)
            throw new InvalidDataException("Starship expedition state must have an expedition ID.");
        if (snapshot.LegCount < 1 || snapshot.CurrentLeg < 0 || snapshot.CurrentLeg > snapshot.LegCount)
            throw new InvalidDataException("Starship expedition leg progress is invalid.");
        if (snapshot.MinimumEventIntervalSeconds < 1 || snapshot.MaximumEventIntervalSeconds < snapshot.MinimumEventIntervalSeconds)
            throw new InvalidDataException("Starship expedition event interval range is invalid.");
        if (snapshot.StationIntervalLegs < 1 || snapshot.EventVoteDurationSeconds < 1)
            throw new InvalidDataException("Starship expedition station or event timing settings are invalid.");
        if (!Enum.IsDefined(snapshot.Status))
            throw new InvalidDataException("Starship expedition status is invalid.");
        if (snapshot.Crew == null)
            throw new InvalidDataException("Starship expedition crew cannot be null.");
        if (snapshot.Crew.Any(member => string.IsNullOrWhiteSpace(member.UserId)))
            throw new InvalidDataException("Every Starship crew member must have a user ID.");
        if (snapshot.Crew.Select(member => member.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Crew.Count)
            throw new InvalidDataException("Starship expedition crew contains duplicate user IDs.");

        if (snapshot.CurrentEvent is { } currentEvent &&
            (currentEvent.EventId == Guid.Empty || string.IsNullOrWhiteSpace(currentEvent.EventType) || currentEvent.Sequence < 1))
        {
            throw new InvalidDataException("Starship expedition event state is invalid.");
        }
    }
}