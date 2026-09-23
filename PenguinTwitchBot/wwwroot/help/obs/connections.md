# OBS WebSocket Connections

Penguin Twitch Bot can connect to one or more instances of **OBS Studio** (v28+) via the native OBS WebSocket v5 protocol to automate scenes, sources, audio, filters, and streaming controls.

---

### Adding an OBS Connection

1. Open **OBS Studio**:
   - Go to *Tools > WebSocket Server Settings*.
   - Ensure **Enable WebSocket server** is checked.
   - Note the **Server Port** (default: `4455`) and copy the **Server Password**.
2. In Penguin Twitch Bot, open [OBS Connections](/obs/connections):
   - Click **Add Connection**.
   - **Name**: A descriptive label (e.g. `Gaming PC OBS`, `Streaming Rig`, `Secondary Camera`).
   - **Server URL**: WebSocket address (e.g. `ws://localhost:4455` or `ws://192.168.1.100:4455`).
   - **Password**: Enter the password configured in OBS Studio.
   - Check **Enabled**.
   - Click **Save**.

---

### Multi-OBS Support

The bot supports configuring multiple OBS connections simultaneously. This is ideal for dual-PC streaming setups (one OBS for gameplay capture and one OBS for streaming/encoding). Each OBS Sub-Action can target a specific connection by name.

