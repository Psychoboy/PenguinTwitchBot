using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core.Points;

namespace PenguinTwitchBot.Bot.Commands.PastyGames;

public sealed record StealGameOptions(int MinimumTransfer, int MaximumTransfer, double SuccessChancePercent)
{
    public const string GameName = "Steal";
    public const string MinimumTransferSetting = "MinimumTransfer";
    public const string MaximumTransferSetting = "MaximumTransfer";
    public const string SuccessChanceSetting = "SuccessChancePercent";

    public static async Task<StealGameOptions> LoadAsync(IPointsSystem pointsSystem, IGameSettingsService settingsService)
    {
        var pointType = await pointsSystem.GetPointTypeForGame(GameName);
        return await LoadAsync(pointType.Id, settingsService);
    }

    public static async Task<StealGameOptions> LoadAsync(Guid pointTypeId, IGameSettingsService settingsService)
    {
        var configuredMax = await settingsService.GetIntSetting(GameName, MaximumTransferSetting, 10_000);
        var effectiveMax = configuredMax <= 0
            ? (int)await MaxBetSettings.GetForPointTypeAsync(settingsService, pointTypeId)
            : Math.Clamp(configuredMax, 1, MaxBetSettings.MaximumAllowedBet);
        var minimum = Math.Clamp(await settingsService.GetIntSetting(GameName, MinimumTransferSetting, 100), 1, effectiveMax);
        var maximum = Math.Clamp(effectiveMax, minimum, MaxBetSettings.MaximumAllowedBet);
        var successChance = Math.Clamp(await settingsService.GetDoubleSetting(GameName, SuccessChanceSetting, 100d / 3d), 0d, 100d);
        return new StealGameOptions(minimum, maximum, successChance);
    }
}
