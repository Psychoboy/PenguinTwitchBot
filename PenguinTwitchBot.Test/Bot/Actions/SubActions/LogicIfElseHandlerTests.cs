using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using NSubstitute;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class LogicIfElseHandlerTests
    {
        [Fact]
        public async Task TrueCondition_ExecutesTrueSubActions()
        {
            var logger = Substitute.For<ILogger<LogicIfElseHandler>>();
            var handler = new LogicIfElseHandler(logger, Substitute.For<IServiceScopeFactory>());

            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<ILogger<SubActionHandlerFactory>>());
            services.AddSingleton(new SubActionHandlerFactory(
                new List<ISubActionHandler>(),
                Substitute.For<ILogger<SubActionHandlerFactory>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IFeatureRuntimeCoordinator>()));
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var asyncScope = scopeFactory.CreateAsyncScope();

            handler = new LogicIfElseHandler(logger, scopeFactory);

            var type = new LogicIfElseType
            {
                LeftValue = "5",
                RightValue = "10",
                Operator = ComparisonOperator.GreaterThan,
                TrueSubActions = new List<SubActionType> { new SetVariableType { Text = "result", Value = "true", Enabled = true, Index = 0 } },
                FalseSubActions = new List<SubActionType>()
            };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var logger = Substitute.For<ILogger<LogicIfElseHandler>>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var handler = new LogicIfElseHandler(logger, scopeFactory);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task FalseCondition_ExecutesFalseSubActions()
        {
            var logger = Substitute.For<ILogger<LogicIfElseHandler>>();
            var handler = new LogicIfElseHandler(logger, Substitute.For<IServiceScopeFactory>());

            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<ILogger<SubActionHandlerFactory>>());
            services.AddSingleton(new SubActionHandlerFactory(
                new List<ISubActionHandler>(),
                Substitute.For<ILogger<SubActionHandlerFactory>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IFeatureRuntimeCoordinator>()));
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var asyncScope = scopeFactory.CreateAsyncScope();

            handler = new LogicIfElseHandler(logger, scopeFactory);

            var type = new LogicIfElseType
            {
                LeftValue = "5",
                RightValue = "10",
                Operator = ComparisonOperator.GreaterThan,
                TrueSubActions = new List<SubActionType>(),
                FalseSubActions = new List<SubActionType> { new SetVariableType { Text = "result", Value = "false", Enabled = true, Index = 0 } }
            };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);
        }
    }
}
