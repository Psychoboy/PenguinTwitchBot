using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Actions;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions
{
    public class ActionTests
    {
        private readonly ILogger<DotNetTwitchBot.Bot.Actions.Action> logger;
        private readonly ILogger<SubActionHandlerFactory> factoryLogger;
        private readonly ILogger<AlertHandler> alertHandlerLogger;
        private readonly IPenguinDispatcher mediator;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IServiceBackbone serviceBackbone;
        private readonly ITwitchService twitchService;
        private readonly DotNetTwitchBot.Bot.Actions.Action action;
        private readonly Dictionary<string, string> variables;

        public ActionTests()
        {
            logger = Substitute.For<ILogger<DotNetTwitchBot.Bot.Actions.Action>>();
            factoryLogger = Substitute.For<ILogger<SubActionHandlerFactory>>();
            alertHandlerLogger = Substitute.For<ILogger<AlertHandler>>();
            mediator = Substitute.For<IPenguinDispatcher, INotificationPublisher>();
            twitchService = Substitute.For<ITwitchService>();
            serviceBackbone = Substitute.For<IServiceBackbone>();

            // Setup service provider and scope factory
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(mediator);
            serviceCollection.AddSingleton<INotificationPublisher>(_ => (INotificationPublisher)mediator);
            serviceCollection.AddSingleton(factoryLogger);
            serviceCollection.AddSingleton(twitchService);
            serviceCollection.AddSingleton(Substitute.For<ILogger<SendMessageHandler>>());
            serviceCollection.AddSingleton(alertHandlerLogger);
            serviceCollection.AddSingleton(Substitute.For<ILogger<PlaySoundHandler>>());
            serviceCollection.AddTransient<ISubActionHandler, SendMessageHandler>();
            serviceCollection.AddTransient<ISubActionHandler, AlertHandler>();
            serviceCollection.AddTransient<ISubActionHandler, PlaySoundHandler>();
            serviceCollection.AddTransient<SubActionHandlerFactory>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            action = new DotNetTwitchBot.Bot.Actions.Action(logger, scopeFactory, serviceBackbone);
            variables = new Dictionary<string, string>
            {
                { "user", "testUser" },
                { "message", "testMessage" }
            };
        }

        [Fact]
        public async Task RunActionAsCommand_ShouldUpdateTextWithUser() 
        {
            var subAction1 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "%user% did something"
            };

            var actionType = new ActionType
            {
                Name = "TestAction",
                Enabled = true,
                SubActions = [subAction1]
            };

            await action.RunAction(variables, actionType);
        }

        [Fact]
        public async Task RunActionAsCommand_DisabledAction_ShouldNotRunSubActions()
        {
            var actionType = new ActionType
            {
                Name = "TestAction",
                Enabled = false,
                SubActions =
                [
                    new SendMessageType
                    {
                        SubActionTypes = SubActionTypes.SendMessage,
                        Text = "Test message"
                    }
                ]
            };

            await action.RunAction(variables, actionType);

            logger.Received(1).Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("was disabled so skipping")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_RandomAction_ShouldRunOneSubAction()
        {
            var subAction1 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 1"
            };
            var subAction2 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 2"
            };

            var actionType = new ActionType
            {
                Name = "RandomTestAction",
                Enabled = true,
                RandomAction = true,
                SubActions = [subAction1, subAction2]
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_RandomActionWithEmptySubActions_ShouldNotThrow()
        {
            var actionType = new ActionType
            {
                Name = "EmptyRandomAction",
                Enabled = true,
                RandomAction = true,
                SubActions = []
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_ConcurrentAction_ShouldRunAllSubActionsConcurrently()
        {
            var subAction1 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 1"
            };
            var subAction2 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 2"
            };
            var subAction3 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 3"
            };

            var actionType = new ActionType
            {
                Name = "ConcurrentTestAction",
                Enabled = true,
                ConcurrentAction = true,
                SubActions = [subAction1, subAction2, subAction3]
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

       

        [Fact]
        public async Task RunActionAsCommand_SequentialAction_ShouldRunAllSubActionsInOrder()
        {
            var subAction1 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 1",
                Index = 1
            };
            var subAction2 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 2",
                Index = 2
            };

            var actionType = new ActionType
            {
                Name = "SequentialTestAction",
                Enabled = true,
                RandomAction = false,
                ConcurrentAction = false,
                SubActions = [subAction2, subAction1]
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_UnknownSubActionType_ShouldLogWarning()
        {
            var subAction = new SendMessageType
            {
                SubActionTypes = SubActionTypes.None,
                Text = "Test"
            };

            var actionType = new ActionType
            {
                Name = "UnknownAction",
                Enabled = true,
                SubActions = [subAction]
            };

            await action.RunAction(variables, actionType);

            factoryLogger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("No handler found")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_SendMessageSubAction_ShouldNotLogWarning()
        {
            var subAction = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Test message"
            };

            var actionType = new ActionType
            {
                Name = "SendMessageAction",
                Enabled = true,
                SubActions = [subAction]
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_NullSendMessageType_ShouldNotThrow()
        {
            var subAction = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Test"
            };

            var actionType = new ActionType
            {
                Name = "NullSendMessageAction",
                Enabled = true,
                SubActions = [subAction]
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Unrecognized action type")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_EmptySubActions_ShouldNotThrow()
        {
            var actionType = new ActionType
            {
                Name = "EmptyAction",
                Enabled = true,
                SubActions = []
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_EmptyVariablesDictionary_ShouldNotThrow()
        {
            var emptyVariables = new Dictionary<string, string>();
            var subAction = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Test message"
            };

            var actionType = new ActionType
            {
                Name = "ActionWithEmptyVariables",
                Enabled = true,
                SubActions = [subAction]
            };

            await action.RunAction(emptyVariables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_MultipleSubActionsWithMixedTypes_ShouldHandleAll()
        {
            var sendMessageAction = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Valid message"
            };
            var alertAction = new AlertType
            {
                SubActionTypes = SubActionTypes.Alert,
                Text = "Alert message"
            };

            var actionType = new ActionType
            {
                Name = "MixedAction",
                Enabled = true,
                SubActions = [sendMessageAction, alertAction]
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task RunActionAsCommand_RandomActionTakesPrecedenceOverConcurrent()
        {
            var subAction1 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 1"
            };
            var subAction2 = new SendMessageType
            {
                SubActionTypes = SubActionTypes.SendMessage,
                Text = "Message 2"
            };

            var actionType = new ActionType
            {
                Name = "RandomAndConcurrentAction",
                Enabled = true,
                RandomAction = true,
                ConcurrentAction = true,
                SubActions = [subAction1, subAction2]
            };

            await action.RunAction(variables, actionType);

            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
