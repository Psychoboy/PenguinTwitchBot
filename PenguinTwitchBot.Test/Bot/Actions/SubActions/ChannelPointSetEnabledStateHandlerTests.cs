using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class ChannelPointSetEnabledStateHandlerTests
    {
        [Fact]
        public async Task ValidType_ProcessesChannelPoint()
        {
            var logger = Substitute.For<ILogger<ChannelPointSetEnabledStateHandler>>();
            var twitchService = Substitute.For<ITwitchService>();

            var handler = new ChannelPointSetEnabledStateHandler(logger, twitchService);

            var type = new ChannelPointSetEnabledStateType { Text = "Test Reward", EnablePoint = true };
            var variables = new ConcurrentDictionary<string, string>();

            var rewards = new List<ChannelPointReward>
            {
                new ChannelPointReward("123", "Test Reward", false, true, 1, null, true, null, false, false, 1, false, 0, false, 1)
            };

            twitchService.GetChannelPointRewards(Arg.Any<bool>()).Returns(rewards);

            await handler.ExecuteAsync(type, variables);

            await twitchService.Received(1).UpdateChannelPointReward("123", Arg.Is<UpdateCustomRewardRequest>(r => r.IsEnabled == true));
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var logger = Substitute.For<ILogger<ChannelPointSetEnabledStateHandler>>();
            var twitchService = Substitute.For<ITwitchService>();
            var handler = new ChannelPointSetEnabledStateHandler(logger, twitchService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task AlreadyEnabled_DoesNotUpdate()
        {
            var logger = Substitute.For<ILogger<ChannelPointSetEnabledStateHandler>>();
            var twitchService = Substitute.For<ITwitchService>();

            var handler = new ChannelPointSetEnabledStateHandler(logger, twitchService);

            var type = new ChannelPointSetEnabledStateType { Text = "Test Reward", EnablePoint = true };
            var variables = new ConcurrentDictionary<string, string>();

            var rewards = new List<ChannelPointReward>
            {
                new ChannelPointReward("123", "Test Reward", true, true, 1, null, true, null, false, false, 1, false, 0, false, 1)
            };

            twitchService.GetChannelPointRewards(Arg.Any<bool>()).Returns(rewards);

            await handler.ExecuteAsync(type, variables);

            await twitchService.DidNotReceive().UpdateChannelPointReward(Arg.Any<string>(), Arg.Any<UpdateCustomRewardRequest>());
        }
    }
}
