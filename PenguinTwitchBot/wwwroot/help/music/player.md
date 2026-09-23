# Music Player Guide

The **Music Player** is a full-featured YouTube music playback system built directly into your bot and stream overlays.

---

### Playback Queue vs. Playlist Pool

The music system balances viewer song requests with background music playback:

1. **Viewer Request Queue (High Priority)**:
   - When viewers request songs via `!sr <url/id/search>` or Channel Point redemptions, songs enter the immediate queue.
   - The player always prioritizes songs in the request queue before playing anything from background playlists.
2. **Background Playlist Pool (Fallback)**:
   - When the request queue is empty, the player pulls random unplayed tracks from your active playlist pool (your default playlist plus any additional selected playlists).
   - Songs are marked as played so they won't repeat until the pool is refreshed.

---

### Now Playing & Controls

- **Current Track**: Displays the video title, requester, and duration.
- **Now Playing Actions**:
  - **Steal Song**: If a viewer-requested song is currently playing, clicking **Steal Song** saves the track into your **Default Playlist** for permanent background rotation (shows **Already in Playlist** if already present).
  - **Add to Cooldown**: Places the currently playing song on a temporary request cooldown.
- **Skipping & Moderation**:
  - **Skipping**: To skip the playing track, use the chat commands `!veto` (immediate skip for Moderators/Streamer) or `!skip` (viewer vote-skip), or advance playback directly in the embedded YouTube player.
  - **Request Queue Moderation**: From the **Song Requests** queue table, click **Move to Next** (priority), **Ban Song** (bans the queued track from future requests), or **Remove** (deletes the request from queue).
  - **Banning Current Song**: To ban the currently playing track from future requests, add it via the [Banned Songs](/bannedsongs) page.
- **Embedded Player**: Uses the YouTube IFrame API to play audio and video directly in your browser.

---

### Default Chat Commands

All commands registered by the Music Player service:

| Command | Permission | Description |
| :--- | :--- | :--- |
| `!sr <url / id / search>` | Everyone | Requests a YouTube song to be queued for playback. |
| `!priority` | Everyone | Bumps the caller's most recent song request to the top of the queue so it plays next (has a default 30-minute cooldown). |
| `!song` / `!currentsong` | Everyone | Posts the currently playing song title, artist, requester, YouTube link, and play count in chat. |
| `!nextsong` | Everyone | Posts the next song queued up in the request line. |
| `!lastsong` | Everyone | Posts details about the previous track that just finished. |
| `!skip` / `!voteskip` | Everyone | Casts a vote to skip the current track (defaults to 3 votes needed). |
| `!veto` | Moderator+ | Instantly forces the current song to skip and logs it to `vetoed.txt`. Clears cooldown if skip exemption is enabled. |
| `!wrongsong` / `!wrong` | Everyone | Removes the caller's last requested song from the queue and clears its cooldown if removed before playback. |
| `!stealsong` / `!steal` | Streamer | Saves the currently playing requested track directly into your **Default Playlist**. |
| `!pause` | Streamer | Pauses or resumes music playback. |
| `!importpl <url / id>` | Streamer | Imports a public or unlisted YouTube playlist into the bot. |
| `!loadpl <name>` | Streamer | Switches the active playlist by name. |
