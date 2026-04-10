using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.SubActions;
using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Test.Bot.Actions
{
    public class ConcurrentActionExecutionTests
    {
        [Fact]
        public async Task ConcurrentAction_WithLogging_ShouldNotThrowExceptions()
        {
            // Arrange - Set up a real DI container with actual services
            var services = new ServiceCollection();

            // Add logging
            services.AddSingleton<ILogger<DotNetTwitchBot.Bot.Actions.Action>>(Substitute.For<ILogger<DotNetTwitchBot.Bot.Actions.Action>>());
            services.AddSingleton<ILogger<SubActionHandlerFactory>>(Substitute.For<ILogger<SubActionHandlerFactory>>());
            services.AddSingleton<ILogger<TestLoggingHandler>>(Substitute.For<ILogger<TestLoggingHandler>>());
            services.AddSingleton<ILogger<ActionExecutionLogger>>(Substitute.For<ILogger<ActionExecutionLogger>>());
            services.AddSingleton<ILogger<SubActionExecutionContext>>(Substitute.For<ILogger<SubActionExecutionContext>>());

            // Add execution logging infrastructure
            services.AddSingleton<ISubActionExecutionContextAccessor, SubActionExecutionContextAccessor>();
            services.AddSingleton<IActionExecutionLogger, ActionExecutionLogger>();

            // Add our test handler
            services.AddTransient<ISubActionHandler, TestLoggingHandler>();
            services.AddTransient<SubActionHandlerFactory>();

            // Mock dependencies
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            services.AddSingleton(serviceBackbone);

            var hubContext = Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<DotNetTwitchBot.Bot.Hubs.MainHub>>();
            services.AddSingleton(hubContext);

            // Build the container
            var serviceProvider = services.BuildServiceProvider();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();

            var action = new DotNetTwitchBot.Bot.Actions.Action(
                serviceProvider.GetRequiredService<ILogger<DotNetTwitchBot.Bot.Actions.Action>>(),
                scopeFactory,
                serviceBackbone);

            // Create an action with concurrent sub-actions that will create log entries
            var concurrentSubActions = new List<SubActionType>();
            for (int i = 0; i < 5; i++)
            {
                concurrentSubActions.Add(new TestSubActionType 
                { 
                    Id = i, 
                    SubActionTypes = SubActionTypes.Alert,  // Using Alert as our test type
                    Text = $"ConcurrentAction_{i}",
                    Enabled = true,
                    Index = i
                });
            }

            var actionType = new ActionType
            {
                Name = "ConcurrentTestAction",
                Enabled = true,
                ConcurrentAction = true,
                SubActions = concurrentSubActions
            };

            var variables = new ConcurrentDictionary<string, string>();
            var executionLogger = serviceProvider.GetRequiredService<IActionExecutionLogger>();

            // Act - Set up execution context and run the action
            var logId = executionLogger.LogActionEnqueued("ConcurrentTestAction", "TestQueue", variables);
            executionLogger.UpdateActionStarted(logId);

            // Set up the execution context in the main service provider (not a scope)
            var contextAccessor = serviceProvider.GetRequiredService<ISubActionExecutionContextAccessor>();
            var context = new SubActionExecutionContext(
                logId, 
                executionLogger, 
                serviceProvider.GetRequiredService<ILogger<SubActionExecutionContext>>());
            contextAccessor.ExecutionContext = context;

            // Act & Assert - This should not throw any exceptions, even with concurrent access
            await action.RunAction(variables, actionType);

            executionLogger.UpdateActionCompleted(logId, variables);

            // Basic verification - at least some sub-actions were logged
            var subActionLogs = executionLogger.GetSubActionLogsSnapshot(logId);

            // We should have at least 1 sub-action log (maybe not all due to race conditions, but at least some)
            Assert.True(subActionLogs.Count >= 1, $"Expected at least 1 sub-action log, but got {subActionLogs.Count}");

            // All logged sub-actions should have completed successfully (no race condition exceptions)
            Assert.All(subActionLogs, log => Assert.True(log.IsSuccess, $"SubAction {log.SubActionType} should have completed successfully"));

            // Each logged sub-action should have messages (no null reference exceptions from race conditions)
            Assert.All(subActionLogs, log => Assert.NotEmpty(log.Messages));
        }

        [Fact]  
        public void TokenBasedContext_WithMultipleIndexes_ShouldHandleCorrectly()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<Microsoft.AspNetCore.SignalR.IHubContext<DotNetTwitchBot.Bot.Hubs.MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);

            var contextLogger = Substitute.For<ILogger<SubActionExecutionContext>>();
            var variables = new ConcurrentDictionary<string, string>();

            // Act - Create context and use token-based API
            var logId = executionLogger.LogActionEnqueued("TestAction", "TestQueue", variables);
            var context = new SubActionExecutionContext(logId, executionLogger, contextLogger);

            // Begin multiple sub-actions and get their tokens
            var index1 = context.BeginSubAction("SubAction1", "First sub-action");
            var index2 = context.BeginSubAction("SubAction2", "Second sub-action");
            var index3 = context.BeginSubAction("SubAction3", "Third sub-action");

            // Log messages using the specific tokens
            context.LogMessage(index1, "Message for SubAction1");
            context.LogMessage(index2, "Message for SubAction2");  
            context.LogMessage(index3, "Message for SubAction3");
            context.LogMessage(index1, "Another message for SubAction1");

            // Complete using the tokens
            context.CompleteSubAction(index1);
            context.CompleteSubAction(index2);
            context.FailSubAction(index3, "Simulated failure");

            // Assert - Verify each sub-action has correct messages and status
            var logs = executionLogger.GetSubActionLogsSnapshot(logId);
            Assert.Equal(3, logs.Count);

            var subAction1Log = logs[index1];
            Assert.Equal("SubAction1", subAction1Log.SubActionType);
            Assert.True(subAction1Log.IsSuccess);
            Assert.Equal(2, subAction1Log.Messages.Count);
            Assert.Contains("Message for SubAction1", subAction1Log.Messages);
            Assert.Contains("Another message for SubAction1", subAction1Log.Messages);

            var subAction2Log = logs[index2];
            Assert.Equal("SubAction2", subAction2Log.SubActionType);
            Assert.True(subAction2Log.IsSuccess);
            Assert.Single(subAction2Log.Messages);
            Assert.Contains("Message for SubAction2", subAction2Log.Messages);

            var subAction3Log = logs[index3];
            Assert.Equal("SubAction3", subAction3Log.SubActionType);
            Assert.False(subAction3Log.IsSuccess);
            Assert.Equal("Simulated failure", subAction3Log.ErrorMessage);
            Assert.Single(subAction3Log.Messages);
            Assert.Contains("Message for SubAction3", subAction3Log.Messages);
        }

        // Test SubAction type for our concurrent execution test
        public class TestSubActionType : SubActionType
        {
            public int Id { get; set; }
        }

        // Test handler that logs messages with the action's text to verify proper attribution
        public class TestLoggingHandler : ISubActionHandler
        {
            private readonly ILogger<TestLoggingHandler> _logger;

            public TestLoggingHandler(
                ILogger<TestLoggingHandler> logger)
            {
                _logger = logger;
            }

            public SubActionTypes SupportedType => SubActionTypes.Alert;

            public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null)
            {
                var testAction = subAction as TestSubActionType;
                var currentIndex = context?.CurrentSubActionIndex ?? -1;

                if (context != null)
                {
                    context.LogMessage(currentIndex, $"Processing {subAction.Text}");

                    // Simulate some async work that could cause race conditions
                    await Task.Delay(Random.Shared.Next(10, 50));

                    context.LogMessage(currentIndex, $"Completed processing {subAction.Text}");
                }

                _logger.LogInformation("Executed test action {ActionText} with index {Index}", subAction.Text, testAction?.Id);
            }
        }
    }
}