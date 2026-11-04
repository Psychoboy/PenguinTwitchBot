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
        var maxBet = (int)await MaxBetSettings.GetPointTypeDefaultForGameAsync(pointsSystem, settingsService, GameName);
        var minimum = Math.Clamp(await settingsService.GetIntSetting(GameName, MinimumTransferSetting, 100), 1, maxBet);
        var maximum = Math.Clamp(await settingsService.GetIntSetting(GameName, MaximumTransferSetting, 10_000), minimum, maxBet);
        var successChance = Math.Clamp(await settingsService.GetDoubleSetting(GameName, SuccessChanceSetting, 100d / 3d), 0d, 100d);
        return new StealGameOptions(minimum, maximum, successChance);
    }
}
