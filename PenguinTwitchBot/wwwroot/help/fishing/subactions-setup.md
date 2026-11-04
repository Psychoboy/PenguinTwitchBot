# Fishing Sub-Actions & Setup

In Penguin Twitch Bot, fishing is powered entirely through the **Actions & Sub-Actions** framework.

---

## Important Architecture Note

> [!IMPORTANT]
> The fishing mini-game is triggered **only via sub-actions**. There are no hardcoded built-in chat command listeners for fishing; instead, chat commands (such as `!fish`) or Twitch channel point redemptions execute action pipelines containing fishing sub-actions.

---

## Set Up Fishing Actions

Create fishing workflows from **Manage Actions** using the fishing sub-actions listed below. A basic chat fishing action combines **Fishing** (Cast & Catch) with a **Send Message** sub-action for its result; add tournament or inventory actions separately as needed. User-provided JSON action exports can still be imported from Manage Actions.

---

## Available Fishing Sub-Actions

When building custom workflows, you can utilize the following dedicated sub-actions:

- **Fishing (Cast & Catch)**: Performs a virtual cast for the triggering viewer. Reads their equipped gear, applies rarity, star, and weight boosts, selects a caught fish, awards gold, and updates tournament standings.
- **Fishing Give Item To Player**: Grants specific fishing rods, tackle, bait, or lures directly to a viewer (great for giveaway prizes, quest rewards, or subscriber loyalty bonuses).
- **Fishing Modify**: Directly alters a player's gold, energy, or equipment inventory from an automated stream event.
- **Fishing Tournament Start**: Automatically starts a scheduled or event-driven fishing tournament.
- **Fishing Tournament End**: Concludes the active tournament, computes final standings, and distributes point/gold reward brackets to top anglers.
- **Fishing Tournament Eligible Catch**: Verifies whether an incoming catch meets tournament rules and qualifies for leaderboards.

