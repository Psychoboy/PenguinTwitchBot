# CommandEventArgsConverter Utility

## Overview
The `CommandEventArgsConverter` utility provides a convenient way to convert `CommandEventArgs` objects into a `Dictionary<string, string>` format suitable for variable substitution in the Actions system.

## Purpose
When executing Actions triggered by commands, we need to make command context data available as variables. This utility standardizes the conversion of `CommandEventArgs` properties into a dictionary format that can be used throughout the Actions system.

## Location
- **Utility Class**: `DotNetTwitchBot/Bot/Utilities/CommandEventArgsConverter.cs`
- **Tests**: `DotNetTwitchBot.Test/Bot/Utilities/CommandEventArgsConverterTests.cs`

## Usage

### Basic Conversion
```csharp
var eventArgs = new CommandEventArgs
{
    Command = "hello",
    DisplayName = "TestUser",
    Args = new List<string> { "arg1", "arg2", "arg3" }
};

var variables = CommandEventArgsConverter.ToDictionary(eventArgs);
// variables["Command"] = "hello"
// variables["DisplayName"] = "TestUser"
// variables["Args_0"] = "arg1"
// variables["Args_1"] = "arg2"
// variables["Args_2"] = "arg3"
```

### With Additional Values
```csharp
var additionalValues = new Dictionary<string, string>
{
    ["CustomKey"] = "CustomValue"
};

var variables = CommandEventArgsConverter.ToDictionary(eventArgs, additionalValues);
// Contains all eventArgs properties plus custom values
```

## Dictionary Keys

### From BaseChatEventArgs
- `IsSub` - Boolean as string
- `IsMod` - Boolean as string
- `IsVip` - Boolean as string
- `IsBroadcaster` - Boolean as string
- `DisplayName` - User's display name
- `Name` - User's login name
- `UserId` - User's Twitch ID
- `MessageId` - Message ID

### From CommandEventArgs
- `Command` - Command name (without !)
- `Arg` - Full argument string
- `TargetUser` - Target user (if specified)
- `IsWhisper` - Boolean as string
- `IsDiscord` - Boolean as string
- `DiscordMention` - Discord mention string
- `FromAlias` - Boolean as string
- `SkipLock` - Boolean as string
- `FromOwnChannel` - Boolean as string

### Args Array
- `Args_0` - First argument
- `Args_1` - Second argument
- `Args_2` - Third argument
- ... (continues for all arguments)

## Features

### Case-Insensitive Keys
The dictionary uses case-insensitive key comparison, so `dictionary["command"]`, `dictionary["Command"]`, and `dictionary["COMMAND"]` all access the same value.

### Null Safety
- Null `CommandEventArgs` returns an empty dictionary
- Null string properties are converted to empty strings
- Null or empty Args list is handled gracefully

### Extensibility
The overload with `additionalValues` parameter allows merging custom key-value pairs with the standard CommandEventArgs properties.

## Example: ActionCommandHandler Integration

```csharp
public class ActionCommandHandler : INotificationHandler<RunCommandNotification>
{
    public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
    {
        if (notification.EventArgs == null || string.IsNullOrWhiteSpace(notification.EventArgs.Command))
            return;
            
        var actions = await actionManagement.GetActionsByTriggerTypeAndNameAsync(
            TriggerTypes.Command, 
            "!" + notification.EventArgs.Command);

        var dictionary = CommandEventArgsConverter.ToDictionary(notification.EventArgs);

        foreach (var action in actions)
        {
            actionService.EnqueueAction(dictionary, action);
        }
    }
}
```

## Use Cases

1. **Command Actions**: Converting command context to variables for action execution
2. **Variable Substitution**: Providing context data for SubActions like SendMessage
3. **Logging**: Creating structured data for action execution logs
4. **Custom Commands**: Extending command functionality with additional context

## Testing
Comprehensive unit tests cover:
- ✅ Null handling
- ✅ All property conversions
- ✅ Args indexing
- ✅ Additional values merging
- ✅ Case insensitivity
- ✅ Edge cases (empty args, null strings, etc.)

All 10 tests pass successfully.

## Benefits

1. **Standardization**: Consistent way to convert command context across the codebase
2. **Type Safety**: Static utility methods with clear signatures
3. **Extensibility**: Easy to add custom variables without modifying core logic
4. **Maintainability**: Single source of truth for conversion logic
5. **Testability**: Well-tested with comprehensive unit tests
