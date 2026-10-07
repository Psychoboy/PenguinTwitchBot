# TTS Commands & Sub-Actions

Integrate Text-to-Speech directly with Twitch chat commands, channel points, and automated Action workflows.

---

### Default Chat Commands

The TTS service registers the following default command:

| Command | Permission | Description |
| :--- | :--- | :--- |
| `!say <message>` | Everyone | Speaks the message aloud using the speaker's assigned voice (or the system fallback voice). |

> [!TIP]
> Cooldowns and point costs for `!say` can be customized in [Default Commands](/commands/default) or [Game Settings](/settings/games).

---

### TTS Sub-Actions (`TtsType`)

When building custom Actions in [Manage Actions](/actions/manage), you can add a **TTS** subaction step:

- **Message**: The text to be spoken. Supports dynamic variable replacement (e.g. `Welcome {user} to the stream! Thank you for the raid of {viewers} viewers!`).
- **Voice Override**: Specify a fixed voice or leave blank to use the triggering user's assigned voice.
- **Volume & Speed**: Customize audio volume and playback pitch/speed.

---

### Channel Point Redemptions

To create a "TTS Chat Message" channel point reward:
1. In [Channel Points](/settings/channel-points), create a reward requiring text input (e.g. *TTS Message*).
2. In [Action Commands](/actions/commands) or [Manage Actions](/actions/manage), add a Channel Point Redemption trigger for this reward.
3. Add a **TTS** subaction with `{rawInput}` as the spoken text.

