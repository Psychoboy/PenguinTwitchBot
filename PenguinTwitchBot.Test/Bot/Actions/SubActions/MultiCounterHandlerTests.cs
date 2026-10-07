using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Services;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class MultiCounterHandlerTests
    {
        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var counterService = Substitute.For<ICounterService>();
            var handler = new MultiCounterHandler(counterService);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task ValidType_AdjustsCounterAndPopulatesVariables()
        {
            var counterService = Substitute.For<ICounterService>();
            var handler = new MultiCounterHandler(counterService);

            var result = new CounterResult
            {
                Success = true,
                CounterName = "test",
                DisplayName = "Test Counter",
                OldValue = 4,
                NewValue = 5,
                Operation = CounterOperation.Increment
            };

            counterService.AdjustCounterAsync(
                "test",
                CounterOperation.Increment,
                null,
                0,
                100,
                Arg.Any<PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs?>()).Returns(Task.FromResult(result));

            counterService.When(x => x.PopulateVariables(
                Arg.Any<ConcurrentDictionary<string, string>>(),
                Arg.Any<CounterResult>(),
                Arg.Any<string?>())).Do(callInfo =>
                {
                    var dict = callInfo.Arg<ConcurrentDictionary<string, string>>();
                    dict["counter_test"] = "5";
                    dict["counter_value"] = "5";
                });

            var type = new MultiCounterType
            {
                Name = "test",
                Operation = CounterOperation.Increment,
                Min = 0,
                Max = 100
            };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("5", variables["counter_test"]);
            Assert.Equal("5", variables["counter_value"]);
        }

        [Fact]
        public async Task CommandArgs_EvaluatesCommandArgs()
        {
            var counterService = Substitute.For<ICounterService>();
            var handler = new MultiCounterHandler(counterService);

            var result = new CounterResult
            {
                Success = true,
                CounterName = "death",
                DisplayName = "Deaths",
                OldValue = 10,
                NewValue = 11,
                Operation = CounterOperation.Increment
            };

            counterService.EvaluateCommandArgsAsync(
                "death",
                Arg.Any<PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs?>(),
                Arg.Any<int?>(),
                Arg.Any<int?>()).Returns(Task.FromResult(result));

            counterService.When(x => x.PopulateVariables(
                Arg.Any<ConcurrentDictionary<string, string>>(),
                Arg.Any<CounterResult>(),
                Arg.Any<string?>())).Do(callInfo =>
                {
                    var dict = callInfo.Arg<ConcurrentDictionary<string, string>>();
                    dict["custom_dest"] = "11";
                });

            var type = new MultiCounterType
            {
                Name = "death",
                Operation = CounterOperation.CommandArgs,
                DestinationVariable = "custom_dest"
            };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("11", variables["custom_dest"]);
        }

        [Fact]
        public async Task ExecuteAsync_WithoutOriginalEventArgs_PassesNullEventArgs()
        {
            var counterService = Substitute.For<ICounterService>();
            var handler = new MultiCounterHandler(counterService);

            counterService.AdjustCounterAsync(
                "test",
                CounterOperation.Increment,
                null,
                0,
                100,
                null).Returns(Task.FromResult(new CounterResult { Success = true, CounterName = "test" }));

            var type = new MultiCounterType
            {
                Name = "test",
                Operation = CounterOperation.Increment
            };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            await counterService.Received(1).AdjustCounterAsync(
                "test",
                CounterOperation.Increment,
                null,
                0,
                100,
                null);
        }

        [Fact]
        public async Task ExecuteAsync_WithOriginalEventArgs_PassesDeserializedEventArgs()
        {
            var counterService = Substitute.For<ICounterService>();
            var handler = new MultiCounterHandler(counterService);

            counterService.AdjustCounterAsync(
                "test",
                CounterOperation.Increment,
                null,
                0,
                100,
                Arg.Is<PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs>(e => e.Name == "testuser")).Returns(Task.FromResult(new CounterResult { Success = true, CounterName = "test" }));

            var type = new MultiCounterType
            {
                Name = "test",
                Operation = CounterOperation.Increment
            };
            var variables = new ConcurrentDictionary<string, string>
            {
                ["OriginalEventArgs"] = System.Text.Json.JsonSerializer.Serialize(new PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs
                {
                    Name = "testuser",
                    DisplayName = "TestUser"
                })
            };

            await handler.ExecuteAsync(type, variables);

            await counterService.Received(1).AdjustCounterAsync(
                "test",
                CounterOperation.Increment,
                null,
                0,
                100,
                Arg.Is<PenguinTwitchBot.Bot.Events.Chat.CommandEventArgs>(e => e.Name == "testuser"));
        }
    }
}
