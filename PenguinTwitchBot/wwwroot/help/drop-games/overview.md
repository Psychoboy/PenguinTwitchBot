# Stream Drop Minigames Overview

The **Stream Drop Minigames** system provides interactive, physics-driven games for your broadcast overlays: **Parachute Drop**, **Plinko Bounce**, **Balloon Ascent**, **Curling / Shuffleboard**, **Golf (Chip & Putt)**, and **Racing & Destruction Derby**. Viewers can launch characters, tokens, stones, golf balls, and race cars into the stream using chat commands (such as `!drop`, `!plinko`, `!balloon`, `!curl`, `!golf`, `!race`, or `!timetrial`), Channel Point rewards, or automated bot actions.

## Stream Overlay Widgets

All minigames run on high-performance, transparent HTML5 canvas overlays designed for OBS Studio and stream production suites:

- **Parachute Drop Overlay**: `/parachute.html` (recommended 1920×1080 transparent Browser Source)
- **Plinko Bounce Overlay**: `/plinko.html` (recommended 1920×1080 transparent Browser Source)
- **Balloon Ascent Overlay**: `/balloon.html` (recommended 1920×1080 transparent Browser Source)
- **Curling / Shuffleboard Overlay**: `/curling.html` (recommended 1920×1080 transparent Browser Source)
- **Golf (Chip & Putt) Overlay**: `/golf.html` (recommended 1920×1080 transparent Browser Source)
- **Racing & Destruction Derby Overlay**: `/racing.html` (recommended 1920×1080 transparent Browser Source)

Each overlay can be added as a standalone OBS Browser Source (using the **Open Overlay** button on the game's settings page to include your `?key=` security token if enabled) or combined inside the **Overlay Editor** (`/overlay.html`). Each overlay automatically connects to the bot's WebSocket server and renders entity movement, collisions, and particle effects at a smooth 60 FPS.

## Unified Canvas HUD & Visual Parity

All drop games share a unified, clean HUD system:
- **Session Header Banners**: Display formatted `mins:secs` countdown timers, real-time drop/racer counters, and leader highlights.
- **Smart Auto-Avoid Overlap**: When multiple games run simultaneously on stream, headers intelligently shift down to prevent visual overlap.
- **Configurable Target & Track Opacity**: Adjust target platform or track opacity (`TargetAlpha` / `TrackAlpha`) to avoid obscuring gameplay during broadcasts.
- **Z-Order Stacking**: Winning leaders and active entities stay visually on top of lingering landed drops.

## Independent Per-Game Queues

To keep chat engaged without collisions between different games:
- **Concurrent Play**: Parachute Drop, Plinko Bounce, Balloon Ascent, Curling, Golf, and Racing can run concurrently on stream without interfering with each other.
- **Sequential Queuing**: If a viewer, moderator, or sub-action starts a new round of a minigame while one is already active, the new request cleanly enqueues behind it (preserving its duration, theme, and mode overrides). As soon as the active round concludes, the next queued session begins automatically. Each game manages its queue independently without cross-game blocking.
- **Stop & Clear Queue**: Running `<command> stop` (e.g., `!drop stop`, `!plinko stop`, `!balloon stop`, `!curl stop`, `!golf stop`, `!race stop`), clicking **Force Stop** in the dashboard, or running **Stop All Minigame Sessions** immediately stops the active session and clears any queued sessions. You can also stop an individual game using its dedicated sub-action (**Stop Parachute Session**, **Stop Plinko Session**, **Stop Balloon Session**, **Stop Curling Session**, **Stop Golf Session**, or **Stop Race Session**), where clearing queued sessions is controlled by the sub-action's `ClearQueue` setting.

## Tamper-Proof Security

All scores, landings, and win distributions are verified server-side:
- Every drop or race entry receives a single-use cryptographically random `dropToken`.
- When an entity reaches its target, finishes a race, or lands on stream, the browser overlay sends the completion token back to the server for authoritative validation.
- Unregistered tokens, duplicated tokens, and out-of-range coordinates are rejected, ensuring that points, leaderboard standings, and win triggers cannot be forged.
