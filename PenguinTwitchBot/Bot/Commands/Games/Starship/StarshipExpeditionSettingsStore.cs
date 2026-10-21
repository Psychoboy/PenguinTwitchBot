using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed class StarshipExpeditionSettingsStore(IServiceScopeFactory scopeFactory) : IStarshipExpeditionSettingsStore
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
            if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting))
                return new StarshipExpeditionSettings();

            StarshipExpeditionSettings? settings;
            try
            {
                settings = JsonSerializer.Deserialize<StarshipExpeditionSettings>(setting.StringSetting, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Persisted Starship Expedition settings are invalid JSON.", ex);
            }

            if (settings == null)
                throw new InvalidDataException("Persisted Starship Expedition settings are empty.");

            return StarshipExpeditionSettings.NormalizeAndValidate(settings);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task SaveAsync(StarshipExpeditionSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = StarshipExpeditionSettings.NormalizeAndValidate(settings);
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingKey)).FirstOrDefault();
            var json = JsonSerializer.Serialize(normalized, JsonOptions);

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
}
