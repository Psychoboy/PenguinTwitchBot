# Automation, Sub-Actions, Triggers & Test Modes

## Credits Action Sub-Actions

Add Credits automation directly inside Commands, Channel Point Rewards, Stream Events, or Timers under the **Credits** sub-action category:

- **Credits: Play / Roll** — Trigger a profile's credits roll with optional runtime overrides for time window mode, lookback minutes, or test sample mode (`off`, `fake`, `db`).
- **Credits: Stop / Hide** — Stop and fade out currently rolling credits.
- **Credits: Pause / Resume** — Toggle pause/resume on an active roll.
- **Credits: Mark Start Checkpoint** — Stamp "now" as a manual checkpoint for multi-day subathons or segmented streams.
- **Credits: Add Manual Entry** — Push a dynamic entry into credits via variables (for example `%user%` and `%input%` from a `!shoutout` or `!creditme` command).

## Credits Completed Trigger

When a live roll finishes (excluding looping rolls and designer previews), the overlay reports completion so you can chain Actions automatically (for example: switch OBS scene, post an end-of-stream Discord/chat message, or trigger fireworks) using the **Credits Completed** trigger.

Available trigger variables:
- `%creditsProfile%` — Name of the profile that finished rolling
- `%totalEntries%` — Total entries rendered in that roll
- `%totalSections%` — Number of visible non-empty sections

## Test Modes (`Fake` vs `DB Viewers` vs `Live`)

Test your timing, CSS, section grouping, and audio without polluting recorded live data:
- **Fake Sample Data (`fake`)**: Generates deterministic sample entries across all enabled sections (matching your exact formatting templates and grouping).
- **Real DB Viewers (`db`)**: Pulls real non-bot viewers from your PenguinTwitchBot database (including real Twitch avatars!) so you can preview how real community usernames and profile pictures look.
- **Live Captured Events (`off`)**: Uses real recorded stream events within the configured Time Window.
