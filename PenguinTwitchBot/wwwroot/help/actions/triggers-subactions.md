# Triggers & Sub-Actions Reference

Penguin Twitch Bot connects incoming channel events to automation pipelines using Triggers and Sub-Actions.

---

## Triggers

Triggers define the circumstances that initiate an action:

- **Twitch Events**: Follows, subscriptions, resubscriptions, gift subs, raids, cheers (Bits), channel point custom rewards, hype trains, and stream online/offline.
- **Chat Commands & Keywords**: Custom prefixes, specific keywords detected in chat messages, or user-level filters (moderator, subscriber, VIP, broadcaster).
- **Timer Groups**: Scheduled ticks from configured background timers.
- **Fishing Events**: Fish catches, item breakage, accidents, and tournament starts/catches/conclusions.
- **Hotkeys**: Global or local keyboard combinations pressed by the broadcaster.
- **Websocket / API**: Remote signals received from external stream deck utilities or webhooks.

---

## Sub-Actions

Sub-actions are the modular building blocks executed when an action fires:

### Stream & Broadcasting
- **OBS Studio**: Switch scenes, toggle source visibility, mute/unmute audio inputs, start/stop recording or streaming, set filter parameters.
- **Twitch Chat**: Send messages, reply to the triggering user, send whispers, announce in chat.
- **Text-to-Speech (TTS)**: Speak text using configured local or cloud voices.

### Overlay & Visual Effects
- **Overlay Alerts**: Send styled alerts, images, animations, or video to overlay browser sources.
- **Fireworks**: Trigger real-time particle firework bursts on the overlay (used by sample alert actions).
- **Stream Timers**: Start, pause, resume, or adjust overlay countdowns and countup timers.

### Variables & Logic
- **Set Variable**: Set a local action variable dynamically for use by downstream subactions in the pipeline.
- **Get Global Variable**: Copy a persistent global database variable into a local action variable for use in `%variable%` replacements.
- **Counters**: Read, increment, decrement, or adjust multi-counters into action variables (e.g. `%counter_<name>%`).
- **Logic: If/Else**: Branch execution based on comparisons between variables, text, numbers, or Twitch rank levels. Subactions inside True and False branches inherit all variables from preceding steps.
- **Delays & Pauses**: Introduce millisecond or second delays between sub-action steps.
- **Execute Action**: Trigger child or companion actions. Variables and passed parameters (`%Args%`, `%TargetUser%`) are forwarded to the child action.
- **Timer Control**: Enable or disable specific Timer Groups dynamically using `TimerGroupSetEnabledState`.

### Games & Economy
- **Viewer Points**: Award, deduct, or check loyalty points for the triggering user or target (`%TargetPoints%`).
- **Wheel Spin & Raffles**: Trigger giveaways, check raffle ticket counts, and draw winners.
- **Fishing & Tournaments**: Mini-game actions that populate catch details (`%fish_name%`, `%fish_weight%`, `%fishing_tournament_name%`).

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
  1. **Caller Actions**: If this action is called by another action via `Execute Action`, the caller's variables and passed arguments (`%Args%`, `%TargetUser%`) are inherited.
  2. **Triggers**: Command arguments (`%Args%`, `%TargetUser%`), Default Command events (e.g., wheel spins `%WheelSpinResult%`, `%WinningLabel%`, `%WinningMessage%`, `%WheelName%`; gamble `%JackpotAmount%`, `%WinAmount%`, `%RolledValue%`; roll dice `%Dice1%`, `%Dice2%`; defuse wires `%ChosenWire%`, `%CorrectWire%`; slots `%Emote1%`, `%Emote2%`, `%Emote3%`; death counters `%NewCount%`, `%OldCount%`), Twitch event payloads (`%Bits%`, `%Tier%`, `%RewardTitle%`), or fishing tournament data.
  3. **Preceding SubActions**: Steps executed prior to the current step (e.g., `Set Variable`, `Get Global Variable`, `Counter`, `RandomInt`, `CheckPoints`).
- **Global Variables**: Persistent database global variables configured in [Global Variables](/actions/globalvariables) are discoverable in the reference, but are not automatically available as runtime template tokens; load them into a local action variable with the `Get Global Variable` subaction before template use.
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
