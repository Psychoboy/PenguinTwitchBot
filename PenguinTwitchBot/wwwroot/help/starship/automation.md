# Starship Triggers & Actions

Starship Expedition uses the bot's normal trigger and action system. Attach actions to any trigger under **Manage Actions** (category **Minigames**).

## Triggers

| Trigger | Fires when |
| :--- | :--- |
| Boarding Opened | The crew boarding window opens |
| Crew Joined | A viewer joins the crew (with `!sj`, or automatically by voting) |
| Event Opened | A crew event opens for voting |
| Event Step Advanced | A multi-step event moves on (after a scan or securing the area) |
| Event Resolved | An event finishes, whatever the outcome |
| Event Succeeded | An event's outcome roll succeeds |
| Event Failed | An event's outcome roll fails; configured loss may be zero |
| Crew Reward Banked | A crew member's payout is settled (once per payout) |
| Risk Succeeded / Failed | A viewer who confirmed a risky choice wins or loses (only events that offer one) |
| Voyage Ended | The voyage returns or is aborted |

## Variables

| Variable | Available on | Description |
| :--- | :--- | :--- |
| `%Game%`, `%ExpeditionId%`, `%Leg%`, `%LegCount%`, `%Sector%`, `%CrewCount%` | All | Voyage information |
| `%ShipSystemsEnabled%`, `%Hull%`, `%Supplies%` | All | Integrated-resource flag and current shared resources; resource numbers are empty for legacy/disabled voyages |
| `%PendingSuccessBonusPercent%`, `%ActiveSuccessBonusPercent%`, `%ReturnReason%` | All | Next/current bonus in percentage points; `HullDepleted` for emergency return, otherwise empty |
| `%HullBefore%`, `%HullAfter%`, `%HullChange%`, `%SuppliesBefore%`, `%SuppliesAfter%`, `%SuppliesChange%` | Resolved, Succeeded, Failed | Whole-event changes, including all steps; opening values are after travel, so travel is excluded; empty without ship context |
| `%EventBaseSuccessChancePercent%`, `%EventSuccessModifierPercent%` | Resolved, Succeeded, Failed | Frozen base chance and resource modifier (shortage plus Continue), before choice modifiers |
| `%User%` | Crew Joined, Risk triggers | The viewer involved |
| `%EventTitle%`, `%EventType%` | Event triggers | Which event it was |
| `%Choices%` | Event Opened, Step Advanced | Choices available |
| `%Step%`, `%StepNote%`, `%RevealedRarity%` | Event Step Advanced | What the last choice did |
| `%Path%` | Step, Resolved, Succeeded, Failed | Choices made, in order |
| `%Outcome%`, `%Rarity%` | Resolved, Succeeded, Failed | Result of the roll and the find's rarity |
| `%PointType%`, `%RewardTotal%`, `%LostTotal%` | Resolved, Succeeded, Failed | First outcome currency and aggregate amounts across currencies |
| `%AffectedCount%` | Succeeded, Failed | Number of distinct viewers with a nonzero earned or lost amount, across all currencies |
| `%RewardParticipantCount%` | Resolved | Number of distinct viewers with a positive earned amount |
| `%Summary%` | Resolved, Voyage Ended | Ready-made sentence for chat |
| `%Story%` | Resolved, Succeeded, Failed | Generated story of what happened, using the names of the viewers who chose each option |
| `%Subject%` | Event Opened, Step Advanced, Resolved, Succeeded, Failed | Generated name of the ship, signal or hazard (for example `the ISV Quiet Meridian`) |
| `%EventsSucceeded%`, `%EventsFailed%`, `%Result%`, `%Duration%` | Voyage Ended | Voyage totals |
| `%Amount%` | Crew Reward Banked | Amount banked |

## Suggested messages

- Event Resolved: `%Summary%`
- Event Succeeded: `%EventTitle%: %Story%`
- Event Failed: `%EventTitle%: %Story%`
- Event Step Advanced: `%StepNote% Options: %Choices%`
- Voyage Ended: `%Summary%`

The bot already announces these in chat by default; use triggers for extra alerts, sounds or scene changes.

## Custom Event Context

Custom events use these same event triggers, not a separate trigger per definition. `%EventType%` is the stable custom event ID and `%EventTitle%` is its title. A duplicated/imported event receives a new ID. Story tokens such as `{subject}` belong in event templates; action messages use `%Subject%`, `%Story%` and the other runtime variables above.

Event Resolved fires once for the group, including a passed or non-rewarding event. On integrated voyages, Event Succeeded/Failed follow the success/failure outcome even without enabled point rewards. Passing does not fire either outcome trigger. Legacy voyages retain their previous behavior. Step Advanced fires after a continuing choice. `%Summary%` is available on Resolved and Voyage Ended, not Succeeded/Failed.

With several currencies, do not describe `%RewardTotal%` as an amount of `%PointType%`: the total is aggregate while the name is the first outcome currency. Use the Resolved `%Summary%` for grouped chat feedback, or Crew Reward Banked for the actual per-user/per-currency permanent payout. Risk triggers fire once per viewer and aggregate that viewer's earned/lost amounts across currencies; use them only for events that genuinely offer that older personal-choice path. Custom editor events do not.

## Reward Profile Actions

The **Reward Profiles** tab can attach existing actions directly to a successful rarity tier or to the profile's failure block. These use the normal action queue, not a separate executor. Standard event triggers remain independent; attaching the same action in both places can run it twice.

- **On Success / On Failure:** one dispatch per configured row and event, never once per crew member. At most ten profile actions are selected per event.
- **After Bank:** waits for all payouts in the station, return or abort settlement to succeed. Repeated references to the same action are combined into one dispatch per settlement.
- Variables include `%OperationId%`, `%Rarity%`, `%EventTitle%`, `%EventType%`, `%Path%`, `%Outcome%`, `%RewardTotal%`, `%LostTotal%`, `%PointType%`, `%Subject%` and `%Story%`. With several currencies, totals are numeric aggregates and `%PointType%` identifies the first outcome currency; use individual payout triggers for currency-specific reactions.
- After-bank actions also receive `%BankedTotal%`, `%BankedCrewCount%` and `%VoyageResult%` (`Returned`, `Aborted`, or `InProgress` at a station).
- Profile actions capture the resource and whole-event change variables above when the outcome resolves. After-bank actions retain that event context rather than substituting a later voyage's resources or return reason. Profile actions still require an enabled reward profile; normal event triggers do not.
- Dispatch is **at most once**: the bot saves its operation claim before attempting to queue the action. A crash during dispatch can skip an alert, but recovery never repeats a claimed dispatch. Missing actions or queue failures are logged and do not block points or banking.

## Action: Starship Show Status Widget

The status widget fades out while the ship is cruising. Add **Starship Show Status Widget** (Minigames) to any action to bring it back for a set time:

- **Show Duration (Seconds)**: 1-3600. Supports variables.
- If the voyage is still cruising when the time runs out, the widget fades out again.
- It does nothing when no voyage exists.

Because it is a normal action, you can run it from a command, channel point reward, timer or hotkey.
