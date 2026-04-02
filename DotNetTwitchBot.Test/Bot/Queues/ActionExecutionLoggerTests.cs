using DotNetTwitchBot.Bot.Models.Queues;
using DotNetTwitchBot.Bot.Queues;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Queues
{
    public class ActionExecutionLoggerTests
    {
        [Fact]
        public void LogActionEnqueued_CreatesLogEntry()
        {
            // Arrange
            var logger = Substitute.For<ILogger<ActionExecutionLogger>>();
            var executionLogger = new ActionExecutionLogger(logger);
            var variables = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };

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
            var executionLogger = new ActionExecutionLogger(logger);
            var variables = new Dictionary<string, string>();
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
            var executionLogger = new ActionExecutionLogger(logger);
            var variables = new Dictionary<string, string>();
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variables);
            executionLogger.UpdateActionStarted(logId);
            var variablesAfter = new Dictionary<string, string>();

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
            var executionLogger = new ActionExecutionLogger(logger);
            var variables = new Dictionary<string, string>();
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
            var executionLogger = new ActionExecutionLogger(logger);
            
            executionLogger.LogActionEnqueued("Action1", "queue1", new Dictionary<string, string>());
            executionLogger.LogActionEnqueued("Action2", "queue2", new Dictionary<string, string>());
            executionLogger.LogActionEnqueued("Action3", "queue1", new Dictionary<string, string>());

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
            var executionLogger = new ActionExecutionLogger(logger);
            
            var logId1 = executionLogger.LogActionEnqueued("Action1", "default", new Dictionary<string, string>());
            var logId2 = executionLogger.LogActionEnqueued("Action2", "default", new Dictionary<string, string>());
            var logId3 = executionLogger.LogActionEnqueued("Action3", "default", new Dictionary<string, string>());
            
            executionLogger.UpdateActionStarted(logId1);
            executionLogger.UpdateActionCompleted(logId1, new Dictionary<string, string>());
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
            var executionLogger = new ActionExecutionLogger(logger);
            
            var beforeTime = DateTime.UtcNow;
            Thread.Sleep(10);
            
            executionLogger.LogActionEnqueued("Action1", "default", new Dictionary<string, string>());
            executionLogger.LogActionEnqueued("Action2", "default", new Dictionary<string, string>());
            
            Thread.Sleep(10);
            var afterFirstTwo = DateTime.UtcNow;
            Thread.Sleep(10);
            
            executionLogger.LogActionEnqueued("Action3", "default", new Dictionary<string, string>());

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
            var executionLogger = new ActionExecutionLogger(logger);
            
            for (int i = 0; i < 10; i++)
            {
                executionLogger.LogActionEnqueued($"Action{i}", "default", new Dictionary<string, string>());
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
            var executionLogger = new ActionExecutionLogger(logger);
            
            executionLogger.LogActionEnqueued("Action1", "default", new Dictionary<string, string>());
            executionLogger.LogActionEnqueued("Action2", "default", new Dictionary<string, string>());
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
            var executionLogger = new ActionExecutionLogger(logger, maxLogEntries: 5);
            
            for (int i = 0; i < 10; i++)
            {
                executionLogger.LogActionEnqueued($"Action{i}", "default", new Dictionary<string, string>());
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
            var executionLogger = new ActionExecutionLogger(logger);
            var variables = new Dictionary<string, string>();
            
            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variables);
            Thread.Sleep(20);
            
            executionLogger.UpdateActionStarted(logId);
            Thread.Sleep(20);

            executionLogger.UpdateActionCompleted(logId, new Dictionary<string, string>());

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
            var executionLogger = new ActionExecutionLogger(logger);
            var variablesBefore = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            var logId = executionLogger.LogActionEnqueued("TestAction", "default", variablesBefore);
            executionLogger.UpdateActionStarted(logId);

            var variablesAfter = new Dictionary<string, string>
            {
                { "key1", "modifiedValue1" },
                { "key2", "value2" },
                { "newKey", "newValue" }
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
    }
}
