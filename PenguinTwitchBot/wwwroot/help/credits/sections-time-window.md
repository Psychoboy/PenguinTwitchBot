# Sections & Flexible Time Windows

## Grouping or Splitting Topics Into Sections

Each credits section accepts **one or more source topics**:

- **Group topics under one header**: Select both **New Subscribers** and **Resubscribers** inside one *"Subscribers"* section so first-time subs and resubs roll together.
- **Keep related topics separate**: Put **Sub Gifters** in its own *"Sub Gifters"* section with its own header, formatting template, and layout.
- **Empty sections stay hidden**: By default, **Hide section automatically when empty** is enabled (`true`). If nobody cheered bits or raided during that stream, the section is omitted cleanly rather than leaving blank headings. Turn it off if you want to show a custom fallback message.

### Entry Formatting Tokens

Customize how each row is displayed with **Entry Format Template**:
- `{name}` — Viewer display name
- `{amount}` — Event count / bits / raid viewers / sub count (hidden when **Show Amount** is off)
- `{detail}` — Event detail (reward title, drop game, song title, static cast role)
- `{tier}` — Subscription tier (`Tier 1`, `Tier 2`, `Tier 3`, `Prime`)

## Time Windows (When Credits Start)

Choose exactly when events begin counting for each profile:

- **Since Stream Start** *(default)*: Starts at the active live stream session start minus **Pre-Stream Grace Minutes** (default 15 minutes so pre-roll cheers/follows count). If offline, falls back gracefully to your most recent recorded stream session.
- **Last Completed Stream**: Uses the most recently ended stream session.
- **Rolling Lookback**: Fixed time window back from now (e.g., last 6 hours, 180 minutes, or 7 days).
- **Since Manual Checkpoint**: Starts from a manual timestamp saved by clicking **Mark Checkpoint Now** or running the **Credits: Mark Start Checkpoint** Action sub-action (great for multi-day subathons).
- **Custom UTC Date Range**: Pin an exact start and optional end date/time.
- **Specific Recorded Session**: Replay credits from any historical recorded stream session.
