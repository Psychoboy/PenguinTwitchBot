# Balloon Ascent Mechanics

In **Balloon Ascent**, viewers launch hot air balloons from the bottom of the screen, floating upward against gravity and crosswinds aiming to dock on a moving cloud platform at the top of the stream.

## Launching Balloons

Viewers can participate directly from Twitch chat:
- **Default Command**: `!balloon [emote] [theme]` (also supports `!float` and `!ascent`).
- **Custom Emotes**: Viewers can pass any Twitch emote code or URL to ride beneath their personalized balloon canopy.
- **Theme Overrides**: Viewers can select from available seasonal or custom themes if enabled.

## Physics & Movement

- **Buoyant Ascent**: Balloons rise with positive buoyancy, countered by atmospheric drag to create a smooth, floating upward journey.
- **Pendulum Avatar Sway**: Avatars sway dynamically beneath their balloon canopy according to lateral velocity and wind drift, tilting into turns.
- **Screen Wall Rebounds**: Balloons gently bounce off screen edges with dampening to keep all players in view.
- **Top Cloud Platform**: A cloud platform patrols the top of the screen with configurable speed, width, bounce margins, and a center golden bullseye. Docking closer to the center awards higher scores (0–100 points).

## Interactive Hazards & Sabotage

Streamers and moderators can trigger interactive hazards (via chat command `!hazards` or sub-actions) to challenge ascending balloons:

| Hazard Archetype | Default Visual | Physics & Effect | Pop Risk |
| :--- | :--- | :--- | :--- |
| **Nudge** | Water droplets, acorns | Elastic deflection: knocks the balloon sideways with pendulum tilt. | **0% Pop** |
| **Heavy Push** | Anvils, heavy weights | Downward slam: strikes the canopy with a cartoon clonk, depressing the balloon and momentarily stalling ascent. | **0% Pop** |
| **Wind Gust** | Jetstreams, clouds | Lateral blast: sweeps across the screen blowing balloons horizontally. | **0% Pop** |
| **Dart Pop** | Sharp darts, needles | Puncturing impact: pops the balloon with confetti and rubber debris; avatar descends with an umbrella. | **Configurable** (0–100%) |

### Scoring & Popped Modes
When a balloon is popped by a sharp hazard, scoring is handled by your configured **Popped Score Mode**:
- **Zero Score**: Popped balloons receive 0 points.
- **Half Score**: Popped balloons receive 50% of the score achieved up to the impact height.
- **Preserve Score**: Retains the score achieved at the moment of impact.

## Game Modes

- **Single Ascent**: Viewers launch balloons at any time (subject to user cooldowns).
- **Timed Session / Round Mode**: A group countdown begins when the round opens. All viewers who launch within the window rise together. The player who docks closest to the bullseye is crowned the round leader with a golden crown highlight.

