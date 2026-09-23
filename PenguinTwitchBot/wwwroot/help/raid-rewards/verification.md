# Raid Verification & Eligibility

To maintain fairness and prevent abuse, PenguinTwitchBot enforces strict eligibility rules before awarding raid rewards.

## Eligibility Rules

1. **Pre-Raid Chat Presence**:
   - A viewer must have been connected and present in your chat before the raid occurred.
   - Users who were already in the target channel's chat before the raid or who did not participate in your stream are not eligible.
2. **Single Award Per Raid**:
   - Points are awarded exactly once per viewer per raid, regardless of how many times they post the raid message in the target chat.
3. **Time Window Limit**:
   - Messages must appear before the configured time window expires. Messages posted after the deadline will not trigger point rewards.
4. **Case-Insensitive Matching**:
   - The message match is case-insensitive and succeeds if the viewer's chat message contains the configured phrase.

## Sub-Actions & Triggers

- **Raid History Integration**: Outgoing raids and participant statistics are tracked and can be reviewed in **Raid History** (`/streamtools/raid-history`).
- **Actions & Sub-Actions**:
  - The `GiftPoints` sub-action can be used in custom automated actions triggered by outgoing or incoming raids.
  - Twitch event triggers (`TwitchEvent`) can listen to raid events to launch hype overlays, play sound effects, or trigger fireworks.

