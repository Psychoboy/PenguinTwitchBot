using NSubstitute;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Bot.Commands.Misc;

public class TopPointTypeSelectionTests
{
    [Fact]
    public async Task ResolveAsync_UsesLegacyTopMappingWhenNoNameOrSavedSelectionExists()
    {
        var fallback = new PointType { Id = Guid.NewGuid(), Name = "Points" };
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("Top").Returns(fallback);
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetStringSetting("Top", TopPointTypeSelection.DefaultPointTypeSettingName, fallback.Id.ToString())
            .Returns(fallback.Id.ToString());

        var result = await TopPointTypeSelection.ResolveAsync(points, settings, null);

        Assert.Same(fallback, result);
        await points.Received(1).RegisterDefaultPointForGame("Top");
    }

    [Fact]
    public async Task ResolveAsync_UsesSavedDefaultWhenItExists()
    {
        var mappedType = new PointType { Id = Guid.NewGuid(), Name = "Points" };
        var selectedType = new PointType { Id = Guid.NewGuid(), Name = "Tickets" };
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypeForGame("Top").Returns(mappedType);
        points.GetPointTypeById(selectedType.Id).Returns(selectedType);
        var settings = Substitute.For<IGameSettingsService>();
        settings.GetStringSetting("Top", TopPointTypeSelection.DefaultPointTypeSettingName, mappedType.Id.ToString())
            .Returns(selectedType.Id.ToString());

        var result = await TopPointTypeSelection.ResolveAsync(points, settings, null);

        Assert.Same(selectedType, result);
    }

    [Fact]
    public async Task ResolveAsync_ResolvesRequestedPointTypeByFullNameCaseInsensitively()
    {
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypes().Returns([
            new PointType { Id = Guid.NewGuid(), Name = "Points" },
            new PointType { Id = Guid.NewGuid(), Name = "Bonus Tickets" }
        ]);

        var result = await TopPointTypeSelection.ResolveAsync(points, Substitute.For<IGameSettingsService>(), "  bonus tickets ");

        Assert.Equal("Bonus Tickets", result?.Name);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNullForUnknownRequestedType()
    {
        var points = Substitute.For<IPointsSystem>();
        points.GetPointTypes().Returns([new PointType { Id = Guid.NewGuid(), Name = "Points" }]);

        var result = await TopPointTypeSelection.ResolveAsync(points, Substitute.For<IGameSettingsService>(), "missing");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("tickets", "tickets")]
    [InlineData("Bonus", "Bonus")]
    public void GetRequestedPointTypeName_UsesArgumentTokens(string argument, string expected)
    {
        Assert.Equal(expected, TopPointTypeSelection.GetRequestedPointTypeName([argument]));
    }

    [Fact]
    public void GetRequestedPointTypeName_JoinsMultiWordNamesAndReturnsNullForNoArguments()
    {
        Assert.Equal("Bonus Tickets", TopPointTypeSelection.GetRequestedPointTypeName(["Bonus", "Tickets"]));
        Assert.Null(TopPointTypeSelection.GetRequestedPointTypeName([]));
    }

    [Fact]
    public async Task SaveDefaultAsync_PersistsSelectedPointTypeId()
    {
        var pointTypeId = Guid.NewGuid();
        var settings = Substitute.For<IGameSettingsService>();

        await TopPointTypeSelection.SaveDefaultAsync(settings, pointTypeId);

        await settings.Received(1).SetStringSetting(
            "Top",
            TopPointTypeSelection.DefaultPointTypeSettingName,
            pointTypeId.ToString());
    }
}