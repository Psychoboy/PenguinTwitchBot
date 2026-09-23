# Moderation Penalties & Messages

When a viewer's message triggers a blacklist match, PenguinTwitchBot executes immediate moderation actions via the Twitch API.

## Configurable Penalties

- **Timeout Duration**: Set a timeout length in seconds (e.g. 10s warning purge, 600s 10-minute timeout, 86400s 24-hour timeout).
- **Permanent Ban**: Toggle **Perma-Ban** to permanently ban the offending account immediately instead of applying a temporary timeout.
- **Audit Ban Reason**: Specify an internal reason passed directly to Twitch's moderation logs so other moderators understand why the action was taken.
- **Bot Response Message**: Optional chat message sent by the bot warning the chatter (e.g. `@{User}, that phrase is prohibited in this channel!`). Leave blank for silent moderation.

