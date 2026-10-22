# Parachute Drop Mechanics

In **Parachute Drop**, viewers float down the screen under a parachute canopy aiming to land squarely on the moving target platform at the bottom of the screen.

## Enable the Game

Parachute Drop is off by default. Use the **Enable Parachute Drop** switch beside the page title and save settings to accept new chat commands, automation drops, and rounds. When disabled, new chat play commands are ignored and logged only when no timed round is active; commands remain accepted during an active round. Turning it off cancels queued rounds, while the active round continues. Stop controls remain available.

## Launching Parachuters

Viewers participate directly from Twitch chat:
- **Default Command**: `!drop [emote] [theme]`
- **Custom Emotes**: Viewers can pass any Twitch emote code or URL to float down under their chosen character.
- **Theme Overrides**: Viewers can optionally specify an active theme to customize their canopy colors and character art.

## Physics & Movement

- **Descent Velocity**: Gravity gently accelerates parachuters downward, balanced by canopy air drag to ensure a readable, floating descent.
- **Wind Drift & Sway**: Parachuters sway dynamically side-to-side based on horizontal momentum, tilting into turns.
- **Target Platform**: A moving platform patrols the bottom of the screen with configurable speed, width, and bounce zones. Landing on the center bullseye awards maximum points and triggers celebratory effects.

## Interactive Droplets & Obstacles

Obstacle droplets can fall from the sky to knock parachuters off course:
- **Momentum Transfer**: When a falling droplet strikes a parachuter canopy or body, it transfers physical horizontal momentum, kicking the parachuter sideways and tilting their chute.
- **Droplet Patterns**:
  - `Targeted`: Concentrated waves of droplets aimed near active parachuters.
  - `TopRain`: Droplets fall from randomized locations across the top of the screen (matching classic arcade drops).
  - `SideWind`: Droplets stream in diagonally from the left or right edges.
  - `Fountain`: Droplets erupt upwards from the bottom of the screen and arc downward.
- **Droplet Sizing**: Choose from Small, Medium, Large, or Giant droplets, or assign custom Twitch emotes as the falling projectiles.

## Anti-Air Rockets

Streamers or chat events can trigger anti-air rockets targeting falling parachuters:
- **Canopy Destruction**: Direct rocket hits can destroy the parachute canopy, causing the player to crash land.
- **Crash Landing Scoring**: Configurable to **Zero Score**, **Half Score**, or **Preserve Score** upon impact.
- **Attacker Recognition**: When a rocket destroys a parachute, the attacker's username is announced and broadcast to the overlay.

## Game Modes

- **Single Drop**: Viewers drop whenever they use the command (subject to user cooldowns).
- **Timed Session / Round Mode**: A group countdown begins when the first viewer drops. All viewers who drop within the time window fall together in a single round. The viewer who lands closest to the bullseye is crowned the session leader with a floating gold crown.

## Outcome Automations

Use **Outcome Automations** on the Parachute game page to link an existing action or create a new one for Win, Bullseye, Lose, or round events. Add a Manage Points sub-action for the selected point type; `%Score%` awards the score directly, and `$math(%Score% * 2)$` applies a multiplier. Edit the linked action in [Manage Actions](/actions/manage) to add more sub-actions or point types. Bullseye also fires Win, so linking the same action to both can award twice.
