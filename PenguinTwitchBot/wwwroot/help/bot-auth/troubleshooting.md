### Authentication Troubleshooting

#### Bot is Connected but Not Speaking in Chat
1. Verify the bot account is not banned or timed out in your channel.
2. Grant the bot **Moderator** or **VIP** status in your Twitch chat (`/mod BotUsername`) to bypass slow mode and AutoMod filters.
3. Click the **Test Ping** button on the Bot Authentication dashboard to verify write permissions.

#### Streamer Reports Disconnected or Missing Scopes
Click **Sign In as Streamer** to initiate an updated Twitch OAuth grant. This ensures newly required Twitch permissions (such as channel points or raid rewards) are granted.

#### EventSub WebSocket Disconnects
The bot maintains an active Twitch EventSub WebSocket connection for live follows, subs, and point redemptions. If network connectivity drops, the bot automatically re-establishes the connection with exponential backoff.

