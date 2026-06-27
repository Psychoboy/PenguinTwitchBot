using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions;

public class GiveawayPrizeHandlerTests
{
    [Fact]
    public async Task ValidPrize_SetsPrizeVariable()
    {
        var giveawayFeature = Substitute.For<IGiveawayFeature>();
        giveawayFeature.GetPrize().Returns(Task.FromResult("Test Prize"));
        var handler = new GiveawayPrizeHandler(giveawayFeature);

        var prizeType = new GiveawayPrizeType();
        var variables = new ConcurrentDictionary<string, string>();

        await handler.ExecuteAsync(prizeType, variables);

        Assert.Equal("Test Prize", variables["Prize"]);
    }

    [Fact]
    public async Task EmptyPrize_SetsEmptyString()
    {
        var giveawayFeature = Substitute.For<IGiveawayFeature>();
        giveawayFeature.GetPrize().Returns(Task.FromResult(string.Empty));
        var handler = new GiveawayPrizeHandler(giveawayFeature);

        var prizeType = new GiveawayPrizeType();
        var variables = new ConcurrentDictionary<string, string>();

        await handler.ExecuteAsync(prizeType, variables);

        Assert.Equal(string.Empty, variables["Prize"]);
    }

    [Fact]
    public async Task WrongType_ThrowsException()
    {
        var giveawayFeature = Substitute.For<IGiveawayFeature>();
        var handler = new GiveawayPrizeHandler(giveawayFeature);

        var wrongType = new SendMessageType();
        var variables = new ConcurrentDictionary<string, string>();

        await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
    }
}
