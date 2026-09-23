# OBS Integration & Sub-Actions

Integrate PenguinTwitchBot with OBS Studio via the OBS WebSocket protocol to trigger scene changes, toggle sources, and update browser sources automatically.

---

### Connecting to OBS Studio

1. In OBS Studio, navigate to **Tools > WebSocket Server Settings**.
2. Check **Enable WebSocket server**, note the port (default `4455`), and set or copy the server password.
3. In PenguinTwitchBot, configure your OBS connection under **Bot Settings > Integrations**.

---

### Supported OBS Sub-Actions

Use these sub-actions in your custom Actions to automate your broadcast:

- **`ObsSetBrowserSourceUrl`**: Dynamically switches the URL of an OBS browser source (useful for displaying web pages, leaderboards, or custom widgets).
- **`ObsSetScene`**: Changes the active program scene in OBS (e.g. switch to *Gameplay* when stream starts).
- **`ObsSetSourceVisibility`**: Toggles or sets the visibility (show/hide) of any source within a scene.
- **`ObsSetSourceMuteState`**: Mutes or unmutes specific audio inputs/microphones.
- **`ObsSetSceneFilterState`**: Enables or disables visual filters applied to an OBS scene.
- **`ObsSetSourceFilterState`**: Enables or disables visual or audio filters on an individual source.
- **`ObsSetText`**: Updates the text content of GDI+ or FreeType2 text sources in OBS.
- **`ObsSetMediaState`**: Controls media sources (play, pause, restart, stop).
- **`ObsTriggerHotkey`**: Programmatically fires an OBS hotkey combination.

