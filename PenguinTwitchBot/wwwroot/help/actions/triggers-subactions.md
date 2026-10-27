# Triggers & Sub-Actions Reference

Penguin Twitch Bot connects incoming channel events to automation pipelines using Triggers and Sub-Actions.

---

## Triggers

Triggers define the circumstances that initiate an action:

- **Game Outcomes & Default Commands**: Configure Win, Bullseye, Podium, Lose, Popped, Knockout, Race Match Concluded, UFO Claw Result, Starship Expedition events, and round lifecycle events from a game's **Outcome Automations** panel, or directly in [Manage Actions](/actions/manage). The game wizard creates regular actions and default-command trigger records; those triggers are the unified outcome automation path.
- **Twitch Events**: Follows, subscriptions, resubscriptions, gift subs, raids, cheers (Bits), channel point custom rewards, hype trains, ad breaks, charity donations, shoutouts, and stream online/offline.
- **Chat Commands & Keywords**: Custom prefixes, specific keywords detected in chat messages, or user-level filters (moderator, subscriber, VIP, broadcaster).
- **Timer Groups**: Scheduled ticks from configured background timers.
- **Fishing Events**: Fish catches, item breakage, accidents, and tournament starts/catches/conclusions.
- **Hotkeys**: Global or local keyboard combinations pressed by the broadcaster.
- **Websocket / API**: Remote signals received from external stream deck utilities or webhooks.

---

## Sub-Actions

Sub-actions are the modular building blocks executed when an action fires:

### Stream & Broadcasting
- **OBS Studio**: Switch scenes (`ObsSetScene`), toggle source visibility (`ObsSetSourceVisibility`), mute/unmute audio inputs (`ObsSetSourceMuteState`), set audio tracks (`ObsSetSourceAudioTrackState`), change browser source URLs (`ObsSetBrowserSourceUrl`), update text/image/media/color sources (`ObsSetText`, `ObsSetImageSourceFile`, `ObsSetMediaSourceFile`, `ObsSetColorSourceColor`), control media playback (`ObsSetMediaState`), toggle scene/source filters (`ObsSetSceneFilterState`, `ObsSetSourceFilterState`), and trigger OBS hotkeys (`ObsTriggerHotkey`).
- **Twitch Chat & Audio**: Send messages (`SendMessage`), reply to the triggering user (`ReplyToMessage`), play local sound effects (`PlaySound`), or speak text via **Text-to-Speech (TTS)** (`Tts`).
- **Channel Points & Commands**: Enable, disable, or pause Twitch Channel Point rewards (`ChannelPointSetEnabledState`, `ChannelPointSetPausedState`), toggle command disabled states (`ToggleCommandDisabledState`), or reset command cooldowns (`ResetCooldowns`).

### Overlay & Visual Effects
- **Overlay Alerts**: Send styled alerts, images, animations, or video to overlay browser sources (`Alert`), with optional alert channel routing.
- **Fireworks**: Trigger real-time particle firework bursts on the overlay (used by sample alert actions).
- **Stream Timers**: Start, stop, add time to, or remove time from named overlay timers (`OverlayTimerStart`, `OverlayTimerStop`, `OverlayTimerAddTime`, `OverlayTimerRemoveTime`).

### Variables, Utilities & AI
- **Set / Get Variables**: Set a local action variable (`SetVariable`) or read/write persistent global database variables (`GetGlobalVariable`, `SetGlobalVariable`) for use in `%variable%` replacements.
- **Counters**: Read, increment, decrement, reset, or set named counters (`MultiCounter`) and expose them as `%counter_<name>%`.
- **Logic: If/Else & Break**: Branch execution (`LogicIfElse`) based on comparisons between variables, text, numbers, or Twitch rank levels, or halt execution early (`Break`). Subactions inside True and False branches inherit all variables from preceding steps.
- **Delays & Loops**: Introduce millisecond or second delays (`Delay`), iterate over active viewers (`ForEachViewer`), or pick random viewers (`SelectRandomViewers`).
- **Execute Action / Command**: Trigger child actions (`ExecuteAction`), default commands (`ExecuteDefaultCommand`), or custom commands (`ExecuteCommand`). Variables and passed parameters (`%Args%`, `%TargetUser%`) are forwarded to child actions.
- **Timer Control**: Enable or disable specific Timer Groups dynamically (`TimerGroupSetEnabledState`).
- **External API, File I/O & OpenAI**: Fetch JSON/text from HTTP endpoints (`ExternalApi`), write or append local files (`WriteFile`), or generate dynamic AI responses (`OpenAi`).

### Games, Minigames & Economy
- **Manage Points**: Add, Remove, Set, or Add Active for a selected point type (`ManagePoints`). Add/Remove/Set target the triggering user by default; Add Active follows the points system's active-viewer policy. Amounts support variables such as `%Score%` and formulas such as `$math(%Score% * 2)$`. Remove leaves balances unchanged when the viewer has insufficient points; balances cannot become negative.
- **Check & Gift Points**: Read a viewer's point balance into variables (`CheckPoints`) or transfer points between viewers (`GiftPoints`).
- **Game Outcome Automations**: **Set Up Automation** on any minigame page creates a normal editable action and attaches it to the selected default-command outcome trigger. Add multiple `ManagePoints` sub-actions for multiple currencies, or edit the action anytime in [Manage Actions](/actions/manage).
- **Stream Overlay Minigames**:
  - **Dedicated Session & Lobby Sub-Actions**: Start (or queue) and stop rounds per game via **Start Parachute Session** / **Stop Parachute Session**, **Start Plinko Session** / **Stop Plinko Session**, **Start Balloon Session** / **Stop Balloon Session**, **Start Curling Session** / **Stop Curling Session**, **Start Golf Session** / **Stop Golf Session**, **Start Race Lobby** / **Stop Race Session** (with mode, track, and lap overrides), or **Stop All Minigame Sessions**.
  - **Parachute Drop**, **Parachute Droplets**, & **Parachute Sky Rockets**: Spawn parachutists, nudging droplets, or canopy-burning fireworks rockets.
  - **Plinko Drop**: Drop Plinko tokens for viewers.
  - **Balloon Ascent** & **Balloon Hazards**: Launch rising balloons or spawn sky hazards (droplets, anvils, wind gusts, sharp darts).
  - **Curling / Shuffleboard Slide** & **Golf Chip & Putt Shot**: Launch stones or strike golf shots with custom or calibrated power, decals, and themes.
- **Starship Expedition**: Start or stop voyages (`StarshipStartExpedition`, `StarshipStopExpedition`) and temporarily display the status or event overlay widgets (`StarshipShowStatus`, `StarshipShowEvent`).
- **UFO Claw Machine**: Show, hide, grab, open/close viewer entries, or reset removed winners (`UfoClawControl`).
- **Wheel Spin & Raffles**: Start/end raffles, enter viewers, set winner counts/awards (`RaffleStart`, `RaffleEnd`, `RaffleEnter`, `RaffleGetEntryCount`, `RaffleSetTotalAward`, `RaffleSetWinnerCount`), and draw giveaway prizes (`GiveawayPrize`).
- **Fishing & Tournaments**: Cast lines (`Fishing`), grant shop items (`FishingGiveItemToPlayer`), modify fishing attributes (`FishingModify`), and run tournaments (`FishingTournamentStart`, `FishingTournamentEligibleCatch`, `FishingTournamentEnd`).

---

## Context-Aware Variable Reference

Penguin Twitch Bot includes an interactive **Available Variables Reference** to make configuring subactions effortless:

### How to Access the Reference
1. **Action SubActions Toolbar**: Click the **Available Variables** button in the SubActions header of any action in [Manage Actions](/actions/manage) to view all variables available across the action.
2. **SubAction Wizard (Step 3: Configure)**: When adding or editing a subaction, an expandable **Available Variables** drawer appears at the bottom of the configuration panel, showing the exact variables available at that point in the sequence.
3. **Logic: If/Else Editor**: Click **Available Variables** in the branch editor dialog to see variables available to the conditional branches.

### Variable Discovery & Overwrite Rules
- **Unique & Case-Insensitive**: Template variables reside in a unified, case-insensitive dictionary. Every variable appears exactly once.
- **Sequential Pipeline Precedence**: Variables are resolved in chronological order:
  1. **System Globals**: Automatic built-in variables (`%bot%`, `%streamer%`, `%user%`, `%date%`, `%time%`, `%ticks%`, `%random%`, and custom ranges like `%random(1, 10)%`) available everywhere in template fields. Date and time use local culture formatting, with `%time%` including seconds.
  2. **Caller Actions**: If this action is called by another action via `Execute Action`, the caller's variables and passed arguments (`%Args%`, `%TargetUser%`) are inherited.
  3. **Triggers**: Command arguments (`%Args%`, `%TargetUser%`), Default Command events (minigame scores `%Score%`, `%PointsAwarded%`, `%Placement%`, `%LapTime%`, `%BucketLabel%`, `%IsBullseye%`, `%IsHoleInOne%`; wheel spins `%WheelSpinResult%`, `%WinningLabel%`; UFO Claw `%UfoClawResult%`, `%PrizeSetName%`; Starship Expedition `%EventTitle%`, `%Outcome%`, `%Winnings%`; gamble/slots/roll/heist/defuse variables), Twitch event payloads (`%Bits%`, `%Tier%`, `%RewardTitle%`), or fishing tournament data.
  4. **Preceding SubActions**: Steps executed prior to the current step (e.g., `Set Variable`, `Get Global Variable`, `Counter`, `RandomInt`, `CheckPoints`).
- **User-Defined Global Variables**: Persistent global variables configured in [Global Variables](/actions/globalvariables) only appear in the Available Variables reference when loaded into the action via a preceding `Get Global Variable` subaction. Once loaded, they appear with token chip formatting and copy controls just like any other action variable.
- **Source Indication**: If a later subaction (such as Step 2 `Set Variable`) re-assigns an existing variable (e.g., `%TargetUser%`), the variable reference shows the exact step that last set the value (e.g. `Step 2`).
- **Click-to-Copy**: Click on any variable token chip to copy `%VariableName%` directly to your clipboard.

### Catch Block Error Handling
When configuring subactions in the **Catch SubActions** section (error handler), the special variable `%ActionErrorMessage%` is available, containing the error message that caused the main sequence to fail.

### Math Expressions
You can perform mathematical operations inside any text field using the `$math(...)` expression helper:
```text
$math(%points% + 100)
$math(%counter_deaths% * 2)
```
