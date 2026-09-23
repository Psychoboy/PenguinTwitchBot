# Action Queues

Action Queues manage how multiple actions are processed, scheduled, and executed in Penguin Twitch Bot.

---

## Why Use Queues?

Without queues, rapid triggers (like multiple cheers, sound alerts, or viewer redeems in quick succession) would execute at the exact same moment. This can cause overlapping audio, visual clutter on stream overlays, or race conditions.

Queues allow you to govern execution behavior:
- **Sequential (Blocking)**: Actions in a blocking queue wait until the currently running action finishes before the next one starts.
- **Concurrent**: Actions execute simultaneously up to a configurable maximum concurrency limit.

---

## Default & Custom Queues

- **Default Queue**: The default execution queue used by standard actions.
- **Custom Queues**: Create dedicated queues for specific alert types (e.g., an `AudioAlerts` queue with blocking enabled, a `TTSQueue` with a single worker, or a `BackgroundTasks` queue with high concurrency).

---

## Queue Controls

- **Pause / Resume**: Click the Play/Pause button on any queue row to halt execution without dropping queued actions. While paused, incoming actions will accumulate in the **Pending** column and resume once unpaused.
- **Clear Pending**: Discard waiting actions in an overloaded queue.
- **Blocking & Max Concurrent**: Configure whether the queue enforces one-at-a-time execution or processes multiple items in parallel.
- **Statistics**: Monitor pending, executing, and total completed actions in real time.

