# Queue System Documentation

## Overview
The Queue System allows you to execute Actions through configurable queues that can be either blocking (sequential) or non-blocking (concurrent). Each queue maintains statistics about pending and completed actions.

## Features
- **Blocking Queues**: Execute actions one at a time in sequence
- **Non-Blocking Queues**: Execute multiple actions concurrently (with configurable max concurrency)
- **Default Queue**: Always available, not stored in database
- **Statistics**: Track pending and completed actions per queue
- **Enable/Disable**: Control queue execution without deleting configuration

## Queue Configuration

### Properties
- `Name` (string): Unique queue name
- `IsBlocking` (bool): True for sequential, false for concurrent execution
- `Enabled` (bool): Whether the queue is currently processing actions
- `MaxConcurrentActions` (int): Maximum concurrent actions for non-blocking queues

### Default Queue
- Name: `"default"`
- Type: Blocking (sequential)
- Cannot be deleted or modified
- Not stored in database

## Usage Examples

### 1. Enqueue an Action
```csharp
// Using Action service
var actionService = scope.ServiceProvider.GetRequiredService<Bot.Actions.Action>();
var variables = new Dictionary<string, string> { { "user", "TestUser" } };

// This will automatically enqueue to the queue specified in action.QueueName
await actionService.EnqueueAction(variables, myAction);
```

### 2. Create a New Queue
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

var config = new QueueConfiguration
{
    Name = "alerts",
    IsBlocking = false,
    MaxConcurrentActions = 5,
    Enabled = true
};

await queueManager.CreateQueueAsync(config);
```

### 3. Get Queue Statistics
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

// Get statistics for a specific queue
var stats = await queueManager.GetQueueStatisticsAsync("alerts");
Console.WriteLine($"Pending: {stats.PendingActions}, Completed: {stats.CompletedActions}");

// Get statistics for all queues
var allStats = await queueManager.GetAllQueueStatisticsAsync();
foreach (var stat in allStats)
{
    Console.WriteLine($"{stat.QueueName}: {stat.PendingActions} pending, {stat.CompletedActions} completed");
}
```

### 4. Update a Queue
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

var config = await db.QueueConfigurations.Find(q => q.Name == "alerts").FirstOrDefaultAsync();
config.MaxConcurrentActions = 10;
config.Enabled = false; // Disable the queue

await queueManager.UpdateQueueAsync(config);
```

### 5. Delete a Queue
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();
await queueManager.DeleteQueueAsync("alerts");
```

## Action Configuration

When creating an action, specify which queue it should use:

```csharp
var action = new ActionType
{
    Name = "MyAction",
    QueueName = "alerts", // Will use the "alerts" queue
    // ... other properties
};

// Or use default queue
var defaultAction = new ActionType
{
    Name = "MyDefaultAction",
    QueueName = "default", // Will use the default queue
    // ... other properties
};
```

## Queue Types

### Blocking Queue (Sequential)
- Processes one action at a time
- Actions are queued and executed in order
- Best for actions that must run sequentially
- Example: File writing, database updates

### Non-Blocking Queue (Concurrent)
- Processes multiple actions simultaneously
- MaxConcurrentActions controls the level of concurrency
- Best for independent actions
- Example: Sending chat messages, playing sounds, alerts

## Statistics

Each queue tracks:
- `PendingActions`: Number of actions waiting to be executed
- `CompletedActions`: Total number of successfully executed actions
- `CurrentlyExecuting`: Number of actions currently being processed
- `IsEnabled`: Whether the queue is processing actions
- `IsBlocking`: Queue execution type

## Best Practices

1. **Use Descriptive Names**: Name queues based on their purpose (e.g., "alerts", "chat-messages", "file-operations")

2. **Choose the Right Type**:
   - Use blocking queues for operations that must be sequential
   - Use non-blocking queues for independent operations that can run in parallel

3. **Set Appropriate Concurrency**:
   - For non-blocking queues, set MaxConcurrentActions based on resource availability
   - Too high: May overwhelm resources
   - Too low: May not utilize available resources

4. **Monitor Statistics**:
   - Regularly check pending actions to identify bottlenecks
   - Monitor completed actions to track throughput

5. **Enable/Disable vs Delete**:
   - Use Enable/Disable to temporarily pause a queue
   - Delete only when the queue is no longer needed

## Error Handling

- If a queue doesn't exist, actions are automatically routed to the default queue
- Failed actions are logged but don't block the queue
- Queue processing continues even if individual actions fail

## Action Execution Logging

The system includes an in-memory execution logger that tracks the lifecycle of every action executed through the queues. This provides observability for debugging and monitoring without database overhead.

### Execution Log Properties
- `Id` (Guid): Unique identifier for the log entry
- `ActionName` (string): Name of the action being executed
- `State` (ActionExecutionState): Current state (Pending, Running, Completed, Failed)
- `Variables` (Dictionary<string, string>): Copy of all variables passed to the action
- `QueueName` (string): Name of the queue executing the action
- `EnqueuedAt` (DateTime): When the action was added to the queue
- `StartedAt` (DateTime?): When execution began
- `CompletedAt` (DateTime?): When execution finished
- `ErrorMessage` (string?): Error details if execution failed

### Computed Metrics
- `ExecutionDuration`: Time spent executing the action (StartedAt - CompletedAt)
- `WaitTime`: Time waiting in queue before execution (EnqueuedAt - StartedAt)
- `TotalTime`: Total time from enqueue to completion (EnqueuedAt - CompletedAt)

### Usage Examples

#### Get Recent Execution Logs
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

// Get the last 100 actions (most recent first)
var recentLogs = queueManager.ExecutionLogger.GetRecentLogs(100);

foreach (var log in recentLogs)
{
    Console.WriteLine($"{log.ActionName} - {log.State} - Queue: {log.QueueName}");
    Console.WriteLine($"  Enqueued: {log.EnqueuedAt}");
    if (log.ExecutionDuration.HasValue)
    {
        Console.WriteLine($"  Duration: {log.ExecutionDuration.Value.TotalMilliseconds}ms");
    }
}
```

#### Get Logs by Queue
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

// Get all logs for the "alerts" queue
var alertLogs = queueManager.ExecutionLogger.GetLogsByQueue("alerts", count: 50);

Console.WriteLine($"Found {alertLogs.Count} actions in alerts queue");
```

#### Get Logs by State
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

// Find all failed actions
var failedLogs = queueManager.ExecutionLogger.GetLogsByState(ActionExecutionState.Failed, count: 20);

foreach (var log in failedLogs)
{
    Console.WriteLine($"{log.ActionName} failed: {log.ErrorMessage}");
    Console.WriteLine($"  Variables: {string.Join(", ", log.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
}

// Find all currently running actions
var runningLogs = queueManager.ExecutionLogger.GetLogsByState(ActionExecutionState.Running);
Console.WriteLine($"{runningLogs.Count} actions currently running");
```

#### Get Logs Since a Time
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

// Get all logs from the last hour
var since = DateTime.UtcNow.AddHours(-1);
var recentLogs = queueManager.ExecutionLogger.GetLogs(since);

Console.WriteLine($"Found {recentLogs.Count} actions in the last hour");
```

#### Clear Logs
```csharp
var queueManager = scope.ServiceProvider.GetRequiredService<IQueueManager>();

// Clear all logs (useful for testing or maintenance)
queueManager.ExecutionLogger.Clear();
```

### Configuration

The logger is configured as a singleton with a default capacity of 1000 entries. When the limit is reached, the oldest entries are automatically removed.

To change the maximum log entries:
```csharp
// In BotCommandsRegistry.cs
services.AddSingleton<IActionExecutionLogger>(sp => 
    new ActionExecutionLogger(
        sp.GetRequiredService<ILogger<ActionExecutionLogger>>(), 
        maxLogEntries: 5000));
```

### Automatic Logging

Execution logging happens automatically when actions are enqueued and executed:

1. **Enqueue**: Log entry created with `State = Pending` and `EnqueuedAt` timestamp
2. **Start Execution**: State updated to `Running`, `StartedAt` timestamp set
3. **Complete**: State updated to `Completed`, `CompletedAt` timestamp set
4. **Failure**: State updated to `Failed`, `CompletedAt` and `ErrorMessage` set

No manual logging is required - the system handles it automatically.

### Use Cases

- **Debugging**: View recent action executions to troubleshoot issues
- **Monitoring**: Track failed actions and error patterns
- **Performance Analysis**: Use timing metrics to identify slow actions
- **Audit Trail**: See what actions ran with which variables
- **Queue Health**: Check for pending actions stuck in queues

