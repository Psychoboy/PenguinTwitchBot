# Plinko Bounce Mechanics

In **Plinko Bounce**, viewer tokens drop from the top of a triangular or rectangular pegboard, ricocheting off pegs before settling into prize buckets at the bottom.

## Enable the Game

Plinko Bounce is off by default, including after upgrading settings that do not yet contain an enable value. Use the **Enable Plinko Bounce** switch beside the page title and save settings to accept new chat commands, automation drops, and rounds. Turning it off silently ignores new chat commands and logs the attempt; an active round continues, while queued rounds are canceled. Stop controls remain available.

## Dropping Plinko Tokens

Viewers participate directly from Twitch chat:
- **Default Command**: `!plinko [emote or points] [theme]`
- **Custom Emotes**: Viewers can pass any Twitch emote code or image URL to brand their falling puck.
- **Theme Overrides**: Viewers can select from available seasonal or custom themes.

## Pegboard Dynamics

- **Deflection Physics**: Tokens bounce off pegs with realistic momentum, slight lateral randomness, and restitution retention so every run is unique.
- **Peg Glow Effects**: Whenever a token contacts a peg, the peg illuminates with a glowing impact burst in the theme's highlight color.
- **Player-on-Player Collisions**: When multiple viewers drop in the same round, their tokens can collide with each other mid-flight, deflecting off one another on their way to the buckets.
- **Board Configuration**: Streamers can configure board dimensions (4 to 14 rows), peg radius, puck radius, bucket height, gravity, bounciness, mass, and peg transparency.

## Prize Buckets

The bottom of the board contains customizable landing slots:
- **Bucket Labels**: Assign names like `100 Pts`, `Jackpot!`, `2x Multiplier`, or `Try Again`.
- **Points & Multipliers**: Set point values awarded upon landing.
- **Win Status**: Mark specific buckets as winning slots (`IsWin: true`). Winning buckets trigger celebratory confetti bursts on the overlay and activate the **Plinko Drop Win** trigger.
- **Color Customization**: Distinguish high-value prize slots with custom fill and border colors.

## Game Modes

- **Single Drop**: Viewers drop tokens whenever they use the command (subject to user cooldowns).
- **Timed Session / Round Mode**: A group countdown begins when the round opens. All viewers who drop within the time window compete in the same round. The highest-scoring token is highlighted as the session leader.

## Outcome Automations

Use **Outcome Automations** on the Plinko game page to link an existing action or create a new one for winning buckets, losing buckets, or round events. Add a Manage Points sub-action for a point type; `%Score%` awards the bucket score directly, while `$math(%Score% * 2)$` applies a multiplier. Edit the linked action in [Manage Actions](/actions/manage) to add more sub-actions or point types.
