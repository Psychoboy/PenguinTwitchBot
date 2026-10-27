# Minigame Triggers & Sub-Actions

All six stream overlay minigames (**Parachute Drop**, **Plinko Bounce**, **Balloon Ascent**, **Curling / Shuffleboard**, **Golf (Chip & Putt)**, and **Racing & Destruction Derby**) integrate deeply with PenguinTwitchBot's **Actions & Sub-Actions** engine.

---

## Sub-Actions (Under *Minigames*)

Add these sub-actions inside any custom Action (for example, triggered by a Channel Point Redemption, Twitch Cheer/Bits, Sub, or custom command):

| Sub-Action | Description |
| :--- | :--- |
| **Start Drop Game Session** | Opens a timed overlay round (or queues it if a round is already active) for **Parachute Drop**, **Plinko Bounce**, **Balloon Ascent**, **Curling / Shuffleboard**, **Golf (Chip & Putt)**, or **Racing (`!race`)**. Supports custom duration and seasonal theme overrides. |
| **Stop Drop Game Session** | Immediately ends the active round and clears any queued sessions for the selected minigame. |
| **Parachute Drop** | Spawns a parachutist for `%User%` (or a custom username) with an optional emote/image override and seasonal theme override. |
| **Parachute Droplets** | Spawns interactive falling droplets (`1`–`25`) on the Parachute overlay that nudge parachutists mid-air. |
| **Parachute Sky Rockets** | Fires interactive fireworks rockets (`1`–`10`, `Targeted`, `Random`, or `Barrage`) into the sky that knock back parachutists or burn their canopy (`CanopyKillChance`) into free-fall. |
| **Plinko Drop** | Drops a Plinko token for `%User%` down the pegboard with optional emote/image and theme overrides. |
| **Balloon Ascent** | Releases a rising balloon for `%User%` toward the top sky arch with optional emote/image and theme overrides. |
| **Balloon Hazards** | Spawns interactive sky hazards (`Nudge`, `HeavyPush` anvil, `WindGust`, `DartPop`, or `Random`) on the Balloon Ascent overlay. |
| **Curling / Shuffleboard Slide** | Slides a curling stone or puck down the sheet for `%User%` with optional launch power (`1`–`100` or blank for calibrated random), decal emote/image, and theme override. |
| **Golf Chip & Putt Shot** | Strikes a golf ball toward the putting green for `%User%` with optional swing power (`1`–`100` or blank for calibrated random), ball decal emote/image, and theme override. |

---

## Default Command Triggers

You can attach automated Actions to any minigame outcome either from the **Outcome Automations** card on each game's settings page, or manually via **Actions > Manage Actions > Add Trigger > Default Command**:

### Parachute Drop (`drop`)
- `Parachute Win` (`Parachute.Win`) — Fires when a player lands on the target platform (`Score >= MinimumWinScore`).
- `Parachute Bullseye` (`Parachute.Bullseye`) — Fires when a player lands dead-center (`Score == 100`).
- `Parachute Lose / Miss` (`Parachute.Lose`) — Fires when a player misses the platform or scores below the win threshold.
- `Parachute Rocket Kill` (`Parachute.RocketKill`) — Fires when a sky rocket destroys a player's canopy mid-air.
- `Parachute Start` (`Parachute.Start`) / `Parachute End` (`Parachute.End`) — Fires when a Parachute round starts or ends.

### Plinko Bounce (`plinko`)
- `Plinko Win` (`Plinko.Win`) — Fires when a token lands in a bucket marked `IsWin = true` (supports optional **Bucket Label Filter**).
- `Plinko Lose` (`Plinko.Lose`) — Fires when a token lands in a non-winning bucket.
- `Plinko Start` (`Plinko.Start`) / `Plinko End` (`Plinko.End`) — Fires when a Plinko round starts or ends.

### Balloon Ascent (`balloon`)
- `Balloon Win` (`Balloon.Win`) — Fires when a balloon reaches the sky arch within a winning ring.
- `Balloon Bullseye` (`Balloon.Bullseye`) — Fires when a balloon hits the center bullseye of the sky arch (`Score == 100`).
- `Balloon Pop` (`Balloon.Pop`) — Fires when a sharp dart hazard pops a balloon mid-air.
- `Balloon Lose / Miss` (`Balloon.Lose`) — Fires when a balloon misses the sky arch or pops for `0` points.
- `Balloon Start` (`Balloon.Start`) / `Balloon End` (`Balloon.End`) — Fires when a Balloon Ascent round starts or ends.

### Curling / Shuffleboard (`curl`)
- `Curling Win` (`Curling.Win`) — Fires when a stone comes to rest inside the scoring house rings.
- `Curling Bullseye` (`Curling.Bullseye`) — Fires when a stone stops dead-center on the button (`Score == 100`).
- `Curling Lose / Miss` (`Curling.Lose`) — Fires when a stone falls short (hogged) or slides out the back of the house.
- `Curling Start` (`Curling.Start`) / `Curling End` (`Curling.End`) — Fires when a Curling round starts or ends.

### Golf — Chip & Putt (`golf`)
- `Golf Win (Green Hit)` (`Golf.Win`) — Fires when a golf ball comes to rest on the putting green or is sunk in the cup.
- `Golf Bullseye (Sunk in Cup / Hole-in-One)` (`Golf.Bullseye`) — Fires when the ball drops into the cup (`Score == 100` on stroke 1).
- `Golf Lose (Missed Green / Hazard)` (`Golf.Lose`) — Fires when a shot lands in the water hazard, sand bunker, rough, or out of bounds.
- `Golf Start` (`Golf.Start`) / `Golf End` (`Golf.End`) — Fires when a Golf round starts or ends.

### Racing & Destruction Derby (`race` and `timetrial`)
- `Racing Win (1st Place)` (`Racing.Win`) — Fires for the 1st place winner of a race or Championship Final.
- `Racing Podium (Top 3)` (`Racing.Podium`) — Fires for 1st, 2nd, and 3rd place finishers (note: 1st place fires both `Racing.Win` and `Racing.Podium`).
- `Racing - Racer Finished (Individual Finish)` (`Racing.End`) — Fires for each individual racer as they finish or are totaled (supports placement, mode, track, knockout, and tournament-final filters).
- `Racing - Race Match Concluded (Final Results)` (`Racing.Complete`) — Fires **once** when the entire race (or tournament final) concludes with full podium summary variables.
- `Racing - Destruction Derby Knockout` (`Racing.DerbyKnockout`) — Fires when a driver totals an opponent's kart in Destruction Derby.
- `Racing Collision / Wreck` (`Racing.Wreck`) — Fires when a racer finishes after suffering one or more spinouts/wrecks (`WreckCount > 0`).
- `Racing Lobby Opened` (`Racing.LobbyOpened`) / `Racing Green Flag` (`Racing.Start`) — Fires when the lobby opens or when the green flag drops.
- `Racing Time Trial Complete` (`Racing.TimeTrialComplete` under `timetrial`) — Fires when a solo hot-lap time trial finishes.

---

## Trigger Variables Reference

### Shared Drop, Curling & Golf Variables
| Variable | Description |
| :--- | :--- |
| `%User%`, `%DisplayName%`, `%UserId%` | Username, display name, and Twitch user ID of the player. |
| `%Score%` | Accuracy, ring, green, or bucket score achieved on the play. |
| `%IsWin%` | `true` if the play met the win criteria, otherwise `false`. |
| `%IsBullseye%` | `true` if dead-center bullseye / button / sunk in cup. |
| `%DistanceFromCenter%` | Distance from the center target/pin in percentage points. |
| `%Theme%` | Name of the active seasonal or custom theme. |
| `%Duration%`, `%TotalDrops%`, `%TriggeredBy%` | Round duration, total participants/shots, and starter name on `*.Start` / `*.End` triggers. |
| `%BucketLabel%`, `%BucketIndex%` | *(Plinko)* Label and 0-based index of the bucket landed in. |
| `%DropletHits%`, `%Attacker%`, `%Victim%`, `%ParachuteDestroyed%` | *(Parachute)* Mid-air droplet hits and Sky Rocket knockout details. |
| `%BalloonPopped%` | *(Balloon)* `true` if popped mid-air by a dart hazard. |
| `%Power%`, `%Condition%` / `%IceCondition%`, `%TargetDistance%` | *(Curling)* Delivery power (`1`–`100`), ice condition (`Slick Ice`, `Normal Ice`, `Heavy Frost`), and house distance. |
| `%Power%`, `%StrokeNumber%`, `%BaseScore%`, `%StrokePenalty%`, `%IsHoleInOne%`, `%IsLipOut%`, `%GreenCondition%`, `%PinDistance%` | *(Golf)* Swing power, stroke count, pre-penalty score, stroke penalty, hole-in-one / lip-out flags, green speed, and pin distance. |

### Racing & Destruction Derby Variables
| Variable | Description |
| :--- | :--- |
| `%Placement%` | Final race placement (`1`, `2`, `3`, etc.). |
| `%PointsAwarded%` & `%Score%` | Points awarded to the racer (both variables hold the same value for consistency across games). |
| `%LapTime%` | Finish time or hot-lap time in seconds (e.g., `24.50`). |
| `%WreckCount%` | Number of spinouts or hazard wrecks suffered during the race. |
| `%KnockoutCount%` | Number of opponent karts totaled by this driver in Destruction Derby. |
| `%IsTotaled%` | `True` if the racer's kart was reduced to `0 HP` in Destruction Derby, otherwise `False`. |
| `%RacingMode%` / `%GameMode%` | Active mode (`GrandPrix`, `Elimination`, `DestructionDerby`, or `TimeTrial`). |
| `%TrackType%` | Active track circuit (`Perimeter`, `EdgeOval`, `Figure8`, `DragStrip`, or `DerbyBowl`). |
| `%Attacker%`, `%Victim%` | *(Derby Knockout)* Display name of the driver who scored the KO and the totaled opponent(s). |
| `%Winner%`, `%WinnerUsername%`, `%First%`, `%Second%`, `%Third%`, `%WinnerLapTime%`, `%ResultsSummary%`, `%RacerCount%`, `%TotalKnockouts%`, `%IsTournamentFinal%` | *(Race Match Concluded / `Racing.Complete`)* Summary variables for the completed race or Championship Final. |
