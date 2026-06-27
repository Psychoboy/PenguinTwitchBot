using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Timers;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class TimerGroupSetEnabledStateHandlerTests
    {
        [Fact]
        public async Task ValidType_SetsTimerGroupEnabled()
        {
            var timerService = Substitute.For<IAutoTimers>();
            timerService.GetTimerGroupAsync(Arg.Any<int>()).Returns(new TimerGroup { Id = 1, Name = "Ad Timers", Active = false });
            timerService.UpdateNextRun(Arg.Any<TimerGroup>()).Returns(new TimerGroup { Id = 1, Name = "Ad Timers", Active = true });
            var handler = new TimerGroupSetEnabledStateHandler(timerService);

            var type = new TimerGroupSetEnabledStateType { TimerGroupId = 1, IsEnabled = true };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await timerService.Received(1).GetTimerGroupAsync(1);
            await timerService.Received(1).UpdateNextRun(Arg.Any<TimerGroup>());
            await timerService.Received(1).UpdateTimerGroup(Arg.Is<TimerGroup>(t => t.Active == true));
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var timerService = Substitute.For<IAutoTimers>();
            var handler = new TimerGroupSetEnabledStateHandler(timerService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task MissingTimerGroupId_ThrowsException()
        {
            var timerService = Substitute.For<IAutoTimers>();
            var handler = new TimerGroupSetEnabledStateHandler(timerService);

            var type = new TimerGroupSetEnabledStateType { TimerGroupId = null };
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(type, variables));
        }
    }
}
