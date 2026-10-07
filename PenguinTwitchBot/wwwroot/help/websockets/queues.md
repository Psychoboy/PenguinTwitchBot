# WebSocket Queue Management

The WebSocket subsystem delivers real-time events, stream alerts, overlay updates, and chat animations from the bot's backend to connected OBS browser sources and web clients.

---

### Global vs. Per-Socket Queues

To guarantee that a slow or lagging browser source in OBS does not stall other overlays or deplete server memory, message dispatching uses a two-tier queue architecture:

1. **Global Queue**:
   - Holds all broadcast events originating from the bot (alerts, chat messages, timer ticks, sound commands).
   - If the global queue reaches capacity, older unread events may be dropped to protect system responsiveness.
2. **Per-Socket Queue**:
   - Each connected browser source or dashboard client maintains its own outbound buffer.
   - If a specific OBS browser source stutters or becomes unresponsive, only its individual queue fills up without affecting other overlays.

---

### Queue Capacities

- **Global Queue Capacity**: Maximum number of queued messages waiting to be distributed across all topics (default: `1,000`).
- **Per-Websocket Queue Capacity**: Maximum buffered messages for any single client (default: `250`).

> [!NOTE]
> Applying updated queue capacities closes and removes active WebSocket connections without restarting the server. Connected browser sources and client overlays will reconnect automatically following each client's reconnect retry policy (e.g. `ws-client.js` exponential backoff).

---

### Chat Commands & Control

Streamers can control alert distribution and websocket broadcasting directly from Twitch chat:

| Command | Rank | Description |
| :--- | :--- | :--- |
| `!pausealerts` | Streamer | Pauses all alert processing and stops dispatching alert events over WebSocket. |
| `!resumealerts` | Streamer | Resumes normal alert dispatching and queue processing. |

