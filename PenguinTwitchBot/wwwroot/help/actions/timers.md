# Timers

Timers automate recurring chat announcements, periodic reminders, or scheduled stream events throughout your broadcast.

---

## How Timers Work

Rather than running commands blindly on a fixed clock, Penguin Twitch Bot evaluates both time intervals and chat activity:

- **Minimum & Maximum Interval (Seconds)**: Timers define an interval range in **seconds** (e.g. `300` to `600` seconds). If a range is specified, the bot randomizes the execution interval between the minimum and maximum seconds.
- **Minimum Chat Messages**: The number of chat lines that must occur since the last execution before the timer will fire again. This prevents the bot from spamming repetitive messages into a quiet chat when you are AFK or before stream starts.
- **Online Only**: Ensures timers only trigger while the channel is live.
- **Repeat & Shuffle**: Configure whether the timer repeats continuously or runs once. If multiple actions are assigned to a timer, enabling **Shuffle** randomly selects from the associated actions instead of cycling sequentially.

---

## Associating Actions

Each timer can link to one or more actions:
- Because timers execute actions, ticks can do much more than post chat text: they can trigger sound effects, change lighting, swap OBS camera angles, or update overlay elements.

---

## Dynamic Control via Sub-Actions

You can automate timer states directly from your actions using the **Timer Group Set Enabled State** sub-action:
- Temporarily disable promotional timers during sponsored segments or competitive matches.
- Enable high-frequency hype timers when reaching follower or sub goals.
- Create streamer hotkeys or mod commands (such as `!pausetimers` and `!resumetimers`) to toggle timers without opening the web dashboard.
