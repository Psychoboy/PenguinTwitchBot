# Music Sub-Actions & Triggers

Integrate your music player with chat commands, channel points, and automated actions.

---

### Commands & Moderation Controls

- **`!sr <url or title>`**: Allow viewers to request songs by YouTube URL, video ID, or search query.
- **`!song` / `!currentsong`**: Display the currently playing song title and artist in chat.
- **`!veto` / `!skip`**: Streamers and moderators can skip the current track. When configured, vetoed tracks can bypass song cooldowns.
- **`!volume <0-100>`**: Adjust playback volume.

---

### Trigger Integration: `BannedSongRequest`

- When a viewer attempts to request a song that is listed on your **Banned Songs** list, the bot fires the `BannedSongRequest` trigger (`TriggerTypes.BannedSongRequest`).
- You can attach an Action to this trigger with Sub-Actions to:
  - Send a chat message warning the user.
  - Refund channel points (if requested via redemption).
  - Play an alert or sound effect.
- **Available Variables**:
  - `{User}`: Viewer who requested the banned song.
  - `{SongTitle}`: Title of the banned song.
  - `{SongId}`: YouTube Video ID.
  - `{Reason}`: Ban reason specified in your database.

