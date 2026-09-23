# Default Commands Overview

PenguinTwitchBot includes built-in commands powering Twitch moderation, loyalty points, mini-games, giveaways, music, and interactive overlays.

## Core Default Commands

| Module | Default Command | Default Rank | Purpose |
| :--- | :--- | :--- | :--- |
| **Points** | `!points`, `!addpoints`, `!removepoints`, `!setpoints`, `!addactivepoints` | Viewer / Streamer | Check, add, deduct, set balances, or award active chatters. |
| **Games** | `!gamble`, `!slots`, `!heist`, `!ffa`, `!roulette`, `!defuse`, `!modspam` | Viewer | Interactive chat minigames wagering channel points. |
| **Loyalty** | `!claim` | Viewer | Once-per-stream attendance bonus claim. |
| **Giveaways** | `!enter`, `!entries`, `!open`, `!close`, `!draw`, `!resetdraw`, `!setprize` | Viewer / Streamer | Enter raffles, inspect tickets, manage drawings from chat. |
| **Wheel** | `!showwheel`, `!hidewheel`, `!spinwheel`, `!join`, `!opennamewheel`, `!shownamewheel`, `!closenamewheel`, `!spinnamewheel` | Viewer / Streamer | Display, spin, and manage prize wheels and viewer name wheels. |
| **Shoutout** | `!so`, `!shoutout` | VIP | Shouts out fellow creators with chat links, API shoutout, and clips. |
| **Music** | `!song`, `!sq`, `!priority`, `!steal`, `!veto`, `!pause`, `!resume` | Viewer / Mod | Song requests, playlist manipulation, and playback controls. |

## Overriding Command Triggers

Every default command can be renamed without altering its internal logic:
- Click the **Edit** icon on any command row in [Default Commands](/commands/default).
- Change the **Custom Command Trigger** field to whatever command name you prefer (e.g. changing `points` to `coins` or `ffa` to `arena`).
- Viewers will immediately use the new trigger word in Twitch chat!

