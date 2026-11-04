using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Bot.Commands.PastyGames;

public class MaxBetSettingsTests
{
    [Fact]
    public async Task GetLegacyDefaultAsync_PreservesTheLegacyDefaultWhenUnconfigured()
    {
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetLongSetting(MaxBetSettings.LegacyGameName, MaxBetSettings.LegacySettingName, MaxBetSettings.DefaultMaxBet)
            .Returns(MaxBetSettings.DefaultMaxBet);

        var result = await MaxBetSettings.GetLegacyDefaultAsync(settings);

        Assert.Equal(200_000_069, result);
    }

    [Fact]
    public async Task GetLegacyDefaultAsync_ClampsStoredValuesToTheSafeAbsoluteMaximum()
    {
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetLongSetting(MaxBetSettings.LegacyGameName, MaxBetSettings.LegacySettingName, MaxBetSettings.DefaultMaxBet)
            .Returns(long.MaxValue);

        var result = await MaxBetSettings.GetLegacyDefaultAsync(settings);

        Assert.Equal(MaxBetSettings.MaximumAllowedBet, result);
    }

    [Fact]
    public async Task SetForPointTypeAsync_RejectsUnsafeValues()
    {
        var settings = Substitute.For<IGameSettingsService>();
        var pointTypeId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => MaxBetSettings.SetForPointTypeAsync(settings, pointTypeId, (long)MaxBetSettings.MaximumAllowedBet + 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => MaxBetSettings.SetForPointTypeAsync(settings, pointTypeId, 0));
        await settings.DidNotReceive().SetLongSetting(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>());
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
    public async Task GetForGameAsync_AllowsGameOverrideAbovePointTypeDefault()
    {
        var settings = CreateSettings(defaultMaxBet: 50_000);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("Gamble").Returns(new PointType { Id = Guid.NewGuid(), Name = "Tickets" });
        settings.GetLongSetting("Gamble", MaxBetSettings.GameOverrideSettingName, 0).Returns(75_000);

        var result = await MaxBetSettings.GetForGameAsync(points, settings, "Gamble");

        Assert.Equal(75_000, result);
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

    [Theory]
    [InlineData("0%")]
    [InlineData("-5%")]
    [InlineData("101%")]
    [InlineData("500%")]
    [InlineData("abc%")]
    public async Task CalculatorRejectsOutOfRangePercentagesBeforeBalanceLookup(string betAmount)
    {
        var settings = CreateSettings(5000L);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("game").Returns(new PointType { Id = Guid.NewGuid(), Name = "Tickets" });
        var calculator = new MaxBetCalculator(points, settings);

        var result = await calculator.CheckAndRemovePoints("viewer", "game", betAmount, 1);

        Assert.Equal(MaxBet.ParseResult.InvalidValue, result.Result);
        await points.DidNotReceive().GetMaxPointsByUserIdAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>());
        await points.DidNotReceive().RemovePointsFromUserByUserIdAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>());
    }

    [Fact]
    public async Task CalculatorAppliesValidPercentageToCappedBalance()
    {
        var settings = CreateSettings(5000L);
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("game").Returns(new PointType { Id = Guid.NewGuid(), Name = "Tickets" });
        points.GetMaxPointsByUserIdAndGame("viewer", "game", 5000L).Returns(5000L);
        points.RemovePointsFromUserByUserIdAndGame("viewer", "game", 2500L).Returns(true);
        var calculator = new MaxBetCalculator(points, settings);

        var result = await calculator.CheckAndRemovePoints("viewer", "game", "50%", 1);

        Assert.Equal(MaxBet.ParseResult.Success, result.Result);
        Assert.Equal(2500L, result.Amount);
    }

    [Theory]
    [InlineData(10_000L, 5_000L, 5_000L, 10_000L)]
    [InlineData(10_000L, 2_500L, 5_000L, 5_000L)]
    [InlineData(10_000L, 1L, 5_000L, 2L)]
    [InlineData(10_000L, 0L, 5_000L, 0L)]
    [InlineData(long.MaxValue, int.MaxValue, int.MaxValue, long.MaxValue)]
    public void GambleJackpotShareIsProportionalToMaxBet(long jackpot, long amount, long maxBet, long expected)
    {
        Assert.Equal(expected, Gamble.CalculateJackpotShare(jackpot, amount, maxBet));
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
