# Permissions, Cooldowns & Costs

Every command in PenguinTwitchBot can be customized with strict cooldown intervals, access permissions, and point entry fees.

## Cooldown Controls

In the [Default Commands](/commands/default) edit view:
- **User Cooldown (Min / Max)**: Enforces a delay in seconds before the same chatter can run the command again. If Max is configured greater than Min, the bot randomizes the cooldown duration between the two values.
- **Global Cooldown (Min / Max)**: Enforces a channel-wide delay before any chatter can trigger the command again.
- **Announce Cooldown**: When checked, typing the command while on cooldown triggers a chat response notifying the user how many seconds remain.

## Permissions & Rank Hierarchy

- **Minimum Rank**: Restricts command execution to a minimum rank tier:
  - `Viewer` -> All chatters
  - `Vip` -> Twitch VIPs, Mods, Editors, Broadcaster
  - `Moderator` -> Twitch Mods, Editors, Broadcaster
  - `Editor` -> Bot Editors, Broadcaster
  - `Streamer` -> Broadcaster only
- **Specific Ranks**: Whitelist specific disjoint rank sets.
- **Specific Users**: Whitelist specific Twitch usernames who are granted access regardless of rank.

## Economy & Point Costs

- **Point Cost**: Require chatters to pay a specified point fee each time they execute the command.
- **Point Type**: Choose which virtual currency is deducted from their account balance upon command execution.

