using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Bot.Commands.PastyGames;

public class MaxBetSettingsTests
{
    [Fact]
    public async Task GetAsync_PreservesTheLegacyDefaultWhenUnconfigured()
    {
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetLongSetting(MaxBetSettings.LegacyGameName, MaxBetSettings.LegacySettingName, MaxBetSettings.DefaultMaxBet)
            .Returns(MaxBetSettings.DefaultMaxBet);

        var result = await MaxBetSettings.GetAsync(settings);

        Assert.Equal(200_000_069, result);
    }

    [Fact]
    public async Task GetAsync_ClampsStoredValuesToTheSafeAbsoluteMaximum()
    {
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetLongSetting(MaxBetSettings.LegacyGameName, MaxBetSettings.LegacySettingName, MaxBetSettings.DefaultMaxBet)
            .Returns(long.MaxValue);

        var result = await MaxBetSettings.GetAsync(settings);

        Assert.Equal(MaxBetSettings.MaximumAllowedBet, result);
    }

    [Fact]
    public async Task SetAsync_RejectsUnsafeValuesAndPersistsValidLimits()
    {
        var settings = Substitute.For<IGameSettingsService>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => MaxBetSettings.SetAsync(settings, (long)MaxBetSettings.MaximumAllowedBet + 1));
        await MaxBetSettings.SetAsync(settings, 500_000_000);

        await settings.Received(1).SetLongSetting(MaxBetSettings.LegacyGameName, MaxBetSettings.LegacySettingName, 500_000_000);
    }

    [Fact]
    public async Task SetForPointTypeAsync_PersistsByPointTypeId()
    {
        var settings = Substitute.For<IGameSettingsService>();
        var pointTypeId = Guid.NewGuid();

        await MaxBetSettings.SetForPointTypeAsync(settings, pointTypeId, 500_000_000);

        await settings.Received(1).SetLongSetting(
            MaxBetSettings.PointTypeSettingsGameName,
            $"PointType{pointTypeId:N}",
            500_000_000);
    }

    [Fact]
    public void ClampPayoutMultiplier_EnforcesFiniteOverflowSafeRange()
    {
        Assert.Equal(MaxBetSettings.MaximumSafePayoutMultiplier, MaxBetSettings.ClampPayoutMultiplier(double.MaxValue));
        Assert.Equal(0d, MaxBetSettings.ClampPayoutMultiplier(double.NaN));
        Assert.Equal(1d, MaxBetSettings.ClampPayoutMultiplier(double.NaN, 1d));
    }

    [Fact]
    public async Task GetForGameAsync_PrefersGameOverrideOverPointTypeDefault()
    {
        var settings = CreateSettings(defaultMaxBet: 50_000);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("Gamble").Returns(new PointType { Id = Guid.NewGuid(), Name = "Tickets" });
        settings.GetLongSetting("Gamble", MaxBetSettings.GameOverrideSettingName, 0).Returns(10_000);

        var result = await MaxBetSettings.GetForGameAsync(points, settings, "Gamble");

        Assert.Equal(10_000, result);
    }

    [Fact]
    public async Task GetForGameAsync_UsesMappedPointTypeDefaultWithoutOverride()
    {
        var pointTypeId = Guid.NewGuid();
        var settings = CreateSettings(defaultMaxBet: 50_000);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("Gamble").Returns(new PointType { Id = pointTypeId, Name = "Tickets" });
        settings.GetLongSetting(
            MaxBetSettings.PointTypeSettingsGameName,
            $"PointType{pointTypeId:N}",
            50_000).Returns(75_000);

        var result = await MaxBetSettings.GetForGameAsync(points, settings, "Gamble");

        Assert.Equal(75_000, result);
    }

    [Fact]
    public async Task GetPointTypeDefaultForGameAsync_IgnoresGameOverride()
    {
        var pointTypeId = Guid.NewGuid();
        var settings = CreateSettings(defaultMaxBet: 50_000);
        settings.GetLongSetting("Gamble", MaxBetSettings.GameOverrideSettingName, 0).Returns(10_000);
        settings.GetLongSetting(
            MaxBetSettings.PointTypeSettingsGameName,
            $"PointType{pointTypeId:N}",
            50_000).Returns(75_000);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("Gamble").Returns(new PointType { Id = pointTypeId, Name = "Tickets" });

        var result = await MaxBetSettings.GetPointTypeDefaultForGameAsync(points, settings, "Gamble");

        Assert.Equal(75_000, result);
    }

    [Fact]
    public async Task CalculatorUsesConfiguredLimitForMaxBets()
    {
        var configuredMax = 5000L;
        var settings = Substitute.For<IGameSettingsService>();
        settings = CreateSettings(configuredMax);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("game").Returns(new PointType { Id = Guid.NewGuid(), Name = "Tickets" });
        points.GetMaxPointsByUserIdAndGame("viewer", "game", configuredMax).Returns(configuredMax);
        points.RemovePointsFromUserByUserIdAndGame("viewer", "game", configuredMax).Returns(true);
        var calculator = new MaxBetCalculator(points, settings);

        var result = await calculator.CheckAndRemovePoints("viewer", "game", "max", 1);

        Assert.Equal(MaxBet.ParseResult.Success, result.Result);
        Assert.Equal(configuredMax, result.MaximumAllowedBet);
        Assert.Equal(configuredMax, result.Amount);
    }

    [Fact]
    public async Task CalculatorRejectsAmountsAboveConfiguredLimit()
    {
        var settings = Substitute.For<IGameSettingsService>();
        settings = CreateSettings(5000L);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("game").Returns(new PointType { Id = Guid.NewGuid(), Name = "Tickets" });
        var calculator = new MaxBetCalculator(points, settings);

        var result = await calculator.CheckAndRemovePoints("viewer", "game", "5001", 1);

        Assert.Equal(MaxBet.ParseResult.ToMuch, result.Result);
        Assert.Equal(5000, result.MaximumAllowedBet);
        await points.DidNotReceive().RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>());
    }

    private static IGameSettingsService CreateSettings(long defaultMaxBet)
    {
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetLongSetting(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns(call => call.ArgAt<long>(2));
        settings.GetLongSetting(MaxBetSettings.LegacyGameName, MaxBetSettings.LegacySettingName, MaxBetSettings.DefaultMaxBet)
            .Returns(defaultMaxBet);
        return settings;
    }
}
