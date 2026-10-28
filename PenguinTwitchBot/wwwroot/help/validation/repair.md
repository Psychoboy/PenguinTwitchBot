# Fixing Common Issues

Use the **Fix** button on a row to open the right page, then run the check again to confirm.

---

### A feature is turned off

Open [Bot Settings](/settings/app) and turn the feature back on, or edit the action and remove the trigger or sub-action that needs it. If you turned the feature off on purpose, use **Ignore** on the warning.

### Missing or deleted action, command or timer group

Open the action in [Manage Actions](/actions/manage). Edit the Execute Action (or timer, command or keyword trigger) that points at the missing item, and choose an existing one or remove the step.

### OBS problems

- **No connection selected / connection missing**: edit the sub-action and pick a connection from the list.
- **Connection disabled**: enable it on [OBS Connections](/obs/connections).
- **Not connected**: check that OBS is running, the WebSocket server is enabled in OBS, and the URL and password match. The last error message from OBS is shown in the issue.

### A game or Raid Rewards point type is missing

Open [Game Settings](/settings/games) or [Raid Rewards](/settings/raidrewards) and choose a point type. Until you do, the first point type is used for that game.

### Duplicate commands or keywords

Rename or delete the extras on [Action Commands](/actions/commands) or [Keywords](/actions/keywords). When two share a name, only one can respond.

### Timer groups

On [Timers](/actions/timers), correct the interval values, add an action to a group that has none, or activate a group whose actions should run.

### Issues you do not need to fix

Click the eye icon to **Ignore** it. The issue stops counting as a problem but stays available under **Show ignored**, so you can restore it later.
