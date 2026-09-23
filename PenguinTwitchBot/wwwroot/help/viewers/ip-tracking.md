# IP Logging & Multi-Account Detection

PenguinTwitchBot includes IP logging capabilities to help channel moderators detect ban evasion, sock-puppet accounts, and unfair giveaway farming.

## How IP Tracking Operates

When viewers interact with authenticated web endpoints (such as public giveaway entry pages, song request dashboards, or webhooks), their client IP addresses are securely logged alongside their Twitch User IDs.

## Shared IP Accounts

In the viewer profile panel:
- **Shared IP View**: Shows other Twitch accounts that have connected from the same IP address or subnet.
- **Alt Account Detection**: Easily spot multiple accounts submitting entries to the same giveaway or evading channel timeouts.
- **Privacy & Security**: IP records are stored locally in the bot's database and are only accessible to authorized Streamers and Moderators.

