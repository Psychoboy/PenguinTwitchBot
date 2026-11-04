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
    public async Task PointTypeDefaultCapsDuelAndStealWithoutASeparateGameOverride()
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
        settings.GetLongSetting(DuelGameOptions.GameName, MaxBetSettings.GameOverrideSettingName, 0).Returns(10);
        settings.GetLongSetting(StealGameOptions.GameName, MaxBetSettings.GameOverrideSettingName, 0).Returns(10);

        var duel = await DuelGameOptions.LoadAsync(points, settings);
        var steal = await StealGameOptions.LoadAsync(points, settings);

        Assert.Equal(50, duel.MaximumWager);
        Assert.Equal(50, steal.MaximumTransfer);
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
