# Triggers, Sub-Actions & Automation

Parachute Drop and Plinko Bounce integrate directly into the bot's Action & Trigger system, allowing you to reward winners, play audio stings, or trigger OBS scene changes when players land.

## Event Triggers

You can attach actions to the following built-in triggers under **Manage Actions**:
- **Parachute Drop Win**: Fires when a viewer lands on the target platform (bullseye or ring).
- **Plinko Drop Win**: Fires when a viewer's token lands in a bucket configured with `IsWin = true`.

### Available Trigger Variables

When a win trigger executes, the following runtime variables are available for sub-actions:

| Variable | Description |
| :--- | :--- |
| `%User%` | Viewer's username login (e.g. `coolstreamer`) |
| `%DisplayName%` | Viewer's friendly display name |
| `%Score%` | Points earned from the drop (Parachute score or Plinko bucket value) |
| `%IsBullseye%` | `True` if landed within the center bullseye (or highest-scoring winning bucket) |
| `%DropletHits%` | Number of interactive droplets that collided with the parachutist mid-air |
| `%Theme%` | Active seasonal or custom theme name |
| `%BucketLabel%` | Name of the Plinko bucket landed in |
| `%BucketIndex%` | Zero-based index of the Plinko bucket landed in |
| `%IsWin%` | `True` if the drop landed in a winning target zone or winning bucket |

## Drop Sub-Actions

You can trigger drops, sessions, and obstacles from Channel Points, timers, or commands using these sub-actions:

- **Start Drop Game Session**: Initiates a timed round (Parachute Drop or Plinko Bounce) with a countdown window for group participation.
- **Stop Drop Game Session**: Forcibly concludes an active round, awards points to the current leader, and advances the session queue.
- **Parachute Drop**: Initiates a drop for a viewer with custom parachute color and emote.
- **Parachute Droplets**: Spawns an obstacle barrage (count, size, speed, pattern, and custom image).
- **Plinko Drop**: Drops a Plinko token for a viewer with custom color and emote.

