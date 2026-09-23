# Shoutout Commands, Triggers & Sub-Actions

In addition to automated detection, creators and moderators can invoke shoutouts manually via chat commands, sub-actions, and trigger hooks.

## Default Commands

| Command | Minimum Rank | Description |
| :--- | :--- | :--- |
| `!so <user>` | VIP | Shouts out the specified user, invoking chat messaging, Twitch `/shoutout`, and optional clip playback. |
| `!shoutout <user>` | VIP | Alias for `!so`. |

## Triggers & Actions Engine Integration

Shoutouts integrate with PenguinTwitchBot's Actions and Triggers system:
- **Twitch Raid Trigger**: Configure an incoming raid trigger (`TwitchEvent`) to immediately run an action that triggers `!so {User}` for the raiding streamer.
- **Sub-Actions**:
  - `Alert`: Triggers an overlay visual alert banner featuring the target channel's avatar and name.
  - `PlaySound`: Plays a signature shoutout sound effect or jingle.
  - `Tts`: Announces the shoutout over Text-to-Speech audio.
  - `SendMessage`: Posts custom followup links (such as Twitter, YouTube, or Discord) alongside the shoutout.

