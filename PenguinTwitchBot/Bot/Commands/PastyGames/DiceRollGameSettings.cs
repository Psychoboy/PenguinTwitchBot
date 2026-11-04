using PenguinTwitchBot.Bot.Commands.Games;

namespace PenguinTwitchBot.Bot.Commands.PastyGames;

public static class DiceRollGameSettings
{
    public const string GameName = "Roll";
    public static readonly IReadOnlyList<(string SettingName, string Label, int DefaultValue)> Payouts =
    [
        ("SnakeEyesPayout", "Snake Eyes (double 1s)", 40),
        ("HardFourPayout", "Hard Four (double 2s)", 160),
        ("HardSixPayout", "Hard Six (double 3s)", 360),
        ("HardEightPayout", "Hard Eight (double 4s)", 640),
        ("HardTenPayout", "Hard Ten (double 5s)", 1000),
        ("BoxcarsPayout", "Boxcars (double 6s)", 1440)
    ];

    public static async Task<IReadOnlyList<int>> LoadPayoutsAsync(IGameSettingsService settingsService)
    {
        var tasks = Payouts.Select(payout => settingsService.GetIntSetting(
            GameName,
            payout.SettingName,
            payout.DefaultValue));
        var values = await Task.WhenAll(tasks);
        return values.Select(value => Math.Clamp(value, 0, MaxBetSettings.MaximumAllowedBet)).ToArray();
    }
}
