# Multi-Playlist Pooling

You can activate and combine multiple playlists simultaneously to create a large, diverse background music pool.

---

### How Multi-Playlist Pooling Works

1. **Default Playlist (Primary Star)**:
   - Mark one playlist as your **Default** (star icon).
   - The default playlist serves as the base library and is always included in the background rotation.
2. **Additional Playlists (Checkboxes)**:
   - Select any number of additional playlists using the checkboxes next to them.
   - All checked playlists are merged together with your default playlist into an active playback pool.
3. **Automatic Deduplication**:
   - The player automatically deduplicates songs across all selected playlists based on their unique YouTube Video ID (`SongId`).
   - Even if the same song exists in multiple checked playlists, it will only be queued once during a rotation cycle.
4. **Randomized Unplayed Selection**:
   - The player selects randomly from all unplayed tracks in the merged pool.
   - When all tracks across the selected playlists have been played, the pool resets and repeats.

> [!NOTE]
> Stolen songs (via `!steal` or the **Steal** button) are always added into your **Default Playlist**, ensuring they immediately enter this multi-playlist rotation.

