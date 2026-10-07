# Live Viewer Name Wheel

The Viewer Wheel allows streamers to draw random active viewers directly on stream by dynamically populating wheel slices with viewer names.

## Viewer Name Wheel Lifecycle

1. **Open Viewer Wheel**: Opens the entry window and clears prior entries. Viewers enter by typing `!join` in Twitch chat.
2. **Show Viewer Wheel**: Displays the dynamically populated name wheel on the stream overlay in real-time as viewers join.
3. **Close Viewer Wheel**: Locks entries, preventing additional chatters from joining the wheel.
4. **Spin Viewer Wheel**: Triggers a randomized spin on the viewer roster, selecting a winner.
5. **Hide Overlay Wheel**: Hides the wheel from OBS once the winner has been celebrated.

## De-duplication & Bot Safety

- Each viewer can only enter once per open session.
- Registered [Known Bots](/community/known-bots) are automatically blocked from entering viewer wheels.

