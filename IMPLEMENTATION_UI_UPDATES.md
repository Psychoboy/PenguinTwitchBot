# UI Updates for Default Command Triggers

## Summary

The UI has been fully updated to support creating and managing DefaultCommand triggers through the Blazor interface.

## Changes Made

### 1. AddTriggerDialog.razor
**Location:** `DotNetTwitchBot/Pages/Actions/AddTriggerDialog.razor`

**New Features:**
- Added "Default Command" option to trigger type selection menu
  - Icon: Functions (green color)
  - Description: "Trigger on default command events (gamble win, defuse, etc.)"

**Configuration UI:**
When DefaultCommand is selected, users see:
1. **Default Command Selector**
   - Auto-complete dropdown
   - Populated with all default commands from the database
   - Search functionality included

2. **Event Type Selector**
   - Dropdown that appears after selecting a default command
   - Shows only events relevant to the selected command
   - Display names are user-friendly (e.g., "Jackpot Win" instead of "Gamble.JackpotWin")

3. **Trigger Name**
   - Auto-generated based on command and event
   - Format: `{commandName}_{eventName}`
   - Example: `gamble_JackpotWin`
   - Read-only to ensure consistency

**Code Additions:**
- Injected `ICommandHandler` to access default commands
- Added fields: `_defaultCommands`, `_selectedDefaultCommand`, `_selectedEventType`
- Added methods:
  - `SearchDefaultCommands()` - Search functionality for command selector
  - `OnDefaultCommandChanged()` - Handles command selection
  - `OnEventTypeChanged()` - Handles event type selection
  - `UpdateDefaultCommandTriggerName()` - Auto-generates trigger name
  - `GetEventTypesForCommand()` - Returns available events for a command
  - `GetEventTypeDisplayName()` - Converts internal event names to display names
- Updated `CanSubmit()` to validate DefaultCommand trigger requirements
- Updated `Submit()` to serialize DefaultCommandTriggerConfiguration

### 2. TriggerManagement.razor
**Location:** `DotNetTwitchBot/Pages/Components/TriggerManagement.razor`

**Display Updates:**
- Added display logic for DefaultCommand triggers in the table
- Shows trigger name with event summary below (e.g., "Event: Jackpot Win")
- Uses green color chip for DefaultCommand type

**Code Additions:**
- Added using statement for `DotNetTwitchBot.Bot.Actions.Triggers.Configurations`
- Updated trigger display logic to handle DefaultCommand type
- Updated `OpenAddTriggerDialog()` to parse DefaultCommandId from configuration
- Updated `GetTriggerTypeColor()` to return green (Color.Success) for DefaultCommand
- Added `GetDefaultCommandEventSummary()` method to display event information

## How to Use the UI

### Creating a DefaultCommand Trigger

1. Navigate to Actions → Manage Actions
2. Select or create an action
3. Click "Add Trigger"
4. Select "Default Command" from the trigger type list
5. Select a default command from the dropdown (e.g., "gamble")
6. Select an event type (e.g., "Jackpot Win")
7. The trigger name will auto-generate
8. Click "Add Trigger"

### Supported Commands and Events

**Gamble Command:**
- Jackpot Win
- Win
- Lose

**Defuse Command:**
- Success
- Failure

### Adding Support for New Commands

To add UI support for a new default command:

1. Open `DotNetTwitchBot/Pages/Actions/AddTriggerDialog.razor`

2. Update `GetEventTypesForCommand` method:
```csharp
private List<string> GetEventTypesForCommand(string commandName)
{
    return commandName.ToLower() switch
    {
        "gamble" => new List<string>
        {
            DefaultCommandEventTypes.GambleJackpotWin,
            DefaultCommandEventTypes.GambleWin,
            DefaultCommandEventTypes.GambleLose
        },
        "defuse" => new List<string>
        {
            DefaultCommandEventTypes.DefuseSuccess,
            DefaultCommandEventTypes.DefuseFailure
        },
        "yournewcommand" => new List<string>
        {
            DefaultCommandEventTypes.YourNewCommandEvent1,
            DefaultCommandEventTypes.YourNewCommandEvent2
        },
        _ => new List<string>()
    };
}
```

3. Update `GetEventTypeDisplayName` method:
```csharp
private string GetEventTypeDisplayName(string eventType)
{
    return eventType switch
    {
        DefaultCommandEventTypes.GambleJackpotWin => "Jackpot Win",
        DefaultCommandEventTypes.GambleWin => "Win",
        DefaultCommandEventTypes.GambleLose => "Lose",
        DefaultCommandEventTypes.DefuseSuccess => "Success",
        DefaultCommandEventTypes.DefuseFailure => "Failure",
        DefaultCommandEventTypes.YourNewCommandEvent1 => "Your Display Name 1",
        DefaultCommandEventTypes.YourNewCommandEvent2 => "Your Display Name 2",
        _ => eventType
    };
}
```

4. Update `GetDefaultCommandEventSummary` in `TriggerManagement.razor`:
```csharp
var eventDisplayName = config.EventType switch
{
    DefaultCommandEventTypes.GambleJackpotWin => "Jackpot Win",
    DefaultCommandEventTypes.GambleWin => "Win",
    DefaultCommandEventTypes.GambleLose => "Lose",
    DefaultCommandEventTypes.DefuseSuccess => "Success",
    DefaultCommandEventTypes.DefuseFailure => "Failure",
    DefaultCommandEventTypes.YourNewCommandEvent1 => "Your Display Name 1",
    DefaultCommandEventTypes.YourNewCommandEvent2 => "Your Display Name 2",
    _ => config.EventType
};
```

## Validation

The UI includes proper validation:
- Default command must be selected before event type dropdown appears
- Both command and event type must be selected before submission is allowed
- Trigger name is auto-generated and cannot be manually edited
- Configuration is properly serialized as JSON to the database

## Visual Design

- **Trigger Type Icon**: Functions icon in green
- **Chip Color**: Green (Color.Success)
- **Layout**: Follows existing MudBlazor patterns for consistency
- **Event Display**: Shows event type below trigger name for easy identification
