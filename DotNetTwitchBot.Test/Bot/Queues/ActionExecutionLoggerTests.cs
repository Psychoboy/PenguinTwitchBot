using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Bot.Models.Queues;
using DotNetTwitchBot.Bot.Queues;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Test.Bot.Queues
{
    public class ActionExecutionLoggerTests
    {
        [Fact]
        public void LogActionEnqueued_CreatesLogEntry()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var variables = new ConcurrentDictionary<string, string>();
            variables["key1"] = "value1";
            variables["key2"] = "value2";

            // Act
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variables);

            // Assert
            Assert.NotEqual(Guid.Empty, logId);
            Assert.Equal(1, executionLogger.GetLogCount());
            
            var logs = executionLogger.GetRecentLogs();
            Assert.Single(logs);
            Assert.Equal("TestAction", logs[0].ActionName);
            Assert.Equal("default", logs[0].QueueName);
            Assert.Equal(ActionExecutionState.Pending, logs[0].State);
            Assert.Equal(2, logs[0].VariablesBefore.Count);
            Assert.Equal("value1", logs[0].VariablesBefore["key1"]);
        }

        [Fact]
        public void UpdateActionStarted_TransitionsStateToRunning()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var variables = new ConcurrentDictionary<string, string>();
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variables);

            // Act
            executionLogger.UpdateActionStarted(logId);

            // Assert
            var logs = executionLogger.GetRecentLogs();
            Assert.Single(logs);
            Assert.Equal(ActionExecutionState.Running, logs[0].State);
            Assert.NotNull(logs[0].StartedAt);
            Assert.Null(logs[0].CompletedAt);
        }

        [Fact]
        public void UpdateActionCompleted_TransitionsStateToCompleted()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var variables = new ConcurrentDictionary<string, string>();
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variables);
            executionLogger.UpdateActionStarted(logId);
            var variablesAfter = new ConcurrentDictionary<string, string>();

            // Act
            executionLogger.UpdateActionCompleted(logId, variablesAfter);

            // Assert
            var logs = executionLogger.GetRecentLogs();
            Assert.Single(logs);
            Assert.Equal(ActionExecutionState.Completed, logs[0].State);
            Assert.NotNull(logs[0].StartedAt);
            Assert.NotNull(logs[0].CompletedAt);
            Assert.NotNull(logs[0].ExecutionDuration);
            Assert.True(logs[0].ExecutionDuration.Value.TotalMilliseconds >= 0);
        }

        [Fact]
        public void UpdateActionFailed_TransitionsStateToFailedWithErrorMessage()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var variables = new ConcurrentDictionary<string, string>();
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variables);
            executionLogger.UpdateActionStarted(logId);

            // Act
            executionLogger.UpdateActionFailed(logId, "Test error message");

            // Assert
            var logs = executionLogger.GetRecentLogs();
            Assert.Single(logs);
            Assert.Equal(ActionExecutionState.Failed, logs[0].State);
            Assert.NotNull(logs[0].StartedAt);
            Assert.NotNull(logs[0].CompletedAt);
            Assert.Equal("Test error message", logs[0].ErrorMessage);
        }

        [Fact]
        public void GetLogsByQueue_ReturnsOnlyMatchingQueueLogs()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            
            executionLogger.LogActionEnqueued("Action1", "queue1", new ConcurrentDictionary<string, string>());
            executionLogger.LogActionEnqueued("Action2", "queue2", new ConcurrentDictionary<string, string>());
            executionLogger.LogActionEnqueued("Action3", "queue1", new ConcurrentDictionary<string, string>());

            // Act
            var queue1Logs = executionLogger.GetLogsByQueue("queue1");

            // Assert
            Assert.Equal(2, queue1Logs.Count);
            Assert.All(queue1Logs, log => Assert.Equal("queue1", log.QueueName));
        }

        [Fact]
        public void GetLogsByState_ReturnsOnlyMatchingStateLogs()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            
            var logId1 = executionLogger.LogActionEnqueued("Action1", "default", new ConcurrentDictionary<string, string>());
            var logId2 = executionLogger.LogActionEnqueued("Action2", "default", new ConcurrentDictionary<string, string>());
            var logId3 = executionLogger.LogActionEnqueued("Action3", "default", new ConcurrentDictionary<string, string>());
            
            executionLogger.UpdateActionStarted(logId1);
            executionLogger.UpdateActionCompleted(logId1, new ConcurrentDictionary<string, string>());
            executionLogger.UpdateActionStarted(logId2);
            executionLogger.UpdateActionFailed(logId2, "Error");

            // Act
            var pendingLogs = executionLogger.GetLogsByState(ActionExecutionState.Pending);
            var completedLogs = executionLogger.GetLogsByState(ActionExecutionState.Completed);
            var failedLogs = executionLogger.GetLogsByState(ActionExecutionState.Failed);

            // Assert
            Assert.Single(pendingLogs);
            Assert.Equal("Action3", pendingLogs[0].ActionName);
            
            Assert.Single(completedLogs);
            Assert.Equal("Action1", completedLogs[0].ActionName);
            
            Assert.Single(failedLogs);
            Assert.Equal("Action2", failedLogs[0].ActionName);
        }

        [Fact]
        public void GetLogs_ReturnsSinceSpecifiedDate()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            
            var beforeTime = DateTime.UtcNow;
            Thread.Sleep(10);
            
            executionLogger.LogActionEnqueued("Action1", "default", new ConcurrentDictionary<string, string>());
            executionLogger.LogActionEnqueued("Action2", "default", new ConcurrentDictionary<string, string>());
            
            Thread.Sleep(10);
            var afterFirstTwo = DateTime.UtcNow;
            Thread.Sleep(10);
            
            executionLogger.LogActionEnqueued("Action3", "default", new ConcurrentDictionary<string, string>());

            // Act
            var allLogs = executionLogger.GetLogs(beforeTime);
            var recentLogs = executionLogger.GetLogs(afterFirstTwo);

            // Assert
            Assert.Equal(3, allLogs.Count);
            Assert.Single(recentLogs);
            Assert.Equal("Action3", recentLogs[0].ActionName);
        }

        [Fact]
        public void GetRecentLogs_ReturnsRequestedCount()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            
            for (int i = 0; i < 10; i++)
            {
                executionLogger.LogActionEnqueued($"Action{i}", "default", new ConcurrentDictionary<string, string>());
            }

            // Act
            var logs = executionLogger.GetRecentLogs(5);

            // Assert
            Assert.Equal(5, logs.Count);
            Assert.Equal("Action9", logs[0].ActionName);
            Assert.Equal("Action5", logs[4].ActionName);
        }

        [Fact]
        public void Clear_RemovesAllLogs()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            
            executionLogger.LogActionEnqueued("Action1", "default", new ConcurrentDictionary<string, string>());
            executionLogger.LogActionEnqueued("Action2", "default", new ConcurrentDictionary<string, string>());
            Assert.Equal(2, executionLogger.GetLogCount());

            // Act
            executionLogger.Clear();

            // Assert
            Assert.Equal(0, executionLogger.GetLogCount());
            Assert.Empty(executionLogger.GetRecentLogs());
        }

        [Fact]
        public void EnforceMaxLogEntries_RemovesOldestEntries()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext, maxLogEntries: 5);
            
            for (int i = 0; i < 10; i++)
            {
                executionLogger.LogActionEnqueued($"Action{i}", "default", new ConcurrentDictionary<string, string>());
            }

            // Act & Assert
            Assert.Equal(5, executionLogger.GetLogCount());
            var logs = executionLogger.GetRecentLogs();
            Assert.Equal(5, logs.Count);
            Assert.Equal("Action9", logs[0].ActionName);
            Assert.Equal("Action5", logs[4].ActionName);
        }

        [Fact]
        public void ComputedProperties_CalculateCorrectTimings()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var variables = new ConcurrentDictionary<string, string>();
            
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variables);
            Thread.Sleep(20);
            
            executionLogger.UpdateActionStarted(logId);
            Thread.Sleep(20);

            executionLogger.UpdateActionCompleted(logId, new ConcurrentDictionary<string, string>());

            // Act
            var logs = executionLogger.GetRecentLogs();

            // Assert
            var log = logs[0];
            Assert.NotNull(log.WaitTime);
            Assert.NotNull(log.ExecutionDuration);
            Assert.NotNull(log.TotalTime);
            Assert.True(log.WaitTime.Value.TotalMilliseconds >= 10);
            Assert.True(log.ExecutionDuration.Value.TotalMilliseconds >= 10);
            Assert.True(log.TotalTime.Value.TotalMilliseconds >= 30);
        }

        [Fact]
        public void VariablesBeforeAndAfter_TracksVariableChanges()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var variablesBefore = new ConcurrentDictionary<string, string>
            {
                ["key1"] = "value1" ,
                ["key2"] = "value2"
            };

            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variablesBefore);
            executionLogger.UpdateActionStarted(logId);

            var variablesAfter = new ConcurrentDictionary<string, string>
            {
                ["key1"] = "modifiedValue1",
                ["key2"] = "value2",
                ["newKey"] = "newValue"
            };

            // Act
            executionLogger.UpdateActionCompleted(logId, variablesAfter);

            // Assert
            var logs = executionLogger.GetRecentLogs();
            var log = logs[0];

            Assert.Equal(2, log.VariablesBefore.Count);
            Assert.Equal("value1", log.VariablesBefore["key1"]);
            Assert.Equal("value2", log.VariablesBefore["key2"]);

            Assert.NotNull(log.VariablesAfter);
            Assert.Equal(3, log.VariablesAfter.Count);
            Assert.Equal("modifiedValue1", log.VariablesAfter["key1"]);
            Assert.Equal("value2", log.VariablesAfter["key2"]);
            Assert.Equal("newValue", log.VariablesAfter["newKey"]);
        }

        [Fact]
        public void LogSubActionStarted_ReturnsCorrectIndex()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", new ConcurrentDictionary<string, string>());

            // Act
            var index1 = executionLogger.LogSubActionStarted(logId, "SubAction1", "First sub-action", 0);
            var index2 = executionLogger.LogSubActionStarted(logId, "SubAction2", "Second sub-action", 0);
            var index3 = executionLogger.LogSubActionStarted(logId, "SubAction3", null, 1);

            // Assert
            Assert.Equal(0, index1);
            Assert.Equal(1, index2);
            Assert.Equal(2, index3);

            var logs = executionLogger.GetRecentLogs();
            Assert.Equal(3, logs[0].SubActionLogs.Count);
            Assert.Equal("SubAction1", logs[0].SubActionLogs[0].SubActionType);
            Assert.Equal("SubAction2", logs[0].SubActionLogs[1].SubActionType);
            Assert.Equal("SubAction3", logs[0].SubActionLogs[2].SubActionType);
        }

        [Fact]
        public void LogSubActionStarted_WithInvalidLogId_ReturnsNegativeOne()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var invalidLogId = Guid.NewGuid();

            // Act
            var index = executionLogger.LogSubActionStarted(invalidLogId, "SubAction", "Description", 0);

            // Assert
            Assert.Equal(-1, index);
        }

        [Fact]
        public void LogSubActionCompleted_UpdatesSubActionStatus()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", new ConcurrentDictionary<string, string>());
            var index = executionLogger.LogSubActionStarted(logId, "SubAction", "Test", 0);

            Thread.Sleep(10);

            // Act
            executionLogger.LogSubActionCompleted(logId, index);

            // Assert
            var logs = executionLogger.GetRecentLogs();
            var subAction = logs[0].SubActionLogs[index];
            Assert.True(subAction.IsSuccess);
            Assert.NotNull(subAction.CompletedAt);
            Assert.NotNull(subAction.Duration);
            Assert.True(subAction.Duration.Value.TotalMilliseconds >= 0);
        }

        [Fact]
        public void LogSubActionFailed_UpdatesSubActionStatusWithError()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", new ConcurrentDictionary<string, string>());
            var index = executionLogger.LogSubActionStarted(logId, "SubAction", "Test", 0);

            // Act
            executionLogger.LogSubActionFailed(logId, index, "Test error message");

            // Assert
            var logs = executionLogger.GetRecentLogs();
            var subAction = logs[0].SubActionLogs[index];
            Assert.False(subAction.IsSuccess);
            Assert.NotNull(subAction.CompletedAt);
            Assert.Equal("Test error message", subAction.ErrorMessage);
        }

        [Fact]
        public void LogSubActionMessage_AddsMessageToSubAction()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", new ConcurrentDictionary<string, string>());
            var index = executionLogger.LogSubActionStarted(logId, "SubAction", "Test", 0);

            // Act
            executionLogger.LogSubActionMessage(logId, index, "Message 1");
            executionLogger.LogSubActionMessage(logId, index, "Message 2");
            executionLogger.LogSubActionMessage(logId, index, "Message 3");

            // Assert
            var logs = executionLogger.GetRecentLogs();
            var subAction = logs[0].SubActionLogs[index];
            Assert.Equal(3, subAction.Messages.Count);
            Assert.Equal("Message 1", subAction.Messages[0]);
            Assert.Equal("Message 2", subAction.Messages[1]);
            Assert.Equal("Message 3", subAction.Messages[2]);
        }

        [Fact]
        public void LogSubActionMessage_TruncatesLongMessages()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", new ConcurrentDictionary<string, string>());
            var index = executionLogger.LogSubActionStarted(logId, "SubAction", "Test", 0);
            var longMessage = new string('A', 200);

            // Act
            executionLogger.LogSubActionMessage(logId, index, longMessage);

            // Assert
            var logs = executionLogger.GetRecentLogs();
            var subAction = logs[0].SubActionLogs[index];
            Assert.Single(subAction.Messages);
            Assert.Equal(150, subAction.Messages[0].Length);
            Assert.EndsWith("...", subAction.Messages[0]);
        }

        [Fact]
        public void LogSubAction_WithNestedDepth_TracksDepthCorrectly()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var hubContext = Substitute.For<IHubContext<MainHub>>();
            var executionLogger = new ActionExecutionLogger(logger, hubContext);
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", new ConcurrentDictionary<string, string>());

            // Act
            var index1 = executionLogger.LogSubActionStarted(logId, "SubAction1", "Depth 0", 0);
            var index2 = executionLogger.LogSubActionStarted(logId, "SubAction2", "Depth 1", 1);
            var index3 = executionLogger.LogSubActionStarted(logId, "SubAction3", "Depth 2", 2);
            var index4 = executionLogger.LogSubActionStarted(logId, "SubAction4", "Back to Depth 0", 0);

            // Assert
            var logs = executionLogger.GetRecentLogs();
            Assert.Equal(0, logs[0].SubActionLogs[index1].Depth);
            Assert.Equal(1, logs[0].SubActionLogs[index2].Depth);
            Assert.Equal(2, logs[0].SubActionLogs[index3].Depth);
            Assert.Equal(0, logs[0].SubActionLogs[index4].Depth);
        }
    }
}

