# Stream Timer Guide

The **Stream Timer** allows you to manage countdowns, countup timers, subathons, speedrun timers, and break countdowns on your stream overlay.

---

### Setup & Playback Controls

- **Display Modes**:
  - **Count Down**: Decrements time toward zero (ideal for BRB screens, starting soon, subathons, and deadlines).
  - **Count Up**: Increments time upward starting from zero or a designated baseline (ideal for speedruns or stream uptime tracking).
- **Time Formats**:
  - Input time in raw seconds (e.g. `90`, `300`, `3600`) or in standard timestamp format (`hh:mm:ss` such as `00:05:00` or `01:30:00`).
- **Apply vs. Start**:
  - **Apply (don't start)**: Sets the timer to the entered duration on your overlay while keeping it paused.
  - **Start**: Resumes or starts counting in the active direction.
  - **Stop**: Pauses the running timer immediately.
  - **Reset to zero**: Clears the timer to `00:00:00`.
- **Overlay Widget Integration**:
  - Ensure you have added the **Timer** widget to your active overlay layout in the [Overlay Editor](/overlay/editor) so viewers can see it on stream.

