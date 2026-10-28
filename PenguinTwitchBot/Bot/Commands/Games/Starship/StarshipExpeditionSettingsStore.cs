using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Models.Games;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed class StarshipExpeditionSettingsStore(IServiceScopeFactory scopeFactory, IWebHostEnvironment environment) : IStarshipExpeditionSettingsStore
{
    public const string SettingKey = "starship_expedition.settings.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new PenguinTwitchBot.Database.Bot.DatabaseTools.LegacyIntCompatibleGuidJsonConverter(), new PenguinTwitchBot.Database.Bot.DatabaseTools.LegacyIntCompatibleNullableGuidJsonConverter() },
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public async Task<StarshipExpeditionSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingKey)).FirstOrDefault();
            var hasPersistedSettings = setting != null && !string.IsNullOrWhiteSpace(setting.StringSetting);
            StarshipExpeditionSettings? settings;
            try
            {
                settings = !hasPersistedSettings
                    ? new StarshipExpeditionSettings()
                    : JsonSerializer.Deserialize<StarshipExpeditionSettings>(setting!.StringSetting!, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Persisted Starship Expedition settings are invalid JSON.", ex);
            }

            if (settings == null)
                throw new InvalidDataException("Persisted Starship Expedition settings are empty.");

            var repairedCatalogRows = false;
            if (hasPersistedSettings)
            {
                var events = await unitOfWork.StarshipEvents.GetAllAsync();
                var profiles = await unitOfWork.StarshipRewardProfiles.GetAllAsync();
                settings.EventCatalog = events.Select(row =>
                    {
                        if (string.IsNullOrWhiteSpace(row.Id))
                            throw new InvalidDataException("A persisted Starship event row has no ID.");
                        var definition = ReadDocument<StarshipEventDefinition>(row.DocumentJson, row.Id);
                        if (string.Equals(definition.EventType, row.Id, StringComparison.Ordinal)) return definition;
                        repairedCatalogRows = true;
                        return definition with { EventType = row.Id };
                    })
                    .OrderBy(definition => definition.SortOrder).ThenBy(definition => definition.EventType, StringComparer.Ordinal).ToList();
                settings.RewardProfiles = profiles.Select(row =>
                    {
                        if (string.IsNullOrWhiteSpace(row.Id))
                            throw new InvalidDataException("A persisted Starship reward-profile row has no ID.");
                        var profile = ReadDocument<StarshipRewardProfile>(row.DocumentJson, row.Id);
                        if (!string.Equals(profile.Id, row.Id, StringComparison.Ordinal))
                        {
                            repairedCatalogRows = true;
                            profile.Id = row.Id;
                        }
                        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > 80)
                        {
                            var rowName = row.Name?.Trim();
                            if (string.IsNullOrWhiteSpace(rowName) || rowName.Length > 80)
                                throw new InvalidDataException($"Persisted Starship reward profile {row.Id} has no valid name.");
                            repairedCatalogRows = true;
                            profile.Name = rowName;
                        }
                        return profile;
                    })
                    .OrderBy(profile => profile.Id, StringComparer.Ordinal).ToList();
                if (settings.EventCatalog.Count == 0 || settings.RewardProfiles.Count == 0)
                    throw new InvalidDataException("Persisted Starship catalog rows are missing; refusing to reset the catalog.");
                if (StarshipExpeditionSettings.MigrateCatalog(settings.EventCatalog))
                    repairedCatalogRows = true;
            }

            var resetShipSystems = false;
            if (settings.ShipSystems == null)
            {
                settings.ShipSystems = new();
                resetShipSystems = true;
            }
            else
            {
                try
                {
                    settings.ShipSystems.Validate();
                }
                catch (ArgumentException)
                {
                    settings.ShipSystems = new();
                    resetShipSystems = true;
                }
            }

            StarshipExpeditionSettings.NormalizeAndValidate(settings);
            var installedStarters = StarshipStarterEvents.Install(settings, environment.WebRootPath);
            if (!hasPersistedSettings || installedStarters || resetShipSystems || repairedCatalogRows)
                await SaveLockedAsync(unitOfWork, settings, setting);
            return settings;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task SaveAsync(StarshipExpeditionSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = StarshipExpeditionSettings.NormalizeAndValidate(settings);
        StarshipStarterEvents.Install(normalized, environment.WebRootPath);
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingKey)).FirstOrDefault();
            await SaveLockedAsync(unitOfWork, normalized, setting);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<StarshipExpeditionSettings> ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new StarshipExpeditionSettings());
        StarshipStarterEvents.Install(settings, environment.WebRootPath);
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var strategy = unitOfWork.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await unitOfWork.BeginTransactionAsync();
                var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingKey)).FirstOrDefault();
                unitOfWork.StarshipEvents.RemoveRange(await unitOfWork.StarshipEvents.GetAllAsync());
                unitOfWork.StarshipRewardProfiles.RemoveRange(await unitOfWork.StarshipRewardProfiles.GetAllAsync());
                await unitOfWork.SaveChangesAsync();
                await SaveLockedAsync(unitOfWork, settings, setting);
                await transaction.CommitAsync(cancellationToken);
            });
            return settings;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private static T ReadDocument<T>(string json, string id)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidDataException($"Starship catalog document {id} is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Starship catalog document {id} contains invalid JSON.", ex);
        }
    }

    private static async Task SaveLockedAsync(IUnitOfWork unitOfWork, StarshipExpeditionSettings settings, Setting? setting)
    {
        var eventRows = (await unitOfWork.StarshipEvents.GetAllAsync()).ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in settings.EventCatalog)
        {
            if (!eventRows.Remove(definition.EventType, out var row))
            {
                row = new StarshipEventRow { Id = definition.EventType };
                unitOfWork.StarshipEvents.Add(row);
            }
            else
                unitOfWork.StarshipEvents.Update(row);
            row.Name = definition.Title;
            row.Enabled = definition.Enabled;
            row.Weight = definition.Weight;
            row.DocumentJson = JsonSerializer.Serialize(definition, JsonOptions);
        }
        unitOfWork.StarshipEvents.RemoveRange(eventRows.Values);

        var profileRows = (await unitOfWork.StarshipRewardProfiles.GetAllAsync()).ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var profile in settings.RewardProfiles)
        {
            if (!profileRows.Remove(profile.Id, out var row))
            {
                row = new StarshipRewardProfileRow { Id = profile.Id };
                unitOfWork.StarshipRewardProfiles.Add(row);
            }
            else
                unitOfWork.StarshipRewardProfiles.Update(row);
            row.Name = profile.Name;
            row.Enabled = profile.Enabled;
            row.Weight = profile.GetTiers().Where(tier => tier.Enabled).Sum(tier => tier.Weight);
            row.DocumentJson = JsonSerializer.Serialize(profile, JsonOptions);
        }
        unitOfWork.StarshipRewardProfiles.RemoveRange(profileRows.Values);

        var node = JsonSerializer.SerializeToNode(settings, JsonOptions)!.AsObject();
        node.Remove("rewardProfiles");
        node.Remove("eventCatalog");
        var json = node.ToJsonString(JsonOptions);
        if (setting == null)
            await unitOfWork.Settings.AddAsync(new Setting { Name = SettingKey, DataType = Setting.DataTypeEnum.String, StringSetting = json });
        else
        {
            setting.DataType = Setting.DataTypeEnum.String;
            setting.StringSetting = json;
            unitOfWork.Settings.Update(setting);
        }
        await unitOfWork.SaveChangesAsync();
    }
}
