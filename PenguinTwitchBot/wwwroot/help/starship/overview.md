# Starship Expedition

A shared, long-form voyage. Viewers board, then vote in chat on occasional events while the ship travels between sectors. Everything is decided by the bot, so every overlay shows the same result.

## Running a voyage

1. **Start** it from the Starship Expedition page (or with `!starship start [legs] [min minutes] [max minutes]` as a moderator).
2. During **boarding**, viewers join with `!sj` (long form: `!starship join`).
3. When boarding closes the ship launches. Viewers can still join **at any time** while the ship is cruising or an event is open, with `!sj` or simply by voting - a first vote enrolls them automatically. Events open on a timer; the crew votes with `!sv <number>` (the option ID also works).
   - If someone joins while an event is open, their vote counts straight away, but they only share that event's rewards and losses from the **next** event.
4. Moderators can use `!starship advance` to skip ahead and `!starship stop` to abort.
5. The voyage ends after the configured number of legs. Anything earned is banked to the crew.

## Events and choices

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
- Reward odds, caps and loss limits are configured per event type under reward profiles.

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
