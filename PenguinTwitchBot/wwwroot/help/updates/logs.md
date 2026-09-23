# Live Logs & Diagnostics

The [Updates & Logs](/settings/updates) view embeds a real-time console log viewer to monitor background operations, chat message processing, and internal errors.

---

### Using the Log Viewer

- **Real-Time Stream**: The viewer captures runtime log output from ASP.NET Core and bot service workers.
- **Log Levels**:
  - **Info** (Normal): System startup, scheduled task executions, Twitch chat connects.
  - **Warning** (Yellow): Throttled API calls, non-critical sub-action validation warnings.
  - **Error** (Red): Unhandled exceptions, failed external HTTP requests, database connection faults.
- **Pause & Resume**: Click the **Pause** icon to temporarily freeze log scrolling while reading a traceback. Click **Play** to resume following the live stream.
- **Clear Logs**: Click the trash icon to clear the in-memory log buffer in the browser view.

