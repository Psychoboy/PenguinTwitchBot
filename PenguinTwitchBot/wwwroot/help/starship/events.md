# Starship Custom Events

Use **Events & Sectors** on the Starship Expedition page to manage the catalog. The bot owns selection, voting, deadlines, outcomes and banking; event definitions contain data, not scripts.

## First Setup

1. Open an event with its edit icon, use **Add Event**, or duplicate a built-in as a custom draft.
2. Set its wording, availability, subject/art and choices. Select a reward profile if the event should award points or roll success/failure.
3. Use **Preview Event** to inspect the description, choice effects and story text with a sample seed. Previewing never starts a voyage, sends chat, runs actions or changes balances.
4. **Apply Event** validates the whole candidate catalog and updates the page's unsaved settings.
5. **Save Settings** persists the catalog. It also validates and applies an open draft; invalid drafts prevent saving. Drafts survive tab switches, but not closing or reloading the page.
6. Start a new voyage to use the saved catalog. A running voyage retains its departure-time catalog, reward profiles and sector pool, including after a restart.

Apply or discard the current draft before opening another event. Deleting a custom event requires confirmation and cannot leave a route/sector without an eligible event.

## Included Starter Events

These three editable events are added automatically the first time this catalog version is loaded. They do not replace existing IDs, are not duplicated on later loads, and stay deleted if you remove them. There is no extra import step. The JSON links below are available for sharing or making another copy.

| Event | Demonstrates | Availability | Rewards |
| :--- | :--- | :--- | :--- |
| [Courier Relay](/help/starship/examples/courier-relay.json) | A simple two-choice narrative event | All routes and sectors, weight 1 | No reward profile by default |
| [Survey Cache](/help/starship/examples/survey-cache.json) | Scan, optional stabilization, recovery or pass; named subjects and tier loot | Survey Route in Ternion Belt, weight 2 | References the SalvageSite profile |
| [Debris Crossing](/help/starship/examples/debris-crossing.json) | Balanced, safer or higher-reward choices with different loss exposure | All routes and sectors, weight 1 | References the Hazard profile |

The examples do not choose a point currency or enable an existing reward profile for you. Configure and enable the referenced profile to activate success/rarity rolls, payouts and failure rules. Without an enabled profile, Courier Relay and the other events still produce choice/story and Event Resolved feedback, but do not produce reward-roll success/failure outcomes. A scan only reveals rarity when an enabled reward profile is available.

Survey Cache cannot appear if Ternion Belt is disabled or the crew takes the Direct Route. Its no-vote default is to pass. Debris Crossing defaults to navigating the gaps. Courier Relay defaults to continuing the voyage.

## Identity and Availability

- Custom event IDs are unique lowercase slugs, for example `survey-cache`. Existing IDs cannot be renamed; duplicate the event to give it a new identity.
- Built-ins cannot be deleted. Random built-ins allow weights and route/sector filters. Route Choice and Station Stop allow wording/art only; their mechanics and default choices remain fixed.
- A selection weight is relative to other eligible events, not a percentage. Weight 2 is twice as likely as weight 1 when both are eligible. It is not a guarantee that an event will appear in a finite voyage.
- Leaving every route or sector unchecked means unrestricted availability, not disabled. Turn off **Enabled** to remove a random event from future selections.
- The catalog must provide an eligible random event for each route and active sector. Structural route/station events do not satisfy that requirement.
- Keep at least two effective sectors. Disabled built-ins may remain in event filters, but the voyage cannot visit them. Custom names must be unique, including against disabled built-ins.

## Choices and Steps

Each event offers two to six choices with unique IDs and labels. Five or six choices use the compact two-column overlay layout. There is no fixed cap on the number of events.

| Field | Meaning |
| :--- | :--- |
| Default Choice | The terminal, always-available choice used for a tied vote or no votes |
| Success Modifier | Adds -50 to +50 percentage points to success chance; the resulting chance is clamped to 0-100% |
| Reward Multiplier | 10-300% of the configured point reward; later steps combine with earlier multipliers |
| Loss Multiplier | 0-300% exposure against unbanked balances, subject to the profile's loss rules and balance protection |
| Continues Event | Opens another step instead of resolving the event; requires a unique granted flag |
| Granted Flag | Makes this choice disappear once used; continuing choices cannot loop forever |
| Reveals Rarity | Shows the event's seeded rarity; requires a reward-profile reference |
| Ends Without Reward | Resolves the event without rewards or losses |

Keep at least one terminal choice. The default cannot continue or grant a flag, because it must remain available on every step. Custom events do not support personal risky-commit/confirmation choices. Existing crew share an ordinary event; late joiners vote immediately but become reward/loss eligible only from the next whole event.

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

Point limits default to unlimited. An empty event/voyage/loss amount cap means unlimited; deleting a point-limit row removes it. Existing numeric caps are preserved, including an explicit zero. Voyage limits count amounts already banked. Unlimited loss amounts still respect the loss percentage and cannot reduce unbanked balances below zero; permanent banked points remain safe.

Earn-time eligibility uses the existing active-viewer/subscriber policy when **Only Reward Active Crew** is enabled. Rewards already earned are banked later without a new activity check. Personal payout triggers and group event actions have different scopes; see **Triggers & Actions** before attaching automations.

## Sharing JSON

**Export JSON** downloads one validated event and fills the JSON text field. **Open JSON File** or **Import as Draft** accepts the same schema-versioned format, up to 256 KB. Imports always get a new custom ID and never replace an existing event.

Reward profiles, point types and actions are not bundled in an event export. If its profile reference is absent locally, the editor opens an invalid draft so you can select or create a local profile. This placeholder validation does not create a saved profile. Remap the reference, Apply Event, then Save Settings.

## Troubleshooting

- **No event is eligible:** enable a random event or relax its route/sector filters. Changing weights alone does not fix an empty pool.
- **An example never appears:** check enabled state, route, sector and relative weight. Structural station/route slots are not random-event slots.
- **Scan does not reveal anything:** check that the event references an enabled reward profile with a weighted tier.
- **No points awarded:** check the profile/currency, chance, selected terminal choice, earn-time eligibility, late-join sequence and explicit caps. Passing deliberately pays nothing.
- **Changes do not affect the current voyage:** this is intentional snapshot behavior; they apply at the next departure.
- **An import is rejected:** use a single-event schema-version-1 document, remove unknown fields, and check the size, IDs, labels, terminal default and story tokens.