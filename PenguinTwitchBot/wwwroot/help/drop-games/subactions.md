# Triggers, Sub-Actions & Automation

All drop minigames integrate directly into the bot's Action & Trigger system, allowing you to reward winners, trigger celebratory alerts, play custom sound effects, or switch OBS scenes automatically.

## Built-In Event Triggers

You can attach automated actions to any of the following triggers under **Manage Actions**:

### Parachute Drop Triggers
- **Parachute Drop Win**: Fires when a viewer lands on the target platform.
- **Parachute Drop Lose**: Fires when a viewer lands outside the target platform in the water/ground.
- **Parachute Bullseye**: Fires when a viewer achieves a direct bullseye landing.
- **Parachute Rocket Kill**: Fires when a parachute canopy is destroyed mid-air by a rocket.
- **Parachute Start**: Fires when a Parachute Drop round begins.
- **Parachute End**: Fires when a Parachute Drop round concludes.

### Plinko Bounce Triggers
- **Plinko Drop Win**: Fires when a viewer's token lands in a bucket configured with `IsWin = true`.
- **Plinko Drop Lose**: Fires when a token lands in a non-winning bucket.
- **Plinko Start**: Fires when a Plinko round begins.
- **Plinko End**: Fires when a Plinko round concludes.

### Balloon Ascent Triggers
- **Balloon Ascent Win**: Fires when a viewer successfully docks on the top cloud platform.
- **Balloon Ascent Pop**: Fires when a viewer's balloon is popped by a sharp hazard mid-air.
- **Balloon Bullseye**: Fires when a viewer hits the center golden bullseye of the cloud platform.
- **Balloon Start**: Fires when a Balloon Ascent round begins.
- **Balloon End**: Fires when a Balloon Ascent round concludes.

---

## Trigger Variables Reference

When a minigame trigger executes, the following runtime variables are available for your sub-actions:

| Variable | Description |
| :--- | :--- |
| `%User%` | Viewer's username login (e.g. `coolstreamer`) |
| `%DisplayName%` | Viewer's friendly display name |
| `%Score%` | Points earned from the drop (0–100 or Plinko bucket value) |
| `%IsBullseye%` | `True` if landed within the center bullseye |
| `%IsWin%` | `True` if the drop reached a winning target zone or winning bucket |
| `%Theme%` | Active seasonal or custom theme name |
| `%Game%` | Minigame type (`ParachuteDrop`, `Plinko`, `BalloonAscent`) |
| `%DropletHits%` | Number of droplet impacts on a parachuter |
| `%RocketHits%` | Number of rocket impacts on a parachuter |
| `%ParachuteDestroyed%` | `True` if the parachute canopy was destroyed |
| `%Attacker%` / `%TriggeredBy%` | User or automation that initiated the hazard or rocket strike |
| `%BucketLabel%` | Name of the Plinko bucket landed in |
| `%BucketIndex%` | Zero-based index of the Plinko bucket landed in |
| `%IsPopped%` | `True` if the balloon was popped during ascent |
| `%PoppedBy%` | Name of the attacker or hazard that popped the balloon |
| `%HazardType%` | Archetype of hazard that struck (`Nudge`, `HeavyPush`, `WindGust`, `DartPop`) |
| `%TotalDrops%` | Total participants in the concluded round (for round end triggers) |
| `%Duration%` | Session duration in seconds |

---

## Drop Minigame Sub-Actions

You can trigger drops, start sessions, and launch hazards from Channel Point rewards, timers, or chat commands:

- **Start Drop Game Session**: Initiates a timed round for **Parachute Drop**, **Plinko Bounce**, or **Balloon Ascent** with custom duration and theme.
- **Stop Drop Game Session**: Concludes the active round, awards points to the leader, and advances the session queue.
- **Parachute Drop**: Launches a parachute drop for a viewer with custom color and emote.
- **Parachute Droplets**: Spawns an obstacle droplet barrage (count, size, speed, pattern, and custom image).
- **Parachute Rockets**: Launches anti-air rockets at active parachuters (single or multi-target patterns).
- **Plinko Drop**: Drops a Plinko token for a viewer with custom color and emote.
- **Balloon Ascent**: Launches a hot air balloon for a viewer with custom emote and theme.
- **Balloon Hazards**: Spawns interactive balloon hazards (Nudge, Heavy Push, Wind Gust, or Dart Pop) targeting specific players or random balloons.
