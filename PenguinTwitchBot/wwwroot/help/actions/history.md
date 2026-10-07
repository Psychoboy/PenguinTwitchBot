# Action Execution History

The Action History viewer provides real-time logging, diagnostics, and debugging for all actions running in Penguin Twitch Bot.

---

## Live Monitoring & SignalR

Action events are pushed live to the browser interface via SignalR:
- **Execution States**: Clearly labeled chips display whether an action is `Running`, `Completed`, `Failed`, or `Canceled`.
- **Timing Diagnostics**: View exact timestamps for when an action was enqueued, started, and finished, along with total runtime duration.
- **Queue Breakdown**: See which queue handled each action invocation.

---

## Sub-Action Inspection & Variables

Clicking an action log entry reveals comprehensive diagnostic details:
- **Sub-Action Step Breakdown**: Inspect each sub-action step, its individual execution state, and exact duration.
- **Error Messages & Stack Traces**: If an action fails (e.g., an external API call times out or an invalid parameter is provided), error details appear directly on the step.
- **Variables Before & After**: Inspect the dictionary of variables present when the action began and any modifications made during execution.

---

## Performance & Memory Management

- **Pause Live Updates**: Click the pause button in the toolbar to freeze the log stream while inspecting an active chain without rows shifting under your cursor.
- **Memory Retention Limit**: To safeguard system memory during long broadcasts, the execution logger keeps a rolling buffer of recent logs in memory (configurable up to the system retention threshold).
- **Clear Logs**: Purge the in-memory history at any time using the trash icon.
