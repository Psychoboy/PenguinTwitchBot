# Fishing Sub-Actions & Setup

In Penguin Twitch Bot, fishing is powered entirely through the **Actions & Sub-Actions** framework.

---

## Important Architecture Note

> [!IMPORTANT]
> The fishing mini-game is triggered **only via sub-actions**. There are no hardcoded built-in chat command listeners for fishing; instead, chat commands (such as `!fish`) or Twitch channel point redemptions execute action pipelines containing fishing sub-actions.

---

## Sample Actions Library

To get started quickly without manually assembling complex action pipelines:
1. Open the [Manage Actions](/actions/manage) page.
2. Click **Library** (or **Import**) in the top toolbar.
3. Select and import the prebuilt **Fishing Sample Actions**.
4. The bot will automatically create ready-to-use actions for `!fish`, inventory checks, and tournament controls, bound to your preferred triggers.

---

## Available Fishing Sub-Actions

When building custom workflows, you can utilize the following dedicated sub-actions:

- **Fishing (Cast & Catch)**: Performs a virtual cast for the triggering viewer. Reads their equipped gear, applies rarity, star, and weight boosts, selects a caught fish, awards gold, and updates tournament standings.
- **Fishing Give Item To Player**: Grants specific fishing rods, tackle, bait, or lures directly to a viewer (great for giveaway prizes, quest rewards, or subscriber loyalty bonuses).
- **Fishing Modify**: Directly alters a player's gold, energy, or equipment inventory from an automated stream event.
- **Fishing Tournament Start**: Automatically starts a scheduled or event-driven fishing tournament.
- **Fishing Tournament End**: Concludes the active tournament, computes final standings, and distributes point/gold reward brackets to top anglers.
- **Fishing Tournament Eligible Catch**: Verifies whether an incoming catch meets tournament rules and qualifies for leaderboards.

