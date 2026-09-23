# Stream Timer Sub-Actions & Automation

Control your stream timer hands-free through chat commands, channel point redemptions, bits, subscriptions, and subathon rules using timer sub-actions.

---

### Available Sub-Actions

You can attach these sub-actions to any Action trigger under **Actions & Triggers**:

1. **`OverlayTimerStart`**: Starts or resumes the timer.
2. **`OverlayTimerStop`**: Pauses the timer.
3. **`OverlayTimerAddTime`**: Adds a specified amount of time (in seconds or `hh:mm:ss`) to the timer.
4. **`OverlayTimerRemoveTime`**: Deducts time from the timer.

---

### Common Automation Examples

#### 1. Subathon Timer Extension
- **Trigger**: Twitch Event -> `ChannelSubscribe`, `ChannelSubscriptionGift`, or `ChannelBitsUse`.
- **Sub-Action**: `OverlayTimerAddTime` with `30` or `60` seconds.
- **Result**: Each subscriber or cheer automatically adds time to your on-screen countdown timer.

#### 2. Channel Points "Add 2 Minutes"
- **Trigger**: Twitch Event -> `ChannelPointsCustomRewardRedemptionAdd` (filtered by Reward Title: `"Add 2 Minutes to Timer"`).
- **Sub-Action**: `OverlayTimerAddTime` with `120` seconds.

#### 3. Moderator Control Commands
- **Trigger**: Command -> `!timer start` -> `OverlayTimerStart`
- **Trigger**: Command -> `!timer stop` -> `OverlayTimerStop`
- **Trigger**: Command -> `!timer add <seconds>` -> `OverlayTimerAddTime`

