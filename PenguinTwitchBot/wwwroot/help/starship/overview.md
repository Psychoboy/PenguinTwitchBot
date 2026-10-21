# Starship Expedition

A shared, long-form voyage. Viewers board, then vote in chat on occasional events while the ship travels between sectors. Everything is decided by the bot, so every overlay shows the same result.

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

Open **Events & Sectors** to search events, edit built-ins, add custom events or duplicate an event as a custom draft. Built-in IDs cannot be changed or deleted. Route Choice and Station Stop allow wording/art edits only; their defaults, choices and mechanics stay protected.

- **Apply Event** validates the draft and updates the page's unsaved catalog. **Save Settings** persists the catalog and also validates/applies an open draft. Switching tabs retains the draft. Apply or discard it before opening another event.
- Non-structural events expose enabled state, selection weight, route/sector filters and a terminal default choice. No selected routes or sectors means unrestricted availability. Every route/active-sector combination must still have an eligible event.
- Choose a shared reward profile or create an event-specific profile with the normal tier/reward/action editor. New profiles are applied with their event. Selecting no rewards disables payouts; custom choices that reveal rarity require a profile.
- Add two to six choices. Edit their IDs, labels, stories and bounded success/reward/loss modifiers. A continuing choice needs a unique granted flag so it disappears after use; the default must always remain available and end the event. Custom events do not have risky commit/confirmation choices.
- Set a subject style or name pool, object kind and optional images with the media-library picker. Loot wording can be supplied for each profile tier; blank wording uses the generic rarity find.
- **Preview Event** renders the description, option effects, choice stories and success/failure text using the sample seed, a sample viewer and the first enabled rarity tier. It does not run a voyage or award anything.

Story fields accept `{crew}`, `{crewlist}`, `{subject}`, `{Subject}`, `{loot}`, `{rarity}` and `{sector}`. Unknown tokens, invalid defaults, duplicate IDs/labels, command-prefixed text and out-of-range effects are rejected.

### Sharing one event

**Export JSON** downloads the current validated draft and fills the JSON text field. **Open JSON File** or **Import as Draft** loads a versioned single-event document, up to 256 KB. Imports receive a fresh custom ID and never overwrite existing events. A missing reward-profile reference remains an inline error until you select or create a local profile; profiles themselves are not included in the event export. Invalid data is rejected before it reaches the saved catalog.

Apply the draft and Save Settings when ready. Existing voyages keep their departure-time catalog; these changes only affect future voyages.

Some events resolve in one vote. Others are mini-voyages made of several steps:

- **Derelict Salvage**: Scan the wreck (reveals the actual rarity), Secure the area, then Recover salvage or Pass.
- **Unidentified Signal**: Scan the signal (reveals rarity), Trace its origin, then Investigate or Continue on course.
- **Navigation Hazard**: Maneuver (balanced), Raise shields (safer, smaller reward) or Hold course (riskier, bigger reward).

Choices that continue an event are used up once chosen. Each choice can change the success chance, the reward size and the loss exposure; the hints are shown next to the options in chat and on the overlay. Passing or continuing on course ends an event with no reward and no loss.

## Story

Each event gets a generated subject (for example "the ISV Quiet Meridian" or "the Kestrel-7 beacon") that stays the same for the whole event. Every choice adds a short story beat that names the viewers who picked it, and the outcome ends with what the crew found or lost, so each voyage reads differently.

## Appearance

Open the **Appearance** tab on the Starship page to restyle both widgets. Everything defaults to the original look.

- **Colours and style:** panel, text, accent, highlight and warning colours, panel opacity, corner radius and font. Tick *Use each ship's own accent colour* to keep the per-voyage ship colour, or untick it to use your accent everywhere.
- **Show or hide:** animations, join toasts, the route map, effect chips, vote bars, the odds and loss lines and the path footer.
- **Custom images:** pick ship images and per-object images (wreck, beacon, debris field, station, route fork) from the media library. They are mixed into the same pool as the built-in art, so each voyage's ship and each event's object is chosen automatically. Switch the built-in art off to use only your images; an object type with no images keeps its built-in art.
- **Live preview:** the tab shows sample data with your unsaved changes. Press **Save Settings** to keep them - connected overlays in OBS update immediately, without changing their URLs.

Images must be media-library files (a path starting with `/media/`) or an `http(s)` address.

## Success, rewards and losses

- Success and rarity are rolled **once per event** from the voyage seed, so results are repeatable and cannot be changed by the overlay.
- On success every eligible crew member earns unbanked points based on the revealed rarity.
- On failure the **whole crew** loses a share of their unbanked points. Securing first reduces that share.
- Unbanked points are banked at station stops, when the voyage returns, or when it is aborted. Failed payouts are retried without paying twice.
- Open **Reward Profiles** to create named profiles and assign them to event types. Multiple events can share one profile; choose **No rewards** to leave an event unbound.
- Each profile has ordered, enabled rarity tiers with names, badge colours and relative weights. Weights do not need to total 100; the editor displays each tier's resulting chance.
- A tier can award multiple point currencies, each with a minimum, maximum and optional bonus chance. Point limits default to **unlimited**. Add a limit to cap a currency per event or voyage; clearing a limit field or deleting its row makes that limit unlimited. An explicit voyage limit includes points already banked.
- Failure loss rules select a currency, percentage and optional maximum loss amount (unlimited by default). An explicit zero maximum prevents loss. Even without an amount cap, losses still respect the configured percentage and cannot exceed unbanked balances; banked points are safe.
- Previously saved numeric limits are preserved. Clear or delete them to make an existing profile unlimited. Per-reward payout ranges and configuration safety bounds are separate from these optional point limits.
- Profiles can run normal actions on success, on failure or after banking. See **Triggers & Actions** for dispatch and recovery behavior.
- Save settings before starting a voyage. Profiles and event bindings are copied at departure; editing them does not change a voyage already underway. Older saved profiles are converted to three equivalent tiers automatically.

## Chat commands

| Command | What it does |
| :--- | :--- |
| `!sj` | Join the crew (before launch, or any time while the voyage is under way) |
| `!sv <number>` | Vote for an option (also joins you if you are not aboard yet) |
| `!sc` | Confirm a risky choice (only for events that offer one) |
| `!starship status` | Show the voyage status |

## Overlays

- **Status widget** (`/starship-status.html`): compact progress, sector, crew and timer. It fades out while the ship is cruising between events and returns for boarding, events and the voyage end. Use the **Starship Show Status Widget** action to bring it back for a set time.
- **Event widget** (`/starship-event.html`): numbered choices, vote counts, the path taken, any revealed rarity and the result of each event.

Both widgets use the same real-time connection; if it drops they reconnect and reload the latest state.

## Catalog Storage and Recovery

Event definitions and reward profiles are stored as versioned database documents. Apply the matching SQLite or PostgreSQL migrations before running this version. On first load, existing reward profiles and event bindings are preserved and moved out of the settings document; a failed import leaves the previous settings intact.

New voyages copy their event catalog, reward profiles and bindings at departure. Editing settings cannot change an active voyage, including after a restart. Older voyages without a saved catalog continue using the built-in event definitions. Missing or unsupported catalog documents fail visibly rather than silently resetting to defaults.

Custom events use the same server-owned voting, step, reward, loss and banking paths as built-in events. Definitions permit two to six choices; more than four use a compact two-column overlay layout. Route Choice and Station Stop retain their fixed mechanics even when wording or art is customized.
