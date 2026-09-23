# Banned Song Trigger & Automation

Whenever a viewer attempts to request a banned song via chat commands (`!sr`) or channel point redemptions, the bot rejects the request and automatically triggers automation rules.

---

### Trigger: `BannedSongRequest` (`Song.BannedRequest`)

Under **Actions & Triggers**, you can bind customized actions whenever a banned track is submitted:

- **Trigger Name**: `Banned Song Requested` (`BannedSongRequest`)
- **System Event**: Fired immediately when input video ID matches a record in the banned songs repository.
- **Available Variables in Actions**:
  - `{User}`: Name of the viewer who attempted the request.
  - `{SongTitle}`: Title of the banned song.
  - `{SongId}`: 11-character YouTube video ID.
  - `{Reason}`: Configured ban reason (or "Banned song" if none was provided).

---

### Suggested Action Configurations

1. **Moderation Feedback**:
   - Add a **Send Message** sub-action: `"@{User}, that song has been banned from stream requests! (Reason: {Reason})"`
2. **Channel Points Protection**:
   - If using channel point redemptions for song requests, configure the bot to cancel/refund the redemption or log the infraction to Discord via a Discord Webhook sub-action.
3. **Sound/Visual Effect**:
   - Trigger a buzzer sound effect or play an OBS overlay animation to poke fun at the denied request.

