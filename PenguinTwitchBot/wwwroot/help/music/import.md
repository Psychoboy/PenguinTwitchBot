# Importing YouTube Playlists

Quickly import existing YouTube playlists into PenguinTwitchBot without adding songs one by one.

---

### Step-by-Step Import

1. In YouTube, ensure your playlist's privacy setting is set to **Public** or **Unlisted** (private playlists cannot be accessed by the YouTube API).
2. Copy the playlist URL or ID:
   - Full URL: `https://www.youtube.com/playlist?list=PL1234567890abcdef`
   - Raw ID: `PL1234567890abcdef`
3. Click the **Import from YouTube** button on the Playlists page.
4. Paste the playlist link or ID, and provide a name for the new playlist.
5. Click **Import**. The bot will contact YouTube to fetch track titles, video IDs, and durations, creating a new local playlist.

> [!NOTE]
> A valid YouTube Data API key must be configured in **Bot Settings > Integrations > YouTube** to fetch playlist metadata from YouTube.

