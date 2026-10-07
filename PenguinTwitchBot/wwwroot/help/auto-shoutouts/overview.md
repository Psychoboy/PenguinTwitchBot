# Auto Shoutouts Overview

The **Auto Shoutouts** system recognizes fellow creators, raiding streamers, and VIP community members when they first arrive and chat during your broadcast.

## How Auto Shoutouts Work

1. **First Message Detection**: When a registered streamer sends their first chat message of the stream session, the bot automatically checks if their shoutout cooldown has expired.
2. **Twitch API Integration**: The bot triggers Twitch's official native `/shoutout` command via the Twitch API.
3. **Chat Announcement**: The bot sends a customized chat message highlighting the streamer's channel and game category.
4. **Overlay Media**: If enabled, the streamer's latest or most popular clip plays automatically in your stream overlay.
5. **Cooldowns**: Each streamer receives a shoutout at most once per broadcast (or per configured interval), preventing repetitive messages.

