# Auto Clip Playback & AI Shoutouts

PenguinTwitchBot takes creator shoutouts beyond simple static text by incorporating automatic video clip playback and AI-generated channel summaries.

## Automatic Clip Playback

- **Auto Play Clip**: When enabled for a creator, the bot queries the Twitch Clips API to fetch their latest or top-performing clip.
- **Overlay Integration**: The clip is sent to the Alert Widget or Video Player on your OBS browser overlay, accompanied by streamer branding and sound.
- **Duration**: Clips play smoothly within your overlay without requiring manual stream deck button presses.

## AI-Generated Shoutouts (LLM Integration)

- **Use AI**: Enable the **Use AI** toggle for personalized, engaging shoutouts generated on the fly.
- **Context Awareness**: The AI model (`IShoutoutAi`) analyzes the streamer's recent broadcast category, stream title, and bio to compose a lively, unique message.
- **Additional Prompt**: You can supply custom prompt instructions (e.g. "Mention that they are an amazing speedrunner and our long-time coop buddy") to inject channel-specific inside jokes and lore.

