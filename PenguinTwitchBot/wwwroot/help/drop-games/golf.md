# Golf (Chip & Putt) Minigame

**Golf (Chip & Putt)** is a non-intrusive edge-strip minigame where viewers chip and putt custom golf balls across an invisible fairway toward a compact putting green with a sunken cup and flagstick.

## Enable the Game

Golf is off by default. Use the **Enable Golf** switch beside the page title and save settings to accept new chat commands, automation shots, and rounds. When disabled, new chat play commands are ignored and logged only when no timed round is active; commands remain accepted during an active round. Turning it off cancels queued rounds, while the active round continues. Stop controls remain available.

---

## Viewer Chat Commands

- `!golf` — Chip a golf ball using a random default power (`DefaultPowerMin`–`DefaultPowerMax`) and your theme or channel emote.
- `!golf [power]` — Chip or putt with a specific swing power from `1` to `100` (e.g., `!golf 72`).
- `!golf [power] [emote]` — Chip or putt with a specific power and custom emote decal (e.g., `!golf 72 PenguinHype`).
- `!golf [emote]` — Leave out the power number to swing with a random default power while still using your chosen emote (even alphanumeric emotes like `!golf sptv318`).
- `!ball` or `!golf ball` — Check the current condition of your ball: lie, distance from the cup/pin, current club (Driver, Wedge, or Putter), strokes remaining, and current green condition (e.g., `!ball` or `!ball @username`).
- `!putt` / `!chip` / `!swing` — Built-in command aliases for `!golf`.

### Golf Clubs & Power Scaling
- **Driver (Stroke 1)**: Long-range tee shot club calibrated for fairway carry onto the green (~84yd at 78 power; up to ~115yd at 100 power). Tunable via `DriverPowerMultiplier`.
- **Wedge (Stroke 2+, Off Green)**: Approach chip club with high loft and soft check roll (up to ~60yd at 100 power; ~30yd at 50 power). Tunable via `WedgePowerMultiplier`.
- **Putter (Stroke 2+, On Green)**: Ground putting roll for smooth precision on the putting green (up to ~25yd across the green). Tunable via `PutterPowerMultiplier`.

### Moderator Session Commands
- `!golf start [seconds]` — Start a timed Golf round.
- `!golf stop` — End the current Golf round early.

---

## Non-Intrusive Overlay Design

- **Invisible Fairway by Default (`BoardAlpha = 0.0`)**: The fairway strip is 100% transparent with no border lines so your stream content remains completely unobstructed.
- **Compact Putting Green (`TargetAlpha = 0.45`)**: Only the compact circular/oval putting green and flagstick at the target end of the strip are drawn, with subtle yardage rings (`50+`, `75+`, and the `100` cup).
- **2.5D Parabolic Flight Arc & Turf Roll**: Tee and chip shots launch into a parabolic arc with a ground shadow tracking directly below, bounce 1–2 times with a seasonal turf/divot puff, and transition smoothly into ground roll toward the cup.

---

## Closest-to-the-Pin Scoring & Cup Capture

1. **Sunk in the Cup (Configurable Maximum Base Score; default `100`)**:
   - Balls that cross the cup within the capture radius at a controlled putt/roll speed drop into the hole (`IsBullseye = true`) and receive the configured maximum base score.
   - **Lip-Out Physics**: Hitting the cup way too fast (`> MaxCaptureSpeed`) causes the ball to catch the rim, deflect at a slight angle, and roll past!
2. **Resting on the Putting Green (Base Score from maximum down to `1`)**:
   - Balls that stop on the green outside the cup earn continuous proximity points, scaling from the configured maximum near the cup down to **1 pt** on the outer fringe.
3. **Off the Green (`0 pts`)**:
   - Shots that stop short on the invisible fairway or roll off the back of the green score `0 pts`.

---

## Single-Stroke vs. Multi-Stroke Mode

- **Single-Stroke Mode (`MaxStrokesPerPlayer = 1`, Default)**:
  - Each viewer gets **1 shot** per entry—a pure Closest-to-the-Pin / Hole-in-One challenge.
- **Multi-Stroke Mode (`MaxStrokesPerPlayer = 2..5`)**:
  - If a viewer's shot does not go in the cup and they have strokes remaining, their ball stays at its resting spot on the overlay (`Stroke 1/3`).
  - Typing `!golf [power]` again putts or chips from that exact spot toward the flag!
  - **Stroke Penalty (`StrokePenaltyPoints`, Default `10`)**: Each stroke after the first deducts `StrokePenaltyPoints` from the final score when the ball is holed or when the stroke limit is reached:
    $$\text{Final Score} = \max(0,\; \text{BaseScore} - (\text{StrokesUsed} - 1) \times \text{StrokePenaltyPoints})$$
   - **Maximum Base Score (`MaxScore`, Default `100`)**: Configure the cup's maximum base score from `1` to `1000`. Green proximity scales to `1` at the fringe; stroke penalties never lower the final score below `0`.

## Outcome Automations

Use **Outcome Automations** on the Golf game page to link an existing action or create a new one for Win, Bullseye, Lose, or round events. Add a Manage Points sub-action for the selected point type; `%Score%` awards the final score, and `$math(%Score% * 2)$` applies a multiplier. Edit the linked action in [Manage Actions](/actions/manage) to add more sub-actions or point types. A hole-in-one fires both Bullseye and Win, so linking the same action to both can award twice.

