# Raid Triggers & Reward Systems

Automate stream responses and reward raiders using EventSub triggers and integrated loyalty systems.

---

### Twitch Event Trigger: `ChannelRaid`

Under **Actions & Triggers**, you can configure an automated action triggered whenever another creator raids your stream.

- **Trigger Name**: `ChannelRaid`
- **Filtering Options**:
  - `MinViewers`: Only execute actions if raid exceeds a minimum viewer count.
  - `MaxViewers`: Cap execution to raids below a viewer threshold.
- **Available Variables**:
  - `{UserId}`: Twitch user ID of the raiding creator.
  - `{Name}` / `{Username}`: Raider's Twitch login name.
  - `{DisplayName}`: Raider's display name.
  - `{NumberOfViewers}` / `{Viewers}`: Total number of viewers participating in the raid.

#### Example Use Cases
- Play a custom sound effect or alert video using an OBS sub-action.
- Post a customized shoutout message in chat welcoming the raider and their community.
- Display a dynamic alert banner on your stream overlay.

---

### Outgoing Raid Commands

- **Chat Command**: `!raid <channel>`
  - Initiates a Twitch raid targeting the specified channel.
  - Automatically records the raid in your outgoing raid history once finalized.

---

### Raid Rewards & Loyalty Integration

When enabled, the bot's `RaidRewardService` awards loyalty points or currency to raiding creators and their viewers:
- **Raider Bonus**: Extra points granted to the streamer initiating the raid.
- **Participant Bonus**: Bonus loyalty awarded to active viewers brought along in the raid.
- Configure amounts and thresholds in **Bot Settings > Feature Services**.

