# Starship Expedition

A shared, long-form voyage. Viewers board, then vote in chat on occasional events while the ship travels between sectors. Everything is decided by the bot, so every overlay shows the same result.

## Enable the Game

Starship Expedition is off by default, including after upgrading settings that do not yet contain an enable value. Use the **Enable Starship Expedition** switch beside the page title and save **Expedition Configuration** before starting a voyage. When off, chat commands are silently ignored and logged unless a voyage is already active; an active voyage continues to accept joins and votes and can still be advanced or stopped. The toggle does not abort a voyage already underway.

## Running a voyage

1. **Start** it from the Starship Expedition page (or with `!starship start [legs] [min minutes] [max minutes]` as a moderator).
2. During **boarding**, viewers join with `!sj` (long form: `!starship join`).
3. When boarding closes the ship launches. Viewers can still join **at any time** while the ship is cruising or an event is open, with `!sj` or simply by voting - a first vote enrolls them automatically. Events open on a timer; the crew votes with `!sv <number>` (the option ID also works).
   - If someone joins while an event is open, their vote counts straight away, but they only share that event's rewards and losses from the **next** event.
4. Moderators can use `!starship advance` to skip ahead and `!starship stop` to abort.
5. The voyage ends after the configured number of legs. Anything earned is banked to the crew.

## Sector Selection

In **Events & Sectors**, the **Sectors** section lets you enable or disable each built-in sector alongside the custom-sector list. All five built-ins are enabled by default, including when loading older settings.

- Keep at least two sectors available in total. You can disable every built-in if you add at least two custom sectors.
- Save the selection before starting a voyage. The seeded preview uses your unsaved selection; disabled sectors are excluded from both previews and new voyages.
- Active voyages keep the sector pool copied at departure, even after edits or a bot restart. Event filters can still reference disabled built-in sectors; those events become eligible there again if the sector is re-enabled.

## Events and choices

Open **Events & Sectors** to edit the catalog. The **Custom Events** help tab covers choices, profiles, filters, previews, story tokens, media and JSON sharing. **Save Event** persists an event immediately; each settings section has its own save button, and saving one section does not save unrelated edits. Active voyages keep their original definitions.

Courier Relay, Survey Cache and Debris Crossing are installed automatically as editable starter events. No manual import is needed. Their reward profiles remain under your control; installation does not select a currency or enable rewards. Edits and deletions are retained on later loads.

Some events resolve in one vote. Others are mini-voyages made of several steps:

- **Derelict Salvage**: Scan the wreck (reveals the actual rarity), Secure the area, then Recover salvage or Pass.
- **Unidentified Signal**: Scan the signal (reveals rarity), Trace its origin, then Investigate or Continue on course.
- **Navigation Hazard**: Maneuver (balanced), Raise shields (safer, smaller reward) or Hold course (riskier, bigger reward).

Choices that continue an event are used up once chosen. Each choice can change the success chance, the reward size and the loss exposure; the hints are shown next to the options in chat and on the overlay. Passing or continuing on course ends an event with no reward and no loss.

## Hull, Supplies and Stations

Enable **Ship Systems** in voyage settings. New voyages default to six legs, 80 starting hull, a 100 maximum hull and 55 supplies. Maximum Hull and Starting Hull are configurable, and Starting Hull cannot exceed its maximum. Supplies remain capped at 100. With the default stop interval of three, five- and six-leg voyages get one evenly placed station stop; seven-leg voyages get two. Travel costs 12 supplies per leg after the initial route choice. Failure can cost 15-25 hull and 10 supplies, so repairs and resupply can matter. Rules are copied at departure. Voyages started with ship systems disabled retain their original behavior.

- Travel after the initial boarding event costs 12 supplies per leg by default, once per event, not once per step.
- Empty supplies reduce event success chance by 15 percentage points by default. They do not force a return or prevent the safe default choice.
- Zero hull triggers an emergency return and banks remaining winnings. Already-banked points remain safe.
- Every station choice banks winnings. **Resupply** restores 40 supplies; **Repair the ship** restores the configurable hull amount (30 by default) for 15 shared supplies, capped at Maximum Hull; **Continue the voyage** grants +10 percentage points success for the next whole non-station event.
- The Continue bonus does not stack, survives steps and restart, and is consumed once. It changes success chance, not point amounts. An unaffordable repair is shown without a voting number.
- Reward-tier weights shift gradually with voyage progress: the first event after route choice uses the configured odds, and the final event can double the highest tier's weight. The curve is based on voyage percentage, so longer voyages ramp more slowly per event.
- Starting resources, travel cost, shortage penalty, station amounts/cost, Continue bonus and default event chance are configurable. Resource gains are clamped to 100.

Both widgets show resources and bonus state. Before voting, the event widget shows each choice's actual resource changes, supply payment, effective success chance and possible outcome damage. Supply changes are net of any separately displayed cost. Damage can be reduced by the choice's loss multiplier. The seeded route preview follows safe defaults; the catalog preview uses starting resources and no prior travel.

## Story

Each event gets a generated subject (for example "the ISV Quiet Meridian" or "the Kestrel-7 beacon") that stays the same for the whole event. Every choice adds a short story beat that names the viewers who picked it, and the outcome ends with what the crew found or lost, so each voyage reads differently.

## Appearance

Open the **Appearance** tab on the Starship page to restyle both widgets. Everything defaults to the original look.

- **Colours and style:** panel, text, accent, highlight and warning colours, panel opacity, corner radius and font. Tick *Use each ship's own accent colour* to keep the per-voyage ship colour, or untick it to use your accent everywhere.
- **Show or hide:** animations, join toasts, the route map, effect chips, vote bars, the odds and loss lines and the path footer.
- **Overlay visibility:** choose automatic overlays with no overlap (default: hides the small status widget while the large event/result widget is visible), status-on-demand, both-on-demand, or event-on-demand. See **Triggers & Actions** for the manual Show Widget actions.
- **Custom images:** pick ship images and per-object images (wreck, beacon, debris field, station, route fork) from the media library. They are mixed into the same pool as the built-in art, so each voyage's ship and each event's object is chosen automatically. Switch the built-in art off to use only your images; an object type with no images keeps its built-in art.
- **Live preview:** the tab shows sample data with your current appearance edits. Press **Save Appearance** to keep them - connected overlays in OBS update immediately, without changing their URLs.

Images must be media-library files (a path starting with `/media/`) or an `http(s)` address.

## Success, rewards and losses

- Success and rarity are rolled **once per event** from the voyage seed, so results are repeatable and cannot be changed by the overlay.
- On success every eligible crew member earns unbanked points based on the revealed rarity.
- On failure the **whole crew** loses a share of their unbanked points. Securing first reduces that share.
- Unbanked points are banked at station stops, when the voyage returns, or when it is aborted. Failed payouts are retried without paying twice.
- Open **Reward Profiles** to create named profiles and assign them to event types. The top dropdowns bind built-in event types to shared profiles; they do not change profile payouts. Multiple events can share one profile; choose **No rewards** to leave an event unbound.
- **Bulk Edit Rarity Rewards** can update an existing point type’s payout range/chance across selected profiles and tiers, or add a new point-type payout where it is missing. It changes unsaved settings only and requires preview and confirmation.
- Each profile has ordered, enabled rarity tiers with names, badge colours and relative weights. Weights do not need to total 100; the editor displays each tier's resulting chance.
- A tier can award multiple point currencies, each with a minimum, maximum and optional bonus chance. Point limits default to **unlimited**. Add a limit to cap a currency per event or voyage; clearing a limit field or deleting its row makes that limit unlimited. An explicit voyage limit includes points already banked.
- Failure loss rules select a currency, percentage and optional maximum loss amount (unlimited by default). An explicit zero maximum prevents loss. Even without an amount cap, losses still respect the configured percentage and cannot exceed unbanked balances; banked points are safe.
- Previously saved numeric limits are preserved. Clear or delete them to make an existing profile unlimited. Per-reward payout ranges and configuration safety bounds are separate from these optional point limits.
- Profiles can run normal actions on success, on failure or after banking. See **Triggers & Actions** for dispatch and recovery behavior.
- Save **Expedition Configuration** before starting a voyage. Profiles and event bindings are copied at departure; editing them does not change a voyage already underway. Older saved profiles are converted to three equivalent tiers automatically.

## Chat commands

| Command | What it does |
| :--- | :--- |
| `!sj` | Join the crew (before launch, or any time while the voyage is under way) |
| `!sv <number>` | Vote for an option (also joins you if you are not aboard yet) |
| `!starship status` | Show voyage status and current Hull/Supplies when ship systems are enabled |

## Overlays

- **Status widget** (`/starship-status.html`, default 520x180): ship, sector, crew, resources and a prominent next-event countdown. It fades out between events; new voyage activity brings it back for 30 seconds. Use the **Starship Show Status Widget** action to show it for a configured duration, including over an active event.
- **Event widget** (`/starship-event.html`, default 620x360): large numbered vote commands, up to four full-width choices, live vote counts/bars, concise consequences and per-crew results. Artwork takes a larger role during boarding/results rather than narrowing the voting area. Detailed odds, limits and event history remain in the streamer interface.

Normal choices stay still. Long custom choices scroll slowly only when they exceed the available voting area, with a reading pause at either end. Disabling animations or enabling the browser's reduced-motion preference stops automatic scrolling. Both widgets scale proportionally when their browser-source size changes; their default footprints have not increased.

Both widgets use the same real-time connection; if it drops they reconnect and reload the latest state.

## Audio

The event widget plays a cue when crew boarding opens, when a vote opens, and a distinct cue for an emergency hull-depletion return. It does not play sounds for individual votes or multi-step updates. The boarding, event-type defaults and emergency-return cues are configured in the **Audio** tab. Each event can inherit its event-type default, use a custom sound, or be muted in **Events & Sectors**. Sounds are selected from the Media Manager and must be available to the overlay at a `/media/...` path.

## Catalog Storage and Recovery

Event definitions and reward profiles are stored as versioned database documents. Apply the matching SQLite or PostgreSQL migrations before running this version. On first load, existing reward profiles and event bindings are preserved and moved out of the settings document; a failed import leaves the previous settings intact.

New voyages copy their event catalog, reward profiles and bindings at departure. Editing settings cannot change an active voyage, including after a restart. Older voyages without a saved catalog continue using the built-in event definitions. Missing or unsupported catalog documents fail visibly rather than silently resetting to defaults.

Custom events use the same server-owned voting, step, reward, loss and banking paths as built-in events. Each vote step permits two to four choices; three or four use a compact two-column overlay layout. Route Choice and Station Stop retain their fixed mechanics even when wording or art is customized.
