# Outgoing Raid Rewards

PenguinTwitchBot's Raid Rewards system rewards loyal viewers who join your outgoing raids to support fellow streamers across the Twitch community.

## How Raid Rewards Work

When you initiate an outgoing raid from your channel:
1. **Raid Detection**: The bot detects that your channel has initiated a raid to a target channel.
2. **Snapshot Chatters**: Viewers who were active in your chat prior to the raid are marked as eligible participants.
3. **Chat Announcement**: The bot posts an announcement in your chat with the raid message, instructions, and time limit.
4. **Target Chat Monitoring**: When the raid lands, viewers post the designated raid message in the raided streamer's chat.
5. **Point Awarding**: When an eligible viewer posts the phrase within the allowed time window, the bot awards the configured points to their account balance.

## Configuration Options

In [Raid Rewards](/settings/raidrewards):
- **Enable Raid Rewards**: Toggle the feature active or inactive.
- **Point Type**: Select which currency is awarded to successful raiders.
- **Points to Award**: Amount of points granted per participant.
- **Time Window (Minutes)**: Time limit (1 to 120 minutes) during which raid messages are monitored in the target channel.
- **Message Viewers Must Send**: Required phrase (case-insensitive) viewers must post in the raided channel.
- **Subscriber-Only Message (Optional)**: Alternative phrase that only subscribers can post (e.g. including channel subscriber emotes).
- **Announce Raid Reward Message**: Posts the raid instructions in your chat prior to raiding out.
- **Send Reminders**: Posts reminder notices at 30 and 60 seconds if the raid has not yet transitioned.
- **Announcement Template**: Customizable template supporting placeholders:
  - `{target}`: Target raided channel username.
  - `{message}`: Required raid message phrase.
  - `{submessage}`: Subscriber-only message phrase.
  - `{minutes}`: Time window duration.
  - `{points}`: Points awarded.
  - `{pointtype}`: Currency name.

