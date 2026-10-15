# Parachute Drop Mechanics

In **Parachute Drop**, viewers float down the screen under a parachute canopy aiming to land squarely on the moving target platform at the bottom of the screen.

## Physics & Movement

- **Descent Velocity**: Gravity gently accelerates parachuters downward, balanced by canopy air drag to ensure a readable, floating descent.
- **Wind Drift & Sway**: Parachuters sway dynamically side-to-side based on their horizontal momentum, tilting naturally into turns.
- **Target Platform**: A moving platform patrols the bottom of the screen with configurable speed, width, and bounce zones. Landing on the bullseye awards maximum points and triggers celebratory effects.

## Interactive Droplets & Obstacles

Obstacle droplets can fall from the sky to knock parachuters off course:
- **Momentum Transfer**: When a falling droplet strikes a parachuter canopy or body, it transfers physical horizontal momentum, kicking the parachuter sideways and tilting their chute.
- **Patterns**:
  - `TopRain`: Droplets fall from randomized locations across the top of the screen (matching classic arcade drops).
  - `SideWind`: Droplets stream in diagonally from the left or right edges.
  - `Barrage`: Concentrated waves of droplets aimed near active parachuters.
- **Droplet Sizing**: Choose from Small, Medium, Large, or Giant droplets, or assign custom Twitch emotes as the falling projectiles.

## Game Modes

- **Single Drop**: Viewers drop whenever they use the command (subject to user cooldowns).
- **Timed Session / Round Mode**: A group countdown begins when the first viewer drops. All viewers who drop within the time window fall together in a single round. The viewer who lands closest to the bullseye is crowned the session leader with a floating gold crown.

