# Racing & Destruction Derby Minigame

The **Racing Minigame** turns the outer border, screen edges, or an arena bowl of your stream into an interactive raceway or demolition derby where viewers compete in mass-start lobbies, multi-heat tournaments, or solo hot-lap time trials!

## Enable the Game

Racing is **off by default**. Turn on **Enable Racing** at the top of `/games/racing` and click **Save All Settings** before opening lobbies or accepting `!race` / `!timetrial` commands. Disabling the game prevents new joins and clears any queued race sessions.

---

## Game Modes

### 1. Grand Prix (`GrandPrix`)
- **Open Lobby Countdown**: When a race starts (either manually, via sub-action, or automatically when a viewer types `!race`), a lobby countdown timer opens (`LobbyDurationSeconds`, default `45s`).
- **Grid Lineup**: Viewers join by typing `!race [emote]` (or aliases `!racing`, `!kart`, `!grandprix`) in chat. Each racer appears in the starting grid with their Twitch username color and custom emote or theme kart.
- **Green Flag & Laps**: Once the lobby timer expires (or a moderator runs `!race launch` / `!race go`), the starting lights count down and all racers launch simultaneously for the configured number of laps (`TotalLaps`, default `3`).
- **Photo Finish & Podium**: Final placements (`1st`, `2nd`, `3rd`, etc.) and lap times are determined on the overlay and reported back to the bot to award points and fire triggers.

### 2. Last Kart Elimination (`Elimination`)
- **Lap-by-Lap Elimination**: Works like Grand Prix, except at the end of every lap, the racer in **last place** is eliminated from the track until only the top finishers remain!

### 3. Destruction Derby (`DestructionDerby`)
- **Full-Contact Crash Arena**: Instead of racing laps around a circuit, karts battle inside an arena (best paired with the **Derby Bowl** track layout).
- **Vehicle Health & Knockouts**: Every kart starts with `100 HP`. High-speed collisions inflict damage based on relative impact speed and `DerbyDamageMultiplier`, spawning sparks, smoke, fire, and debris particles.
- **Totaled Karts & Final Standings**: When a kart reaches `0 HP`, it is **totaled** (`%IsTotaled% = True`), crediting the attacker with a knockout (`%KnockoutCount%`) and firing the **Destruction Derby Knockout** trigger. The last surviving kart (or highest HP remaining when the match timer expires) wins 1st place!

### 4. Multi-Heat Tournaments (Automatic Bracket Mode)
- Whenever more viewers join the lobby than can fit on the track at once (`MaxRacersPerHeat`, default `8`, up to `MaxLobbyRacers`, default `32`), the bot automatically organizes a **Multi-Heat Tournament**:
  1. Entrants are divided evenly across qualifying heats (e.g., *Heat 1 of 3*, *Heat 2 of 3*, *Heat 3 of 3*).
  2. The top finishers (or top survivors in Destruction Derby) from each qualifying heat advance to the **Championship Final**.
  3. Points and placement triggers are awarded during the Championship Final, and `%IsTournamentFinal%` is set to `True`.

### 5. Solo Time Trial (`!timetrial` / `!tt`)
- Viewers can run a solo 1-lap hot-lap time trial anytime no mass-start lobby or race is active (`AllowSoloTimeTrial`).
- Fires the **Racing Time Trial Complete** (`Racing.TimeTrialComplete`) trigger under the `timetrial` command with `%LapTime%` and `%WreckCount%`.

---

## Track Layouts

1. **Perimeter Ring (`Perimeter`)**: Follows all 4 outer edges of your 1920x1080 stream canvas so the center of your gameplay remains completely unobstructed.
2. **Edge Strip Oval (`EdgeOval`)**: A compact stadium oval along a single screen edge (`Bottom`, `Top`, `Left`, or `Right`).
3. **Figure-8 Intersection (`Figure8`)**: A crisscrossing circuit with a center intersection where leading and trailing karts can collide!
4. **Drag / Sprint Strip (`DragStrip`)**: A straight point-to-point sprint along the selected screen edge.
5. **Derby Bowl (`DerbyBowl`)**: A circular demolition arena with outer containment walls designed for **Destruction Derby** mode.

---

## Physics & Track Hazards

- **Drafting / Slipstream Boost**: Karts trailing closely behind another racer gain an aerodynamic speed burst to set up overtakes.
- **Rubber-Banding**: Optional catch-up assistance (`RubberBandingStrength`) keeps packs tight for exciting photo finishes.
- **Vehicle Bumping & Spinouts**: Karts trading paint can bump rivals wide or trigger brief spinouts (`CollisionSpinoutChance`).
- **Track Debris & Oil Slicks**: Optional oil slicks/bananas (`EnableDebrisHazards`, `DebrisDensity`) cause karts that hit them to spin out, incrementing `%WreckCount%`.

---

## Session Queueing

Just like Parachute Drop and Plinko, **Racing sessions automatically queue** if a new race session is triggered (via `Start Minigame Session` sub-action, `!race start`, or `!race derby`) while a lobby or race is already in progress:
- Queued sessions preserve their requested lobby duration, theme override, game mode, track layout, and lap count.
- Once the active race (or tournament final) finishes and completes its podium celebration, the next queued race lobby opens automatically.
- Stopping a race via `!race stop`, `!race cancel`, the dashboard **Force Stop** button, or the `Stop Minigame Session` sub-action clears all queued sessions and ends the active race immediately.

---

## Chat Commands

### Viewer Commands
| Command | Description |
| :--- | :--- |
| `!race` (aliases: `!racing`, `!kart`, `!grandprix`) | Joins the open race lobby (or auto-starts a lobby if **Auto-Start Lobby on `!race`** is enabled). |
| `!race <emote>` | Joins the race using a specific Twitch, 7TV, BTTV, or FFZ emote as your kart driver. |
| `!timetrial [emote]` (alias: `!tt`) | Runs a solo 1-lap hot-lap time trial when no mass race is active. |

### Moderator / Streamer Commands
| Command | Description |
| :--- | :--- |
| `!race start [seconds]` | Opens a new race lobby (or queues one if a race is already active) with an optional custom lobby countdown duration. |
| `!race derby [seconds]` or `!race start derby [seconds]` | Opens (or queues) a **Destruction Derby** lobby on the **Derby Bowl** arena track. |
| `!race launch` or `!race go` | Ends the open lobby countdown early and launches the green flag immediately. |
| `!race stop` or `!race cancel` | Immediately stops the active lobby/race and clears any queued race sessions. |

---

## Points & Outcome Automations

- **Built-in Point Payouts**: Configure automatic point rewards on `/games/racing` for **1st Place** (`WinPointsFirst`), **2nd Place** (`WinPointsSecond`), **3rd Place** (`WinPointsThird`), **Participation** (`ParticipationPoints`), and **Destruction Derby Knockout Bonus** (`DerbyKnockoutPoints`). The total points awarded to a racer are passed to triggers in both `%PointsAwarded%` and `%Score%`.
- **Outcome Automations Wizard**: At the bottom of `/games/racing`, you can configure one-click automated actions (such as chat announcements, sound effects, or extra point bonuses) for both **Mass Race & Derby Outcomes (`!race`)** and **Solo Time Trial Outcomes (`!timetrial`)**.
- **Trigger Filters**: When attaching actions to Racing triggers (`Racing Win`, `Racing Podium`, `Racing - Racer Finished`, `Racing - Race Match Concluded`, `Racing - Destruction Derby Knockout`, `Racing Collision / Wreck`), you can filter by:
  - **Placements** (e.g., `1` or `1,2,3`)
  - **Game Mode** (`GrandPrix`, `Elimination`, `DestructionDerby`)
  - **Track Type** (`Perimeter`, `EdgeOval`, `Figure8`, `DragStrip`, `DerbyBowl`)
  - **Minimum Knockouts** (e.g., at least `2` KOs)
  - **Tournament Final Only** (only fire on the Championship Final heat)
