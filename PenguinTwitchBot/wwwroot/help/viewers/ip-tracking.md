# IP Logging & Multi-Account Detection

PenguinTwitchBot includes IP logging capabilities to help channel moderators detect ban evasion, sock-puppet accounts, and unfair giveaway farming.

## How IP Tracking Operates

When viewers interact with web endpoints (such as public giveaway entry pages or song request dashboards), their client IP addresses are recorded into the database alongside their Twitch User IDs. Access to these IP records within the viewer profile panel is restricted to authorized Streamers and Moderators. Note that server application logs may also record client IP addresses during HTTP request logging or when database IP logging operations fail.

## Shared IP Accounts

In the viewer profile panel:
- **Shared IP View**: Shows other Twitch accounts that have connected from the same IP address or subnet.
- **Alt Account Detection**: Easily spot multiple accounts submitting entries to the same giveaway or evading channel timeouts.
- **Privacy & Security**: IP records stored in the database are protected by Streamer and Moderator authorization checks.

