# Chat Templates & Game Messaging

PenguinTwitchBot allows full customization of every chat message sent during mini-game execution, from countdown announcements to dramatic win and loss outcomes.

## Dynamic Placeholders

Depending on the specific game, the following runtime tokens are replaced automatically:

- `{Name}` / `{User}`: Username of the triggering player or participant.
- `{Cost}` / `{Bet}`: The number of points wagered or spent to enter.
- `{Reward}` / `{Payout}`: Total points won upon victory.
- `{PointType}`: Display name of the active currency (e.g. Points, Tickets).
- `{Time}` / `{Seconds}`: Remaining seconds in the join countdown window.
- `{Survivors}` / `{Winners}`: Comma-separated list or count of surviving winners (Heist, FFA).
- `{Reels}` / `{Result}`: The visual outcome display (e.g. `[ 🍒 | 🍒 | 🍒 ]` in Slots or chamber click in Roulette).

## Formatting Best Practices

- **Keep Messages Concise**: Busy Twitch chats scroll rapidly; short, punchy messages with channel emotes are easier for viewers to read.
- **Tone & Roleplay**: Customize messaging to match your stream aesthetic (e.g. pirate raid heist, cyberpunk hacker defuse, space arena FFA).
- **Emotes**: You can include any Twitch global, channel, or third-party (BTTV/7TV) emote text in your templates.

