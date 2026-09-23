# External Commands

External Commands allow you to register commands that are executed by other external programs or bots (such as Streamer.bot, MixItUp, SAMMI, or external scripts) into Penguin Twitch Bot.

---

## Purpose & Scope

> [!NOTE]
> External Commands do **not** make outgoing HTTP requests or forward responses. Instead, they serve as a unified registry so that commands handled outside Penguin Twitch Bot are listed in the public viewer commands page (`/commands`).

- **Unified Commands Directory**: Viewers visiting your channel's public `/commands` page see all available commands in one place, even if some commands are powered by external software.
- **Categorization**: Group external commands under clean categories alongside your bot's native default and action commands.
- **Cooldown & Rank Documentation**: Display the required permission tier (e.g. Broadcaster, Moderator, VIP, Viewer) and cooldown durations so viewers understand how and when the command can be used.
