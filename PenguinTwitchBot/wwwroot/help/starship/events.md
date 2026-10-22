# Starship Custom Events

Use **Events & Sectors** on the Starship Expedition page to manage the catalog. The bot owns selection, voting, deadlines, outcomes and banking; event definitions contain data, not scripts.

## First Setup

1. Open an event with its edit icon, use **Add Event**, or duplicate a built-in as a custom draft.
2. Set its wording, availability, subject/art and choices. Select a reward profile if the event should award points. Integrated voyages roll success/failure even without point rewards.
3. Use **Preview Event** to inspect the description, choice effects and story text with a sample seed. Previewing never starts a voyage, sends chat, runs actions or changes balances.
4. **Save Event** validates and immediately persists that event. It does not save unrelated edits in other sections. Drafts survive tab switches, but not closing or reloading the page.
5. Save changes in other sections with the save button in that section. Imported events remain drafts until you choose **Save Event**.
6. Start a new voyage to use the saved catalog. A running voyage retains its departure-time catalog, reward profiles and sector pool, including after a restart.

Apply or discard the current draft before opening another event. Deleting a custom event requires confirmation and cannot leave a route/sector without an eligible event.

## Included Starter Events

These three editable events are added automatically the first time this catalog version is loaded. They do not replace existing IDs, are not duplicated on later loads, and stay deleted if you remove them. There is no extra import step. The JSON links below are available for sharing or making another copy.

| Event | Demonstrates | Availability | Rewards |
| :--- | :--- | :--- | :--- |
| [Courier Relay](/help/starship/examples/courier-relay.json) | A simple branching narrative event | All routes and sectors, weight 1 | No reward profile by default |
| [Survey Cache](/help/starship/examples/survey-cache.json) | Scan, optional stabilization, recovery or pass; named subjects and tier loot | Survey Route in Ternion Belt, weight 2 | References the SalvageSite profile |
| [Debris Crossing](/help/starship/examples/debris-crossing.json) | Balanced, safer or higher-reward choices with different loss exposure | All routes and sectors, weight 1 | References the Hazard profile |

The examples do not choose a point currency or enable an existing reward profile for you. Configure and enable the referenced profile to activate rarity, payouts and point-loss rules. On integrated voyages, success/failure and shared ship effects still resolve without points. A scan only reveals rarity when an enabled reward profile is available. Legacy/disabled ship-system voyages keep their previous outcome behavior.

Survey Cache cannot appear if Ternion Belt is disabled or the crew takes the Direct Route. Its no-vote default is to pass. Debris Crossing defaults to navigating the gaps. Courier Relay defaults to relaying the message, then forwarding the second packet.

## Identity and Availability

- Custom event IDs are unique lowercase slugs, for example `survey-cache`. Existing IDs cannot be renamed; duplicate the event to give it a new identity.
- Built-ins cannot be deleted. Random built-ins allow weights and route/sector filters. Route Choice and Station Stop allow wording/art only; their mechanics and default choices remain fixed.
- A selection weight is relative to other eligible events, not a percentage. Weight 2 is twice as likely as weight 1 when both are eligible. It is not a guarantee that an event will appear in a finite voyage.
- Leaving every route or sector unchecked means unrestricted availability, not disabled. Turn off **Enabled** to remove a random event from future selections.
- The catalog must provide an eligible random event for each route and active sector. Structural route/station events do not satisfy that requirement.
- Keep at least two effective sectors. Disabled built-ins may remain in event filters, but the voyage cannot visit them. Custom names must be unique, including against disabled built-ins.

## Branching Steps

Each narrative event is a directed graph with one start step and one or more end steps. Vote steps offer two to four choices; each choice points to another named step, and multiple choices may share a target. Automatic steps show their narrative and advance immediately without a vote. End steps finish the event and resolve its outcome. All steps must be reachable, the graph cannot contain cycles, an event may contain at most 16 total steps, and no single route may exceed eight steps. Route Choice and Station Stop remain fixed structural events.

The editor shows graph connections, lets you choose a start step, and lists reachable paths in the seeded preview. **Default**, **Seeded Random**, **Risk-Averse**, and **Risk-Seeking** policies in the Test tab simulate different deterministic votes through normal graph transitions.

Each event has a default supplies cost charged once when a step is entered. A step may override that cost; enter `0` to make it free. Costs apply to vote, automatic, and end steps, including the start step. A step that cannot be paid for fails the event instead of opening a vote. The default choice must remain available and affordable.

Each vote step offers two to four choices with IDs unique within that step. Three or four choices use the compact two-column overlay layout. There is no fixed cap on the number of events.

| Field | Meaning |
| :--- | :--- |
| Default Choice | The always-available choice used for a tied vote or no votes at that vote step |
| Success Modifier | Adds -50 to +50 percentage points to success chance; the resulting chance is clamped to 0-100% |
| Reward Multiplier | 10-300% of the configured point reward; later steps combine with earlier multipliers |
| Loss Multiplier | 0-300% exposure against unbanked balances, subject to the profile's loss rules and balance protection |
| Next Step | The named step opened by this choice; targets may be shared to merge branches |
| Reveals Rarity | Shows the event's seeded rarity; requires a reward-profile reference |
| Ends Without Reward | Resolves the event without rewards or losses |

The graph must have an end step, and every branch must eventually reach one. Custom events do not support personal risky-commit/confirmation choices. Existing crew share an ordinary event; late joiners vote immediately but become reward/loss eligible only from the next whole event.

## Ship Effects

For non-structural events, **Event Success Chance (%)** overrides the bound profile's base chance. Leave it empty to use that profile's chance (even if its payouts are disabled), or the ship-system default when no profile is bound. Shortage and choice modifiers apply afterward, clamped to 0-100%.

**Override Success/Failure Ship Effect** enables bounded hull/supplies changes (-100 to +100). Hull gains are capped at the voyage's configured Maximum Hull; supplies remain capped at 100. Leave it off for the displayed default. An explicit zero effect overrides stock damage. Stock failure effects are -25 hull/-10 supplies for Hazard and Debris Crossing, -20/-10 for Salvage and Survey Cache, and -15/-10 for Signal. Negative outcome changes follow accumulated loss multipliers; positive changes are not amplified.

Each choice can also override a **Choice Ship Effect**, including a supply payment of 0-100. That payment must be affordable before voting; a positive supplies gain cannot fund its own payment. The default choice cannot require supplies, and outcome effects cannot require payment. Choice effects apply once when selected. Passing skips outcome effects but still applies a configured direct choice effect.

**Preview Event** shows starting resources, numbered available choices, costs, effective chances, reachable graph paths and potential outcome effects using the same server calculations as the widgets. The seeded route preview follows the selected voter policy and stops at zero hull. No preview pays points or runs actions. Route Choice and Station Stop mechanics remain protected.

Configure the default opening cue for every event type in the Starship **Audio** tab, including custom event types. In the event editor, each event can inherit its event-type default, select a custom cue through the Media Manager, or mute that event. Cues play once when the vote opens, not on each vote or later step. Event JSON exports include the selected media path, not the audio file itself.

## Story Tokens and Media

| Token | Resolved value |
| :--- | :--- |
| `{crew}` | A seeded sample of the viewers who chose the option, or the crew fallback |
| `{crewlist}` | The participating names, shortened for a larger group |
| `{subject}`, `{Subject}` | The selected subject name, unchanged or initially capitalized |
| `{loot}` | This event's wording for the selected tier, or a generic rarity find |
| `{rarity}` | The known rarity name, or `unknown` before it is revealed |
| `{sector}` | The event's actual sector |

Curly-brace story tokens are different from the percent-delimited action variables in the **Triggers & Actions** help tab. Story templates are plain text. Unknown/unbalanced tokens, command-prefixed lines and unsafe content are rejected. Titles, labels, subject names and loot wording are literal text rather than template fields.

Choose a generated subject style or provide a name pool, then choose an object kind. **Event Images** uses the existing media library picker; event-specific images take precedence over theme object images. With no event image, the normal theme/built-in art remains available. Images must be media-library `/media/...` paths or HTTP(S) URLs.

The editor preview uses a sample viewer, seed and the first enabled profile tier. It previews wording and effects, not an authoritative success roll, payout or live rendered OBS scene. Actual success/failure rolls and rarity remain deterministic server decisions.

## Reward Profiles and Limits

Select a shared profile or use **Create Event Reward Profile** to build a new one in the draft. The new profile is applied together with the event and remains editable in Reward Profiles. Each tier can pay multiple currencies and optionally run normal actions. Events reference profiles; they do not carry independent payout amounts that bypass profile rules.

On the **Reward Profiles** tab, the event-type dropdowns bind each built-in event to a shared profile; custom-event bindings are selected in the event editor. **Bulk Edit Rarity Rewards** can change selected minimum, maximum or chance values for an existing currency, or add a missing currency payout to selected profiles at one rarity. Other currencies, tiers, actions and limits are preserved. Preview the changes before applying them to unsaved settings.

Reward minimums, maximums and optional caps accept non-negative values through `long.MaxValue` (9,223,372,036,854,775,807). An empty event/voyage/loss amount cap means unlimited; deleting a point-limit row removes it. Existing numeric caps are preserved, including an explicit zero. Voyage limits count amounts already banked. Unlimited loss amounts still respect the loss percentage and cannot reduce unbanked balances below zero; permanent banked points remain safe. Large totals saturate at `long.MaxValue` rather than wrapping around.

Earn-time eligibility uses the existing active-viewer/subscriber policy when **Only Reward Active Crew** is enabled. Rewards already earned are banked later without a new activity check. Personal payout triggers and group event actions have different scopes; see **Triggers & Actions** before attaching automations.

## Sharing JSON

**Export JSON** downloads one validated event and fills the JSON text field. **Open JSON File** or **Import as Draft** accepts schema version 2, up to 256 KB. Imports always get a new custom ID and never replace an existing event. Version 1 documents are not imported; export or recreate them using the current editor.

Reward profiles, point types and actions are not bundled in an event export. If its profile reference is absent locally, the editor opens an invalid draft so you can select or create a local profile. This placeholder validation does not create a saved profile. Remap the reference, then choose **Save Event**. A new reward profile created in the event editor is saved with that event.

## Troubleshooting

- **No event is eligible:** enable a random event or relax its route/sector filters. Changing weights alone does not fix an empty pool.
- **An example never appears:** check enabled state, route, sector and relative weight. Structural station/route slots are not random-event slots.
- **Scan does not reveal anything:** check that the event references an enabled reward profile with a weighted tier.
- **No points awarded:** check the profile/currency, chance, selected ending branch, earn-time eligibility, late-join sequence and explicit caps. Passing deliberately pays nothing.
- **Changes do not affect the current voyage:** this is intentional snapshot behavior; they apply at the next departure.
- **An import is rejected:** use a single-event schema-version-2 document, remove unknown fields, and check the size, IDs, step targets, terminal paths and story tokens.