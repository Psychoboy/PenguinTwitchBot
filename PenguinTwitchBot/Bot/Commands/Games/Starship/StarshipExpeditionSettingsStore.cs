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
            StarshipExpeditionSettings? settings;
            try
            {
                settings = setting == null || string.IsNullOrWhiteSpace(setting.StringSetting)
                    ? new StarshipExpeditionSettings()
                    : JsonSerializer.Deserialize<StarshipExpeditionSettings>(setting.StringSetting, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Persisted Starship Expedition settings are invalid JSON.", ex);
            }

            if (settings == null)
                throw new InvalidDataException("Persisted Starship Expedition settings are empty.");

            if (settings.CatalogStorageVersion == 1)
            {
                var events = await unitOfWork.StarshipEvents.GetAllAsync();
                var profiles = await unitOfWork.StarshipRewardProfiles.GetAllAsync();
                settings.EventCatalog = events.Select(row => ReadDocument<StarshipEventDefinition>(row.DocumentJson, row.Id))
                    .OrderBy(definition => definition.SortOrder).ThenBy(definition => definition.EventType, StringComparer.Ordinal).ToList();
                settings.RewardProfiles = profiles.Select(row => ReadDocument<StarshipRewardProfile>(row.DocumentJson, row.Id))
                    .OrderBy(profile => profile.Id, StringComparer.Ordinal).ToList();
                if (settings.EventCatalog.Count == 0 || settings.RewardProfiles.Count == 0)
                    throw new InvalidDataException("Persisted Starship catalog rows are missing; refusing to reset the catalog.");
            }
            else if (settings.CatalogStorageVersion != 0)
                throw new InvalidDataException($"Unsupported Starship catalog storage version {settings.CatalogStorageVersion}.");

            StarshipExpeditionSettings.NormalizeAndValidate(settings);
            var installedStarters = StarshipStarterEvents.Install(settings, environment.WebRootPath);
            if (settings.CatalogStorageVersion == 0 || installedStarters)
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

    private static T ReadDocument<T>(string json, string id)
    {
        try
        {
            var document = JsonSerializer.Deserialize<CatalogDocument<T>>(json, JsonOptions);
            if (document == null || document.SchemaVersion != 1 || document.Definition == null)
                throw new InvalidDataException($"Unsupported or empty Starship catalog document {id}.");
            return document.Definition;
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
            row.DocumentJson = JsonSerializer.Serialize(new CatalogDocument<StarshipEventDefinition>(1, definition), JsonOptions);
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
            row.DocumentJson = JsonSerializer.Serialize(new CatalogDocument<StarshipRewardProfile>(1, profile), JsonOptions);
        }
        unitOfWork.StarshipRewardProfiles.RemoveRange(profileRows.Values);

        var node = JsonSerializer.SerializeToNode(settings, JsonOptions)!.AsObject();
        node.Remove("rewardProfiles");
        node.Remove("eventCatalog");
        node["catalogStorageVersion"] = 1;
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
        settings.CatalogStorageVersion = 1;
    }

    private sealed record CatalogDocument<T>(int SchemaVersion, T Definition);
}
