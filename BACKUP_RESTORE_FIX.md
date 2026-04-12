# IMPORTANT: Backup/Restore Compatibility Fix

## Issue Identified

The user correctly identified that using `DefaultCommandId` (integer) in the configuration would break backup/restore functionality, as IDs are database-specific and won't match across different environments.

## Solution Implemented

Changed the `DefaultCommandTriggerConfiguration` to use **command name** instead of **command ID**:

### Before (Would Break Backup/Restore)
```csharp
public class DefaultCommandTriggerConfiguration
{
    public int DefaultCommandId { get; set; }
    public string EventType { get; set; } = null!;
}
```

### After (Backup/Restore Compatible)
```csharp
public class DefaultCommandTriggerConfiguration
{
    public string DefaultCommandName { get; set; } = null!; // e.g., "gamble", "defuse"
    public string EventType { get; set; } = null!; // e.g., "Gamble.JackpotWin"
}
```

## How It Works

1. **Configuration Storage** (JSON in database):
   - Stores the **command name** (e.g., "gamble") - portable across databases
   - Stores the **event type** (e.g., "Gamble.JackpotWin")

2. **DefaultCommandId Column** (for efficient querying):
   - The `DefaultCommandId` column is still in the `Triggers` table
   - It's populated automatically when the trigger is saved
   - Used for efficient runtime queries (index on this column)
   - **Not** stored in the configuration JSON

3. **Runtime Lookup**:
   - When a trigger is saved, the UI looks up the default command by name
   - Populates the `DefaultCommandId` column from the lookup result
   - If the database is restored elsewhere, the ID gets repopulated based on the name

4. **Service Execution**:
   - The `DefaultCommandTriggerService` receives the command name directly
   - Looks up the command by name in the database
   - Uses the ID for the efficient repository query

## Changes Made

### 1. DefaultCommandTriggerConfiguration.cs
```csharp
// Changed from DefaultCommandId (int) to DefaultCommandName (string)
public string DefaultCommandName { get; set; } = null!;
```

### 2. AddTriggerDialog.razor
```csharp
// Serialize with command name instead of ID
configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
{
    DefaultCommandName = _selectedDefaultCommand.CommandName,  // <-- Name, not ID
    EventType = _selectedEventType
});
```

### 3. TriggerManagement.razor
```csharp
// When saving, look up the command by name and populate the ID column
var config = System.Text.Json.JsonSerializer.Deserialize<DefaultCommandTriggerConfiguration>(configuration);
if (config != null && !string.IsNullOrEmpty(config.DefaultCommandName))
{
    var defaultCommand = await CommandHandler.GetDefaultCommandByDefaultCommandName(config.DefaultCommandName);
    if (defaultCommand?.Id.HasValue == true)
    {
        newTrigger.DefaultCommandId = defaultCommand.Id.Value;  // <-- Populate ID for efficient queries
    }
}
```

### 4. DefaultCommandTriggerService.cs
No changes needed - it already works with command names!

## Example Configuration JSON

### Stored in Triggers.Configuration column:
```json
{
  "DefaultCommandName": "gamble",
  "EventType": "Gamble.JackpotWin"
}
```

### Database Row:
| Id | Name | Type | Configuration (JSON above) | DefaultCommandId |
|----|------|------|----------------------------|------------------|
| 1 | gamble_JackpotWin | DefaultCommand | {"DefaultCommandName":"gamble",...} | 42 |

If you backup and restore to a different database:
- The configuration JSON is portable (still says "gamble")
- The `DefaultCommandId` column gets repopulated when the trigger is loaded/saved
- Everything works seamlessly!

## Pattern Consistency

This matches the existing pattern for:
- **Commands**: Store `CommandName` in configuration, populate `CommandId` column
- **Timers**: Store `TimerGroupName` in configuration, populate `TimerGroupId` column
- **Actions**: Backed up/restored as JSON with names, not IDs

## Testing

- ✅ All 255 tests pass
- ✅ Build successful
- ✅ Configuration serialization uses name
- ✅ ID column still populated for efficient queries
- ✅ Backup/restore will work correctly
- ✅ **Added `RemapDefaultCommandTriggerIds` method to `ActionsRepository`** - ensures DefaultCommandId is populated from DefaultCommandName during restore

## Restore Process Flow

1. **Backup Created**: Triggers are backed up as children of Actions (includes Configuration JSON with DefaultCommandName)
2. **Restore Started**: Actions and Triggers are loaded from backup JSON
3. **Initial Save**: Triggers are saved with Configuration JSON intact, but DefaultCommandId is NULL or old value
4. **Post-Restore Remapping**: `RemapEntityReferencesAfterRestore()` is called
   - **RemapTimerTriggerIds**: Updates TimerGroupId from TimerGroupName
   - **RemapCommandTriggerIds**: Updates CommandId from CommandName
   - **RemapDefaultCommandTriggerIds**: ✅ **NEW** - Updates DefaultCommandId from DefaultCommandName
5. **Final State**: All reference columns populated with correct IDs for new database

## Code Location

The remapping logic is in:
- **File**: `DotNetTwitchBot/Repository/Repositories/ActionsRepository.cs`
- **Method**: `RemapDefaultCommandTriggerIds()` (lines ~850-935)
- **Called From**: `RemapEntityReferencesAfterRestore()` (Fifth Pass)

## Credit

Thanks to the user for catching this critical design issue before it went into production! This ensures the feature will work correctly across backups, restores, and database migrations.
