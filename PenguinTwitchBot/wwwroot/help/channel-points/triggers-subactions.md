# Channel Points Triggers & Sub-Actions

Channel Point redemptions deeply integrate with PenguinTwitchBot's Actions and Automation engine, allowing redemptions to trigger bot actions or allow actions to dynamically modify reward states.

## Triggers: `ChannelPointsCustomRewardRedemptionAdd`

Any channel point redemption can initiate automated bot actions:
- **Trigger Type**: `TwitchEvent` with event name `ChannelPointsCustomRewardRedemptionAdd`.
- **Targeting Specific Rewards**: You can bind an action to a specific reward by entering its exact title, or leave the filter empty to react to all redemptions.
- **Redemption Context Variables**:
  When a redemption fires an action, the following runtime variables are automatically made available to subsequent sub-actions:
  - `{User}`: Twitch username of the viewer who redeemed the reward.
  - `{DisplayName}`: Display name of the redeemer.
  - `{UserId}`: Twitch User ID of the redeemer.
  - `{RewardTitle}`: Name of the redeemed channel point reward.
  - `{RewardCost}`: Cost in channel points.
  - `{UserInput}` / `{Message}`: Text message submitted by the viewer (if User Input Required was enabled).
  - `{RedemptionId}`: Unique Twitch redemption ID.
  - `{WatchStreak.StreakCount}`: Viewer's current watch streak count (if applicable).
  - `{WatchStreak.ChannelPointsAwarded}`: Points awarded for the streak.

## Sub-Actions for Channel Points

Actions can programmatically control rewards during streams:

### 1. `ChannelPointSetEnabledState`
- Dynamically enables or disables a channel point reward.
- **Use Cases**:
  - Automatically disable an expensive reward during intense gameplay or boss fights.
  - Enable a special "Bonus Reward" only when reaching a subscriber or hype goal.
  - Re-enable rewards at stream startup or via a stream deck button.

### 2. `ChannelPointSetPausedState`
- Sets a custom reward to paused or unpaused.
- **Use Cases**:
  - Pause high-intensity viewer redeemable rewards (e.g. wheel spins, jump scares, TTS) while in queue or during breaks.
  - Automatically pause a reward when a maximum count of custom bot items is reached.

## Common Workflows

- **Channel Point Song Requests**: Trigger an action on a custom reward redemption with user input to enqueue a song in the Music Player queue.
- **Channel Point Wheel Spins**: Trigger the Wheel overlay to spin for the viewer's channel points.
- **Channel Point Fireworks**: Trigger a celebration firework overlay widget when a viewer redeems a hype reward.
- **User-to-User Point Transfers**: Transfer internal points from one viewer to another using the `GiftPoints` sub-action within custom redemption workflows.

