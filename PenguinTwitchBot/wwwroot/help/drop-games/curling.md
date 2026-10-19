# Curling / Shuffleboard Mechanics

In **Curling / Shuffleboard**, viewers slide realistic granite curling stones across a frozen ice sheet or wooden shuffleboard track along the edge of the broadcast screen. Stones slide according to true friction physics, colliding with other stones and coming to rest in high-scoring target zones.

## Viewer Commands

Viewers can participate directly from Twitch chat:
- **Default Command**: `!curl [power] [emote]` (also supports `!slide` and `!shuffle`).
- **Throw Power**: When manual power is enabled in settings, viewers can type a power number from `1` to `100` (e.g. `!curl 52`). Power around ~50–55 lands near the button center under normal ice conditions.
- **Custom Emotes & Avatars**: Non-numeric arguments (such as Twitch emotes, 7TV/BTTV/FFZ emotes, or emojis) are rendered as circular stone face decals, complete with custom user chat color accent rings and handles.

## Edge Strip Layout & Inward-Facing Nametags

Curling / Shuffleboard is engineered to sit along any screen edge without obstructing central stream content:
- **Configurable Edge Placement**: Choose **Bottom** (default), **Top**, **Left**, or **Right** strip positioning.
- **Dynamic Track Thickness**: Adjustable from 10% to 25% of screen width/height (default 14%).
- **Inward-Facing Nametags**: Nametags and score labels render inward towards the center of the screen (above the stone for bottom strips, below the stone for top strips, right for left strips, and left for right strips). This guarantees viewer names and scores never clip outside screen boundaries.
- **Transparent Sheet**: The ice sheet canvas is 100% transparent by default (`BoardAlpha = 0.0`), with toggleable 1px guide rails. The overlay automatically hides itself when no stones or sessions are active.

## Dynamic Ice Sheet Conditions

To prevent viewers from setting up macros on a single power number, rounds feature dynamic ice conditions:
- **Normal Ice**: Standard friction and deceleration drag ($0.015$).
- **Slick Ice**: Reduced friction (-20% drag). Stones slide further with higher glide speed.
- **Heavy Frost**: Increased friction (+25% drag). Stones decelerate rapidly.
- **Cross-Drift**: Subtle lateral wind waves and micro-pebble drift along the track.
- **Random Target Distance**: In addition to ice conditions, the target house distance can randomize between 70% and 92% each round. Dynamic condition pills notify viewers in real time.

## Anti-Griefing & Collision Modes

Stones interact via realistic 2D elastic collisions. The collision rules are fully configurable:
- **Lock On Rest (Default / Anti-Griefing)**: Moving stones collide with each other in real time while sliding. Once a stone comes to a complete rest, it locks into place, and subsequent moving stones ghost cleanly past it. This protects the session leader in the button from last-minute griefing!
- **No Collision**: All stones ghost through one another without physical collision.
- **Full Collision**: Moving stones collide with all stones, including stationary ones.

## Dual Scoring Modes

The minigame supports two distinct scoring rule sets:
- **Curling House Rings**:
  - **Button (Bullseye)**: 100 points
  - **4-Foot Ring**: 75 points
  - **8-Foot Ring**: 50 points
  - **12-Foot Ring**: 25 points
  - **Out of Bounds**: 0 points
- **Shuffleboard Zones**:
  - **Zone 3 (Apex)**: 30 points
  - **Zone 2**: 20 points
  - **Zone 1**: 10 points
  - **10 OFF / Kitchen (Back Trap)**: -10 points penalty (can be toggled on/off in settings to score 0 points on overshot pucks without penalty)

## Game Modes

- **Single Slide (Free Play)**: Viewers launch stones at any time (subject to viewer cooldowns).
- **Timed Session / Round Mode**: A group countdown begins when the round opens. Viewers compete to land closest to the center button. The highest scoring player is crowned the round champion with a golden crown (`👑`) highlight over their stone.

