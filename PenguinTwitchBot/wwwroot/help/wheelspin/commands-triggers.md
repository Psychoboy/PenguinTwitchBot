# Wheel Commands, Triggers & Sub-Actions

The Wheel system can be controlled entirely via chat commands or automated through the Actions engine and stream deck buttons.

## Registered Chat Commands

| Command | Minimum Rank | Description |
| :--- | :--- | :--- |
| `!showwheel <id>` | Streamer | Displays the prize wheel with the specified numeric ID on the stream overlay (e.g. `!showwheel 1`). |
| `!hidewheel` | Streamer | Hides the currently visible wheel from the stream overlay. |
| `!spinwheel` | Streamer | Triggers the active wheel on stream to spin. |
| `!join` | Viewer | Enters the viewer's username into the currently open Viewer Name Wheel. |
| `!opennamewheel` | Streamer | Opens entry registration for the Viewer Name Wheel. |
| `!shownamewheel` | Streamer | Displays the Viewer Name Wheel on the overlay. |
| `!closenamewheel` | Streamer | Closes entry submissions for the Viewer Name Wheel. |
| `!spinnamewheel` | Streamer | Spins the Viewer Name Wheel to pick a winning chatter. |

## Actions & Automation Integration

- **Triggering via Sub-Actions**: Wheel operations can be triggered programmatically from automated actions using `ExecuteDefaultCommand` or custom websocket events.
- **Triggers on Completion (`WheelSpinResult`)**:
  When a wheel completes its spin, the bot fires the `DefaultCommandEventTypes.WheelSpinResult` trigger event:
  - `%WheelSpinResult%` (or `%WinningLabel%`): The winning slice label text.
  - `%WinningMessage%`: The winning announcement text with the `{label}` token resolved.
  - `%WheelName%`: The name of the wheel that was spun.
  - `%WinningIndex%`: The numerical index of the selected slice.
  - `%IsNameWheel%`: `"true"` if spun from a viewer name wheel, otherwise `"false"`.
- **Chained Sub-Actions**:
  Actions triggered by a wheel spin result can play victory sound effects (`PlaySound`), trigger celebratory fireworks (`Alert`), award bonus points to the winner (`GiftPoints`), or send a highlighted chat message (`SendMessage`).

