using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Bot.Commands.Misc;

public static class TopPointTypeSelection
{
    public const string GameName = "Top";
    public const string DefaultPointTypeSettingName = "DefaultPointTypeId";

    public static async Task<PointType> GetDefaultAsync(IPointsSystem pointsSystem, IGameSettingsService gameSettingsService)
    {
        await pointsSystem.RegisterDefaultPointForGame(GameName);
        var fallback = await pointsSystem.GetPointTypeForGame(GameName);
        var configuredId = await gameSettingsService.GetStringSetting(
            GameName,
            DefaultPointTypeSettingName,
            fallback.Id.ToString());

        return Guid.TryParse(configuredId, out var pointTypeId)
            ? await pointsSystem.GetPointTypeById(pointTypeId) ?? fallback
            : fallback;
    }

    public static async Task SaveDefaultAsync(IGameSettingsService gameSettingsService, Guid pointTypeId)
    {
        if (pointTypeId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(pointTypeId));
        }

        await gameSettingsService.SetStringSetting(GameName, DefaultPointTypeSettingName, pointTypeId.ToString());
    }

    public static async Task<PointType?> ResolveAsync(
        IPointsSystem pointsSystem,
        IGameSettingsService gameSettingsService,
        string? requestedPointTypeName)
    {
        if (string.IsNullOrWhiteSpace(requestedPointTypeName))
        {
            return await GetDefaultAsync(pointsSystem, gameSettingsService);
        }

        var pointTypes = await pointsSystem.GetPointTypes();
        return pointTypes.FirstOrDefault(pointType =>
            string.Equals(pointType.Name, requestedPointTypeName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string? GetRequestedPointTypeName(IEnumerable<string> arguments)
    {
        var name = string.Join(' ', arguments).Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }
}