# Quick Start Guide - DefaultCommand Triggers

## Problem Solved ✅

The service lifetime error has been fixed! The issue was that `IDefaultCommandTriggerService` was registered as **Scoped**, but was being injected into **Singleton** services (Gamble and Defuse commands).

**Solution:** Changed the service registration in `BotCommandsRegistry.cs` from `AddScoped` to `AddSingleton`. This is safe because the service already uses `IServiceScopeFactory` internally for all database operations.

## Next Steps

### 1. Apply the Migration

Run this command from your workspace root:

```powershell
dotnet ef database update --project DotNetTwitchBot
```

This will add the `DefaultCommandId` column to your `Triggers` table.

### 2. Test the UI

1. Start your application
2. Navigate to **Actions → Manage Actions**
3. Select or create an action
4. Click **"Add Trigger"**
5. You should now see **"Default Command"** as an option (with a green Functions icon)
6. Try creating a trigger:
   - Select "gamble" as the default command
   - Select "Jackpot Win" as the event
   - Click "Add Trigger"

### 3. Test the Functionality

To test that the trigger fires correctly:

1. Create an action with some sub-actions (e.g., send a chat message)
2. Add a DefaultCommand trigger for "gamble" → "Jackpot Win"
3. In chat, use the `!gamble` command and try to win the jackpot
4. The action should execute when you hit the jackpot!

## What Was Changed

### Backend (Already Complete)
- ✅ New trigger type enum value
- ✅ Configuration class for DefaultCommand triggers
- ✅ Event type constants (Gamble.JackpotWin, etc.)
- ✅ Service to handle trigger execution
- ✅ Updated Gamble and Defuse commands
- ✅ Database migration
- ✅ Repository methods
- ✅ Service registration (fixed to Singleton)
- ✅ All tests passing (255/255)

### UI (Just Completed)
- ✅ Added DefaultCommand option to trigger type selector
- ✅ Default command dropdown (auto-complete)
- ✅ Event type dropdown (filtered by command)
- ✅ Auto-generated trigger names
- ✅ Display logic in trigger management
- ✅ Green color coding for easy identification
- ✅ Event summary display

## Available Features

### Current Default Commands with Triggers:

**Gamble (!gamble)**
- 🎰 Jackpot Win - Triggers when someone hits the jackpot
  - Variables: `%JackpotAmount%`, `%WinAmount%`, `%TotalWinnings%`, `%RolledValue%`
- 💰 Win - Triggers when someone wins (not jackpot)
  - Variables: `%WinAmount%`, `%RolledValue%`
- 💸 Lose - Triggers when someone loses
  - Variables: `%LoseAmount%`, `%RolledValue%`

**Defuse (!defuse)**
- ✅ Success - Triggers when bomb is defused
  - Variables: `%WinAmount%`, `%ChosenWire%`, `%CorrectWire%`
- 💣 Failure - Triggers when bomb explodes
  - Variables: `%LoseAmount%`, `%ChosenWire%`, `%CorrectWire%`

## Example Use Case

### Fireworks on Jackpot Win

**Before:** Hardcoded HTTP call to launch fireworks in Gamble command

**Now:**
1. Create an action called "Jackpot Celebration"
2. Add sub-actions:
   - Send chat message: "🎉 %displayname% just won the jackpot of %JackpotAmount% points! 🎉"
   - Execute HTTP request to launch fireworks
   - Play celebration sound
3. Add a DefaultCommand trigger:
   - Type: Default Command
   - Command: gamble
   - Event: Jackpot Win
4. Done! Now it's configurable through the UI

## Documentation

- 📄 **IMPLEMENTATION_DEFAULT_COMMAND_TRIGGERS.md** - Full backend implementation details
- 📄 **IMPLEMENTATION_UI_UPDATES.md** - UI implementation guide
- 📄 **This file** - Quick start guide

## Troubleshooting

If the migration fails, check:
1. Database connection is working
2. No other pending migrations
3. EF Core tools are installed: `dotnet tool install --global dotnet-ef`

If triggers don't fire:
1. Check the trigger is enabled in the UI
2. Verify the DefaultCommandId is set correctly in the database
3. Check the EventType matches exactly (case-sensitive)

## Build Status

✅ Build successful
✅ All 255 tests passing
✅ No compilation errors
✅ Service lifetime issues resolved

You're ready to go! 🚀
