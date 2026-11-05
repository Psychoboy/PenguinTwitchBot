using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Pages.PointGameSettings;
using PenguinTwitchBot.Pages.PointGameSettings.Providers;

namespace PenguinTwitchBot.Test.Bot.Commands.PastyGames;

public class PointOnlyGameOptionsTests
{
    [Fact]
    public async Task DuelOptions_DefaultToExistingWagerChanceAndTimeout()
    {
        var settings = CreateDefaultSettings();

        var options = await DuelGameOptions.LoadAsync(CreateDefaultPoints(), settings);

        Assert.Equal(1, options.MinimumWager);
        Assert.Equal(100, options.MaximumWager);
        Assert.Equal(50, options.WinChancePercent);
        Assert.Equal(2, options.ChallengeTimeoutMinutes);
    }

    [Fact]
    public async Task DiceRollPayouts_DefaultToExistingPrizes()
    {
        var payouts = await DiceRollGameSettings.LoadPayoutsAsync(CreateDefaultSettings());

        Assert.Equal(new[] { 40, 160, 360, 640, 1000, 1440 }, payouts);
    }

    [Fact]
    public async Task DiceRollPayouts_AllowValuesAboveOneMillionAndIgnoreWagerOverrides()
    {
        var settings = CreateDefaultSettings();
        settings.GetIntSetting(DiceRollGameSettings.GameName, "BoxcarsPayout", 1440).Returns(2_000_000);
        settings.GetLongSetting(DiceRollGameSettings.GameName, MaxBetSettings.GameOverrideSettingName, 0).Returns(1000);

        var payouts = await DiceRollGameSettings.LoadPayoutsAsync(settings);

        Assert.Equal(2_000_000, payouts[^1]);
    }

    [Fact]
    public async Task DuelAndSteal_CanExceedPointTypeDefaultOrUseZeroForPointTypeDefault()
    {
        var points = CreateDefaultPoints();
        var settings = CreateDefaultSettings();
        var pointTypeId = Guid.NewGuid();
        points.GetPointTypeForGame(Arg.Any<string>()).Returns(new PenguinTwitchBot.Database.Bot.Models.Points.PointType
        {
            Id = pointTypeId,
            Name = "Points"
        });
        settings.GetLongSetting(MaxBetSettings.PointTypeSettingsGameName, $"PointType{pointTypeId:N}", Arg.Any<long>())
            .Returns(50);

        var duelWithDefault = await DuelGameOptions.LoadAsync(points, settings);
        var stealWithDefault = await StealGameOptions.LoadAsync(points, settings);

        Assert.Equal(100, duelWithDefault.MaximumWager);
        Assert.Equal(10_000, stealWithDefault.MaximumTransfer);

        settings.GetIntSetting(DuelGameOptions.GameName, DuelGameOptions.MaximumWagerSetting, 100).Returns(0);
        settings.GetIntSetting(StealGameOptions.GameName, StealGameOptions.MaximumTransferSetting, 10_000).Returns(0);

        var duelUsingPointTypeDefault = await DuelGameOptions.LoadAsync(points, settings);
        var stealUsingPointTypeDefault = await StealGameOptions.LoadAsync(points, settings);

        Assert.Equal(50, duelUsingPointTypeDefault.MaximumWager);
        Assert.Equal(50, stealUsingPointTypeDefault.MaximumTransfer);
    }

    [Fact]
    public async Task StealOptions_DefaultToExistingRangeAndSuccessChance()
    {
        var options = await StealGameOptions.LoadAsync(CreateDefaultPoints(), CreateDefaultSettings());

        Assert.Equal(100, options.MinimumTransfer);
        Assert.Equal(10_000, options.MaximumTransfer);
        Assert.Equal(100d / 3d, options.SuccessChancePercent);
    }

    [Fact]
    public async Task FirstOptions_DefaultToExistingClaimLimitAndRewardCurve()
    {
        var options = await FirstGameOptions.LoadAsync(CreateDefaultSettings());

        Assert.Equal(200, options.MaximumClaims);
        Assert.Equal(100, options.StartingAward);
        Assert.Equal(0.5d, options.AwardDecreasePerClaim);
        Assert.Equal(100, options.GetAwardForPriorClaims(0));
        Assert.Equal(99, options.GetAwardForPriorClaims(1));
        Assert.Equal(1, options.GetAwardForPriorClaims(198));
        Assert.Equal(0, options.GetAwardForPriorClaims(199));
    }

    [Fact]
    public async Task FirstOptions_UseConfiguredRewardCurveAndMessages()
    {
        var settings = CreateDefaultSettings();
        settings.GetIntSetting(FirstGameOptions.GameName, FirstGameOptions.MaximumClaimsSetting, 200).Returns(25);
        settings.GetIntSetting(FirstGameOptions.GameName, FirstGameOptions.StartingAwardSetting, 100).Returns(40);
        settings.GetDoubleSetting(FirstGameOptions.GameName, FirstGameOptions.AwardDecreasePerClaimSetting, 0.5).Returns(2d);
        settings.GetStringSetting(FirstGameOptions.GameName, FirstGameOptions.AwardMessageSetting, Arg.Any<string>()).Returns("Place {Position}: {Amount} {PointType}");

        var options = await FirstGameOptions.LoadAsync(settings);

        Assert.Equal(25, options.MaximumClaims);
        Assert.Equal(40, options.StartingAward);
        Assert.Equal(38, options.GetAwardForPriorClaims(1));
        Assert.Equal("Place {Position}: {Amount} {PointType}", options.AwardMessage);
    }

    [Fact]
    public void FirstSettingsProvider_ExposesAndValidatesClaimAndRewardSettings()
    {
        var provider = new FirstSettingsProvider();
        var fieldKeys = provider.GetSections().SelectMany(section => section.Fields).Select(field => field.Key).ToArray();
        var validValues = new Dictionary<string, object?>
        {
            [FirstGameOptions.MaximumClaimsSetting] = 200,
            [FirstGameOptions.StartingAwardSetting] = 100,
            [FirstGameOptions.AwardDecreasePerClaimSetting] = 0.5d,
            [FirstGameOptions.OfflineMessageSetting] = "offline",
            [FirstGameOptions.TooSlowMessageSetting] = "slow",
            [FirstGameOptions.AwardMessageSetting] = "{Position} {Amount} {PointType}"
        };

        Assert.Contains(FirstGameOptions.MaximumClaimsSetting, fieldKeys);
        Assert.Contains(FirstGameOptions.StartingAwardSetting, fieldKeys);
        Assert.Contains(FirstGameOptions.AwardDecreasePerClaimSetting, fieldKeys);
        Assert.Contains(FirstGameOptions.OfflineMessageSetting, fieldKeys);
        Assert.Contains(FirstGameOptions.TooSlowMessageSetting, fieldKeys);
        Assert.Contains(FirstGameOptions.AwardMessageSetting, fieldKeys);
        Assert.Null(provider.Validate(validValues));

        validValues[FirstGameOptions.MaximumClaimsSetting] = 0;
        Assert.NotNull(provider.Validate(validValues));
    }

    [Fact]
    public void SettingsProviders_RejectInvalidGameplayRanges()
    {
        var duelValues = new Dictionary<string, object?>
        {
            [DuelGameOptions.MinimumWagerSetting] = 20,
            [DuelGameOptions.MaximumWagerSetting] = 10,
            [DuelGameOptions.WinChanceSetting] = 50,
            [DuelGameOptions.ChallengeTimeoutSetting] = 2
        };
        var rollValues = DiceRollGameSettings.Payouts.ToDictionary(payout => payout.SettingName, payout => (object?)(-1));
        var stealValues = new Dictionary<string, object?>
        {
            [StealGameOptions.MinimumTransferSetting] = 100,
            [StealGameOptions.MaximumTransferSetting] = 10_000,
            [StealGameOptions.SuccessChanceSetting] = 101d
        };

        Assert.NotNull(new DuelGameSettingsProvider().Validate(duelValues));
        Assert.NotNull(new RollSettingsProvider().Validate(rollValues));
        Assert.NotNull(new StealSettingsProvider().Validate(stealValues));
        Assert.NotNull(new GambleSettingsProvider().Validate(new Dictionary<string, object?>
        {
            [Gamble.JACKPOT_CONTRIBUTION] = 0.1d,
            [MaxBetSettings.GameOverrideSettingName] = -1
        }));
        Assert.NotNull(new HeistSettingsProvider().Validate(new Dictionary<string, object?>
        {
            [Heist.WIN_MULTIPLIER] = 1.5d,
            [MaxBetSettings.GameOverrideSettingName] = -1
        }));
        Assert.NotNull(new SlotsSettingsProvider().Validate(new Dictionary<string, object?>
        {
            [Slots.GAMESETTING_EMOTES] = "a,b,c",
            [Slots.GAMESETTING_3_OF_A_KIND] = 3.5d,
            [Slots.GAMESETTING_2_OF_A_KIND] = 0.3d,
            [Slots.GAMESETTING_FIRST_2_MULTIPLIER] = 2.5d,
            [Slots.GAMESETTING_LAST_2_MULTIPLIER] = 1.5d,
            [MaxBetSettings.GameOverrideSettingName] = -1
        }));
        Assert.NotNull(new RouletteSettingsProvider().Validate(new Dictionary<string, object?>
        {
            [Roulette.MUST_BEAT] = 52,
            [Roulette.MAX_PER_BET] = -1,
            [Roulette.MAX_AMOUNT] = 1000
        }));
    }

    [Fact]
    public void PointOnlyGameSettings_DoNotExposeRedundantMaxBetOverrides()
    {
        var providers = new IGameSettingsFormProvider[]
        {
            new DuelGameSettingsProvider(),
            new RollSettingsProvider(),
            new StealSettingsProvider()
        };

        Assert.All(providers, provider =>
            Assert.DoesNotContain(provider.GetSections().SelectMany(section => section.Fields), field =>
                field.Key == MaxBetSettings.GameOverrideSettingName));
    }

    private static IGameSettingsService CreateDefaultSettings()
    {
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetIntSetting(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(call => call.ArgAt<int>(2));
        settings.GetDoubleSetting(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>())
            .Returns(call => call.ArgAt<double>(2));
        settings.GetLongSetting(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns(call => call.ArgAt<long>(2));
        return settings;
    }

    private static IPointsSystem CreateDefaultPoints()
    {
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame(Arg.Any<string>()).Returns(new PenguinTwitchBot.Database.Bot.Models.Points.PointType
        {
            Id = Guid.NewGuid(),
            Name = "Points"
        });
        return points;
    }
}
