using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Bot.Commands.PastyGames;

public class TaxPointTypePolicySettingsTests
{
    [Fact]
    public async Task LoadAsync_EnablesOnlyTheLegacyMappedPointTypeByDefault()
    {
        var legacyType = new PointType { Id = Guid.NewGuid(), Name = "Tickets" };
        var otherType = new PointType { Id = Guid.NewGuid(), Name = "Gold" };
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypes().Returns([legacyType, otherType]);
        points.GetPointTypeForGame("Tax").Returns(legacyType);
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetStringSetting(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(string.Empty);

        var result = await TaxPointTypePolicySettings.LoadAsync(points, settings);

        Assert.Equal(2, result.Count);
        Assert.True(result.Single(entry => entry.PointType.Id == legacyType.Id).Policy.Enabled);
        Assert.False(result.Single(entry => entry.PointType.Id == otherType.Id).Policy.Enabled);
        var legacyPolicy = result.Single(entry => entry.PointType.Id == legacyType.Id).Policy;
        Assert.Equal(1, legacyPolicy.InactiveDays);
        Assert.Equal(25_000, legacyPolicy.MinimumBalance);
        Assert.Equal(1m, legacyPolicy.Percentage);
        Assert.Equal(200_000_069, legacyPolicy.MaximumDeduction);
        await points.Received(1).RegisterDefaultPointForGame("Tax");
    }

    [Fact]
    public async Task LoadAsync_RespectsAnExplicitlyDisabledLegacyPolicy()
    {
        var pointType = new PointType { Id = Guid.NewGuid(), Name = "Tickets" };
        var storedPolicy = new TaxPointTypePolicy { PointTypeId = pointType.Id, Enabled = false };
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypes().Returns([pointType]);
        points.GetPointTypeForGame("Tax").Returns(pointType);
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetStringSetting(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(System.Text.Json.JsonSerializer.Serialize(storedPolicy));

        var result = Assert.Single(await TaxPointTypePolicySettings.LoadAsync(points, settings));

        Assert.False(result.Policy.Enabled);
    }

    [Theory]
    [InlineData(25_000, 0)]
    [InlineData(25_100, 251)]
    [InlineData(30_000_000_000, 200_000_069)]
    public void CalculateDeduction_AppliesThresholdRateAndCap(long balance, long expected)
    {
        var policy = new TaxPointTypePolicy
        {
            Enabled = true,
            MinimumBalance = 25_000,
            Percentage = 1m,
            MaximumDeduction = 200_000_069
        };

        Assert.Equal(expected, TaxPointTypePolicySettings.CalculateDeduction(balance, policy));
    }

    [Fact]
    public void CalculateDeduction_DisabledPolicyDoesNotCharge()
    {
        var policy = new TaxPointTypePolicy { Enabled = false };

        Assert.Equal(0, TaxPointTypePolicySettings.CalculateDeduction(100_000, policy));
    }

    [Fact]
    public void GetCandidateViewerCutoff_UsesShortestEnabledInactivityThreshold()
    {
        var now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        TaxPointTypePolicy[] enabledPolicies =
        [
            new() { Enabled = true, InactiveDays = 1 },
            new() { Enabled = true, InactiveDays = 7 }
        ];

        Assert.Equal(now.AddDays(-1), TaxPointTypePolicySettings.GetCandidateViewerCutoff(now, enabledPolicies));
    }

    [Fact]
    public async Task SaveAsync_PersistsPolicyByPointTypeId()
    {
        var pointTypeId = Guid.NewGuid();
        var policy = new TaxPointTypePolicy { PointTypeId = pointTypeId, Enabled = true };
        var settings = Substitute.For<IGameSettingsService>();

        await TaxPointTypePolicySettings.SaveAsync(settings, policy);

        await settings.Received(1).SetStringSetting(
            "TaxPointPolicies",
            $"PointType{pointTypeId:N}",
            Arg.Is<string>(value => System.Text.Json.JsonSerializer.Deserialize<TaxPointTypePolicy>(value)!.PointTypeId == pointTypeId));
    }
}