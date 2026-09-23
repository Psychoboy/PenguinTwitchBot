# Point Chat Commands

Each configured point type automatically receives a set of dedicated chat commands allowing viewers and streamers to inspect, award, deduct, and manage balances.

## Default Commands for Points

For the default point type, the standard registered commands are:

| Command | Type | Minimum Rank | Description |
| :--- | :--- | :--- | :--- |
| `!getpoints` (or `!points`) | `Get` | Viewer | View the sender's current point balance (or check another viewer: `!points @username`). |
| `!addpoints <user> <amount>` | `Add` | Streamer | Add specified points to a user's balance. |
| `!removepoints <user> <amount>` | `Remove` | Streamer | Deduct specified points from a user's balance. |
| `!setpoints <user> <amount>` | `Set` | Streamer | Explicitly set a user's balance to an exact amount. |
| `!addactivepoints <amount>` | `AddActive` | Streamer | Distribute points to all currently active chatters. |

## Commands for Custom Currencies

When you create custom point types (such as "Tickets" or "Tokens"):
- A unique command set is generated based on the currency name or assigned in the commands configuration.
- Custom commands can be expanded directly in the **Point Types** table.
- Each command respects standard command rules:
  - **Cooldowns**: Can be assigned global and user cooldowns in the Command Cooldowns view.
  - **Permissions**: Minimum ranks (Viewer, VIP, Moderator, Editor, Streamer) can be adjusted to restrict administrative commands.
  - **Aliases**: Shorter aliases (such as `!tickets` or `!p`) can be created in the Aliases manager.

