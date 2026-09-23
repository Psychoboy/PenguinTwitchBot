# Twitch Channel Points & Custom Rewards

PenguinTwitchBot allows streamers and editors to view, create, edit, pause, and delete Twitch Channel Point custom rewards directly from the bot interface.

## Viewing Rewards

The Channel Points view displays all custom rewards registered on your Twitch channel:
- **Owned Rewards**: Rewards created by PenguinTwitchBot or associated with its application client. These rewards can be fully edited, paused, unpaused, or deleted from this dashboard.
- **External Rewards**: Rewards created directly on Twitch or by third-party applications. These are marked with an **External** badge and can be viewed or deleted, but Twitch API restrictions prevent third-party apps from modifying external rewards.
- **Filter and Search**: Use the search bar at the top of the rewards list to quickly filter rewards by title.
- **Status Chips**:
  - **Cost**: The channel point redemption price.
  - **Enabled / Disabled**: Whether the reward is currently open for redemption in Twitch chat.
  - **Paused**: Indicates redemptions are temporarily held.

## Creating a Custom Reward

Click the **+** (Add) button in the rewards sidebar to open the **Create Reward** form:

1. **Title / Name**: The name displayed to viewers in Twitch chat (maximum 45 characters).
2. **Cost**: The point cost required to redeem (minimum 1).
3. **Prompt**: Optional description or prompt displayed to the viewer when redeeming (maximum 200 characters).
4. **User Input Required**: Enable if the viewer must supply a text message when redeeming (e.g. requesting a song, entering text, or specifying a target).
5. **Skip Queue**: When checked, redemptions automatically fulfill immediately without waiting in your Twitch creator dashboard redemption review queue.
6. **Max Per Stream**: Limit total redemptions of this reward across an entire stream broadcast.
7. **Max Per User Per Stream**: Limit how many times a single viewer can redeem during a stream.
8. **Global Cooldown**: Enforce a cooldown interval in seconds between successive redemptions by any viewer.
9. **Background Color**: Hex color code for the redemption card button in the Twitch chat viewer menu.

## Managing Existing Rewards

Selecting any reward from the list loads its details:
- **Edit Details**: Update title, cost, prompt, limits, and cooldowns. Click **Save Changes** to immediately synchronize with Twitch.
- **Pause / Unpause**: Temporarily prevent viewers from redeeming without having to delete the reward.
- **Delete**: Permanently removes the custom reward from your Twitch channel.

