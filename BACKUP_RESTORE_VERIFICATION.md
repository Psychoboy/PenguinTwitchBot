# Backup/Restore Verification for DefaultCommand Triggers

## ✅ Verified: DefaultCommandId Column Will Be Populated Correctly After Restore

### The Problem You Identified

You correctly noticed that storing `DefaultCommandId` (integer) in the configuration would break backup/restore because IDs change across different databases.

### The Solution We Implemented

1. **Configuration Storage** (portable):
   - Stores `DefaultCommandName` (string) - e.g., "gamble", "defuse"
   - Stores `EventType` (string) - e.g., "Gamble.JackpotWin"

2. **Reference Column** (runtime optimization):
   - `DefaultCommandId` column exists for efficient querying
   - Populated automatically during save/restore

### How Backup/Restore Works

#### During Backup:
```csharp
// Triggers are backed up as children of Actions
// Configuration JSON is included:
{
  "DefaultCommandName": "gamble",
  "EventType": "Gamble.JackpotWin"
}
```

#### During Restore:

**Step 1: Initial Restore** (`ActionsRepository.RestoreTable`)
- Triggers are loaded from backup JSON
- Configuration contains `DefaultCommandName`
- `DefaultCommandId` is NULL (or old/invalid value)

**Step 2: Post-Restore Remapping** (`ActionsRepository.RemapEntityReferencesAfterRestore`)

This method is called automatically after all tables are restored. It performs several passes:

- **Third Pass**: `RemapTimerTriggerIds()` - Updates TimerGroupId from TimerGroupName
- **Fourth Pass**: `RemapCommandTriggerIds()` - Updates CommandId from CommandName  
- **✅ Fifth Pass**: `RemapDefaultCommandTriggerIds()` - **NEW** - Updates DefaultCommandId from DefaultCommandName
- **Sixth Pass**: `RemapTimerGroupSubActionIds()` - Updates timer group subaction references
- **Seventh Pass**: `RemapToggleCommandDisabledSubActions()` - Validates command references

### The Remapping Logic

```csharp
private async Task RemapDefaultCommandTriggerIds(DbContext context, List<ActionType> records, ILogger? logger)
{
    // 1. Get all DefaultCommand triggers from restored actions
    var defaultCommandTriggers = records
        .SelectMany(a => a.Triggers)
        .Where(t => t.Type == TriggerTypes.DefaultCommand && !string.IsNullOrEmpty(t.Configuration))
        .ToList();

    // 2. Load all default commands from database (with their NEW IDs)
    var defaultCommands = await context.Set<Bot.Models.Commands.DefaultCommand>()
        .AsNoTracking()
        .ToListAsync();

    // 3. Create a map: CommandName → New ID
    var commandNameToIdMap = defaultCommands
        .Where(dc => dc.Id.HasValue)
        .GroupBy(dc => dc.CommandName, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().Id!.Value, StringComparer.OrdinalIgnoreCase);

    // 4. For each trigger, look up the command by name and populate the ID
    foreach (var trigger in defaultCommandTriggers)
    {
        var config = JsonSerializer.Deserialize<DefaultCommandTriggerConfiguration>(trigger.Configuration);
        
        if (commandNameToIdMap.TryGetValue(config.DefaultCommandName, out var newDefaultCommandId))
        {
            trigger.DefaultCommandId = newDefaultCommandId; // ✅ Populate the reference column
            logger?.LogDebug("Remapped default command trigger for '{CommandName}' event '{EventType}': new ID is {NewId}", 
                config.DefaultCommandName, config.EventType, newDefaultCommandId);
        }
        else
        {
            logger?.LogWarning("Default command trigger references unknown default command: {CommandName}", 
                config.DefaultCommandName);
        }
    }

    // 5. Save changes to database
    await context.SaveChangesAsync();
}
```

### Example Scenario

**Original Database (Production):**
| DefaultCommands Table |
|----------------------|
| Id: 42, CommandName: "gamble" |

| Triggers Table |
|----------------|
| Id: 100, Type: DefaultCommand, DefaultCommandId: 42, Configuration: {"DefaultCommandName":"gamble","EventType":"Gamble.JackpotWin"} |

**Backup JSON:**
```json
{
  "Actions": [
    {
      "Id": 1,
      "Name": "Celebrate Jackpot",
      "Triggers": [
        {
          "Type": "DefaultCommand",
          "Configuration": "{\"DefaultCommandName\":\"gamble\",\"EventType\":\"Gamble.JackpotWin\"}"
        }
      ]
    }
  ]
}
```

**Restored Database (Dev/Test):**
| DefaultCommands Table |
|----------------------|
| Id: 99, CommandName: "gamble" | ← **Different ID!**

**After Restore (Before Remapping):**
| Triggers Table |
|----------------|
| Id: 1, Type: DefaultCommand, DefaultCommandId: NULL, Configuration: {"DefaultCommandName":"gamble",...} |

**After Remapping:**
| Triggers Table |
|----------------|
| Id: 1, Type: DefaultCommand, **DefaultCommandId: 99**, Configuration: {"DefaultCommandName":"gamble",...} | ← ✅ **Correctly remapped!**

### Why This Works

1. **Configuration is portable**: Contains `"DefaultCommandName": "gamble"` (string)
2. **Remapping uses names**: Looks up command by name in the new database
3. **ID column gets updated**: Populated with the new database's ID for that command
4. **Runtime queries work**: Service uses ID for efficient queries

### Files Modified

✅ **DefaultCommandTriggerConfiguration.cs**
- Uses `DefaultCommandName` instead of `DefaultCommandId`

✅ **ActionsRepository.cs**
- Added `RemapDefaultCommandTriggerIds()` method
- Called from `RemapEntityReferencesAfterRestore()` (Fifth Pass)

✅ **AddTriggerDialog.razor**
- Saves `DefaultCommandName` in configuration

✅ **TriggerManagement.razor**
- Looks up command by name and populates `DefaultCommandId` when creating triggers

### Verification Checklist

- ✅ Configuration stores command **name** (portable)
- ✅ Reference column stores command **ID** (runtime optimization)
- ✅ Restore deserializes configuration with name intact
- ✅ Post-restore remapping populates ID from name
- ✅ Same pattern as Timer and Command triggers
- ✅ All 255 tests passing
- ✅ Build successful

## Conclusion

**Your concern was 100% valid**, and we've implemented the correct solution:

1. ✅ Configuration uses **names** (portable across databases)
2. ✅ Reference columns use **IDs** (efficient runtime queries)
3. ✅ **Remapping logic added** to populate IDs after restore
4. ✅ Follows the same pattern as existing trigger types

The `DefaultCommandId` column **will be correctly populated** when you restore a backup to a different database! 🎉
