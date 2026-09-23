# Configuring External Commands

Adding an external command registers its metadata into the bot's database so viewers can discover it on your channel's public commands page.

---

## Configuration Fields

- **Command Trigger**: The chat trigger keyword (without the leading `!` prefix).
- **Category**: Organizational grouping for display on the commands page (e.g., `Minigames`, `Socials`, `Streamer.bot`).
- **Description**: Explains what the command does when invoked in Twitch chat.
- **Minimum Rank**: Specifies the minimum viewer rank required to use the command:
  - `Broadcaster`
  - `Moderator`
  - `VIP`
  - `Subscriber`
  - `Viewer`
- **User Cooldown (Min / Max)**: Per-user cooldown duration in seconds (optional randomized range if max is set).
- **Global Cooldown (Min / Max)**: Channel-wide cooldown duration in seconds.
- **Command Disabled**: Temporarily hides the command from the public commands directory without deleting it.
