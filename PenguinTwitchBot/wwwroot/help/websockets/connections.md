# Connected Clients & Topics

The **Current Status** table in [Websocket Queues](/settings/websockets) provides real-time visibility into all active clients connected to the bot's WebSocket server on `/ws`.

---

## Client Connection Details

| Column | Description |
| :--- | :--- |
| **Display Name** | Identifies the connected client (e.g. `Overlay: Default`, browser source label). |
| **Connection ID** | Unique GUID assigned to the active socket session. |
| **Topics** | Specific event channels the client is subscribed to. Displays `all` if no filter was requested. |
| **State** | WebSocket lifecycle status (`Open`, `Connecting`, `Closing`, `Closed`). |
| **Sent** | Total count of event messages successfully dispatched to this client. |
| **Dropped** | Messages dropped if the client's per-socket bounded channel reaches capacity. |
| **Peak Pending** | Highest recorded queue depth reached by this connection relative to capacity. |
| **Action** | Disconnect and reconnect an individual client session. |

---

## Topic Routing Reference

Clients connect to the single `/ws` endpoint (e.g. `ws://localhost:5000/ws` or `ws://localhost:5000/ws?topics=alerts,overlay`). If no topic query is supplied, the client receives all broadcast messages.

The bot categorizes messages into the following official topics:

- **`chat`**: Live chat messages, message deletions, and user timeouts/bans for on-screen chat box widgets.
- **`alerts`**: Standard channel alerts including follows, subs, resubs, gift bombs, raids, cheers, and custom alerts.
- **`clips`**: Shoutout clip player triggers and stop playback signals.
- **`fishing`**: Real-time animations, casts, snaps, and catch notifications for the fishing mini-game overlay.
- **`wheel`**: Wheel spin commands and prize outcome animations.
- **`overlay`**: Layout reloading, widget property updates, and configuration synchronization dispatched by the Overlay Editor.
- **`events`**: Structured bot lifecycle and trigger events (`WsEvent`).
