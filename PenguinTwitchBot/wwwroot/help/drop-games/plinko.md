# Plinko Bounce Mechanics

In **Plinko Bounce**, viewer tokens drop from the top of a triangular or rectangular pegboard, ricocheting off pegs before settling into prize buckets at the bottom.

## Pegboard Dynamics

- **Deflection Physics**: Tokens bounce off pegs with realistic momentum, slight lateral randomness, and restitution retention so every run is unique.
- **Peg Glow Effects**: Whenever a token contacts a peg, the peg illuminates with a glowing impact burst in the theme's highlight color.
- **Player-on-Player Bounces**: When multiple viewers drop in the same round, their tokens can collide with each other mid-flight, deflecting off one another on their way to the buckets.

## Prize Buckets

The bottom of the board contains customizable landing slots:
- **Bucket Labels**: Assign names like `100 Pts`, `Jackpot!`, `2x Multiplier`, or `Try Again`.
- **Points & Multipliers**: Set point values awarded upon landing.
- **Win Status**: Mark specific buckets as winning slots (`IsWin: true`). Winning buckets trigger celebratory confetti bursts on the overlay and activate the **Plinko Drop Win** trigger.
- **Color Customization**: Distinguish high-value prize slots with custom fill and border colors.

