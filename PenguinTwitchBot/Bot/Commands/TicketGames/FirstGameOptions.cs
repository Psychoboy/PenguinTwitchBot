using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;

namespace PenguinTwitchBot.Bot.Commands.TicketGames;

public sealed record FirstGameOptions(
    int MaximumClaims,
    int StartingAward,
    double AwardDecreasePerClaim,
    string OfflineMessage,
    string TooSlowMessage,
    string AwardMessage)
{
    public const string GameName = "First";
    public const string MaximumClaimsSetting = "MaximumClaims";
    public const string StartingAwardSetting = "StartingAward";
    public const string AwardDecreasePerClaimSetting = "AwardDecreasePerClaim";
    public const string OfflineMessageSetting = "OfflineMessage";
    public const string TooSlowMessageSetting = "TooSlowMessage";
    public const string AwardMessageSetting = "AwardMessage";

    public static async Task<FirstGameOptions> LoadAsync(IGameSettingsService settingsService)
    {
        var maximumClaims = Math.Clamp(
            await settingsService.GetIntSetting(GameName, MaximumClaimsSetting, 200),
            1,
            int.MaxValue);
        var startingAward = Math.Clamp(
            await settingsService.GetIntSetting(GameName, StartingAwardSetting, 100),
            1,
            MaxBetSettings.MaximumAllowedBet);
        var configuredDecrease = await settingsService.GetDoubleSetting(GameName, AwardDecreasePerClaimSetting, 0.5);
        var awardDecrease = double.IsFinite(configuredDecrease)
            ? Math.Clamp(configuredDecrease, 0d, MaxBetSettings.MaximumSafePayoutMultiplier)
            : 0.5d;

        return new FirstGameOptions(
            maximumClaims,
            startingAward,
            awardDecrease,
            await settingsService.GetStringSetting(GameName, OfflineMessageSetting, "Nice try, the stream is currently offline."),
            await settingsService.GetStringSetting(GameName, TooSlowMessageSetting, "Sorry, You were to slow today. FeelsBadMan"),
            await settingsService.GetStringSetting(
                GameName,
                AwardMessageSetting,
                "Whooohooo! You came in position {Position} and get {Amount} {PointType}!! PogChamp"));
    }

    public long GetAwardForPriorClaims(int priorClaims)
    {
        var award = (decimal)StartingAward - (decimal)Math.Max(0, priorClaims) * (decimal)AwardDecreasePerClaim;
        return (long)Math.Clamp(decimal.Floor(award), 0m, MaxBetSettings.MaximumAllowedBet);
    }
}