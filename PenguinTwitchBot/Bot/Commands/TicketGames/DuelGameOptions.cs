using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Core.Points;

namespace PenguinTwitchBot.Bot.Commands.TicketGames;

public sealed record DuelGameOptions(int MinimumWager, int MaximumWager, int WinChancePercent, int ChallengeTimeoutMinutes)
{
    public const string GameName = "DuelGame";
    public const string MinimumWagerSetting = "MinimumWager";
    public const string MaximumWagerSetting = "MaximumWager";
    public const string WinChanceSetting = "WinChancePercent";
    public const string ChallengeTimeoutSetting = "ChallengeTimeoutMinutes";

    public static async Task<DuelGameOptions> LoadAsync(IPointsSystem pointsSystem, IGameSettingsService settingsService)
    {
        var maxBet = (int)await MaxBetSettings.GetPointTypeDefaultForGameAsync(pointsSystem, settingsService, GameName);
        var minimum = Math.Clamp(await settingsService.GetIntSetting(GameName, MinimumWagerSetting, 1), 1, maxBet);
        var maximum = Math.Clamp(await settingsService.GetIntSetting(GameName, MaximumWagerSetting, 100), minimum, maxBet);
        var winChance = Math.Clamp(await settingsService.GetIntSetting(GameName, WinChanceSetting, 50), 0, 100);
        var timeout = Math.Clamp(await settingsService.GetIntSetting(GameName, ChallengeTimeoutSetting, 2), 1, 120);
        return new DuelGameOptions(minimum, maximum, winChance, timeout);
    }
}
