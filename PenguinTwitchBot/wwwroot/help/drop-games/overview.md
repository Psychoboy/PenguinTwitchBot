# Parachute Drop & Plinko Bounce Overview

The **Parachute Drop & Plinko Bounce** system provides interactive, physics-driven minigames for your stream overlays. Viewers can drop characters into the stream using chat commands (such as `!drop` or `!plinko`), Channel Point rewards, or automated bot actions.

## Stream Overlay Widgets

Both minigames run on transparent HTML5 canvas overlays designed for OBS Studio and stream production suites:

- **Parachute Drop Overlay**: `/parachute.html` (recommended 1920×1080 transparent Browser Source)
- **Plinko Bounce Overlay**: `/plinko.html` (recommended 1920×1080 transparent Browser Source)

Both overlays automatically connect to the bot's WebSocket server and render entity movement at a smooth 60 FPS.

## Independent Per-Game Queues

To keep chat engaged without collisions between different games:
- **Concurrent Play**: Parachute Drop and Plinko Bounce can run simultaneously on stream at the same time without interfering with each other.
- **Sequential Queuing**: If a viewer or command starts a new round of Parachute Drop while one is already active, the new drop cleanly enqueues behind it. As soon as the active round finishes or lands, the next queued session begins automatically. Plinko rounds queue independently in the same manner.

## Tamper-Proof Security

All scores, landings, and win distributions are verified by the server:
- Every drop receives a single-use cryptographically signed `dropToken`.
- When an entity lands on stream, the browser overlay sends the completion token back to the server for authoritative validation.
- Unregistered tokens, duplicated tokens, and modified landing coordinates are rejected, ensuring that points and win triggers cannot be forged.

