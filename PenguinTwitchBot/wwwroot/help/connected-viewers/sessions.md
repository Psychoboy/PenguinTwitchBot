# Live Connected Browser Sessions

The [Connected Viewers](/community/connected-viewers) page monitors real-time SignalR WebSocket circuits established between web browsers and the PenguinTwitchBot dashboard.

## Active Circuit Tracking

The Active Sessions table displays:
- **User**: Username and avatar of authenticated users (Streamer, Editor, Moderator), or "Guest / Anonymous" for public overlay and song request views.
- **Current / Last Page**: The active URI route currently open in that browser tab (e.g. `/overlay/editor`, `/musicplayer`, `/settings/app`).
- **Last Active**: Timestamp indicating recent interaction or heartbeat signals from the client browser.
- **IP Address**: Client connection IP address.
- **Status Indicator**: Green dot indicating an open, responsive circuit.

