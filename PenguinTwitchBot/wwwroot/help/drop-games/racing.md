# Racing Minigame

**Racing** is an interactive, multi-vehicle stream overlay minigame where viewers race go-karts and seasonal speedsters around customizable race tracks right on your stream broadcast!

## Key Features

- **Multiple Track Layouts**:
  - **Perimeter Ring**: Spline track hugging the outer edges of your screen. The entire center 90%+ of the display remains crystal clear so gameplay and camera feeds are unobstructed.
  - **Edge Oval**: Compact strip oval along the bottom, top, left, or right edge.
  - **Figure-8**: Classic crossover layout with intersections creating thrilling near-misses and collisions.
  - **Drag Strip**: Straight sprint track for high-velocity shootouts.
- **Dynamic Physics & Suspense**:
  - **Vehicle Bumping**: Racers bump into each other in tight corners, pushing opponents sideways.
  - **Slipstream Drafting**: Tucking closely behind a rival charges a drafting meter, yielding a sudden turbo surge and overtake maneuver!
  - **Debris & Boost Pads**: Oil slicks cause spinouts, traffic cones bounce karts off course, and neon boost pads launch racers forward.
  - **Rubber-Banding**: Trailing karts receive a subtle boost to ensure close, nail-biting finishes.
- **Custom Emotes & Drivers**:
  - Viewers can ride with their favorite Twitch, BTTV, FFZ, or 7TV emote displayed in the cockpit, with fallback to seasonal penguins.
- **Game Modes**:
  - **Grand Prix (Mass Start)**: Viewers join the grid during a configurable countdown (default 60 seconds). When the lights go green, all racers launch simultaneously in a high-octane dash!
  - **Solo Time Trial**: Instant solo hot lap (`!timetrial [emote]`) without waiting for others. Features a real-time stopwatch and semi-transparent ghost kart matching the stream record!
  - **Elimination**: Multi-lap tournament where the last-place racer on each lap is knocked out until only the champion remains!

---

## Viewer Chat Commands

- `!race [emote]` — Join the active race lobby or start a new grid countdown with an optional custom emote.
- `!racing` / `!kart` / `!grandprix` — Built-in aliases for `!race`.
- `!timetrial [emote]` — Launch an immediate solo Time Trial hot lap against the clock.
- `!tt [emote]` — Shorthand alias for `!timetrial`.

### Moderator Session Commands

- `!race start [seconds]` — Open a Grand Prix race lobby for the specified duration (e.g., `!race start 45`).
- `!race stop` — Terminate the active race session or lobby immediately.

---

## Stream Overlay Widget

- **Overlay URL**: `/racing.html` (recommended 1920×1080 transparent Browser Source in OBS Studio)
- **Automatic Fade-Out**: When no race is active, the track curbs, timers, and HUD elements automatically fade to 0% opacity, keeping the broadcast spotless.
- **Client Authority**: The authorized browser overlay computes physics, overtakes, lap crossings, and wrecks, reporting authoritative finishes with single-use cryptographically verified `dropToken`s.

