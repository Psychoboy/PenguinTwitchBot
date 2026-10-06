# Counters Overview

Counters allow you to create, track, and manage custom numeric tallies during your stream. They are ideal for death counters, win/loss trackers, drink/water reminders, swear jars, or any on-stream goal.

---

## Key Properties

When creating or editing a counter, configure the following settings:

- **Name**: The unique programmatic identifier for the counter (alphanumeric and underscores, e.g., `deaths`, `wins`). Used in chat commands, sub-actions, and variables.
- **Display Name**: A user-friendly title shown on stream overlays and in chat responses (e.g., `Death Counter`, `Boss Victories`).
- **Initial Value**: The starting value of the counter when created or reset (default is `0`).
- **Min / Max Value**: Optional boundary clamping. If set, the counter cannot be decreased below the minimum or increased above the maximum.
- **Step**: The default quantity added or subtracted when incrementing or decrementing without a specific amount (default is `1`).

---

## Permission Ranks

To prevent unauthorized changes in chat, each counter operation has an independent minimum user rank requirement:

- **Increment Rank**: Minimum rank allowed to increase the counter (default: `Moderator`).
- **Decrement Rank**: Minimum rank allowed to decrease the counter (default: `Moderator`).
- **Set Rank**: Minimum rank allowed to assign an exact number (default: `Moderator`).
- **Reset Rank**: Minimum rank allowed to reset the counter back to its initial value (default: `Streamer`).

Supported ranks include: `Everyone`, `Regular`, `VIP`, `Subscriber`, `Moderator`, `SuperModerator`, and `Streamer`.

---

## Automatic Action & Command Generation

When creating or editing a counter, you can enable **Create Action & Command**:
- Automatically creates an Action named after your counter.
- Adds a **Multi-Counter** sub-action configured with `CommandArgs` to process chat inputs dynamically.
- Adds an optional chat announcement sub-action displaying the updated count.
- Generates a chat command (e.g., `!countername`) bound directly to the new action.
