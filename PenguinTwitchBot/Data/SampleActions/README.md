# Sample Action Groups

Pre-configured action group exports that can be imported into PenguinTwitchBot via **Action Management → Import**.

## How to Import

1. Go to **Action Management**
2. Click **Import** in the top toolbar
3. Select a `.json` file from this folder
4. Configure queue name and group overrides as needed
5. Click **Import**

## Automatic Execute Action Re-linking

When importing, Execute Action sub-actions are **automatically re-linked by action name**. The importer resolves each Execute Action's target against:
1. Actions imported in the same batch (highest priority)
2. Actions already present in the system (fallback)

The number of links resolved is shown in the success message. If a target action cannot be found by name (e.g., a cross-group dependency whose other group hasn't been imported yet), that Execute Action will remain unlinked and will need to be set manually afterward.

---

## ActionGroup_Fireworks.json — Group: `Fireworks`

A fireworks display system driven by a recursive countdown loop. Callers set `%cumulative%` to the desired count, then dispatch the loop which fires one effect per iteration with a 100 ms delay.

### Required Queues

| Queue | Notes |
|-------|-------|
| `Default` | Entry point (`Fireworks - 10`) |
| `Fireworks` | All loop and effect actions |

### Actions

| Action | Trigger(s) | Count |
|--------|-----------|-------|
| **Fireworks** | *(internal — fires one effect)* | 1 (10 ms delay placeholder for actual effect) |
| **Fireworks - Multi** | *(internal — recursive loop engine)* | N (counts down `%cumulative%`, 100 ms between each) |
| **Fireworks - 5** | `ChannelFollow` | 5 |
| **Fireworks - 10** | `!fireworks` (Subscriber+ rank), "Fireworks" channel point reward | 10 |
| **Fireworks - 25** | `ChannelRaid` | 25 |
| **Fireworks - 50** | `ChannelSubscriptionGift` (1–9 gifts) | 50 |
| **Fireworks - 100** | *(no trigger — called by other systems)* | 100 |
| **Fireworks - Epic** | `ChannelSubscriptionGift` (10+ gifts), `!epicfireworks` | 250 |
| **Subscription Check Fireworks** | `ChannelSubscribe`, `ChannelSubscriptionMessage` | 25 (skips re-subs that had a prior sub via `HadPreviousSub` + `%IsRenewal%` check) |

### Execute Action Linking

All Execute Action links within this group are resolved automatically on import (all targets are in the same file).

### Variables

| Variable | Set by | Description |
|----------|--------|-------------|
| `cumulative` | Entry actions (e.g. Fireworks - 10) | Number of fireworks remaining to fire |

---

## ActionGroup_Fishing.json — Group: `Fishing`

A fishing mini-game. Players cast a line and receive Pasties based on the fish caught. Supports single casts, channel-point bulk fishing, and cheer-triggered fishing.

### Required Queues

| Queue | Notes |
|-------|-------|
| `FishingNonblock` | Individual fish attempts (non-blocking, allows parallel casts) |
| `Blocking` | The multi-fish loop (blocking, ensures sequential iteration) |
| `Default` | Help command |

### Actions

| Action | Trigger(s) | Description |
|--------|-----------|-------------|
| **Fish** | *(internal)* | Runs the `Fishing` sub-action (1 attempt), posts result to chat for non-Accident catches, then awards Points |
| **Fish - Multi** | *(internal)* | Recursive loop: calls `Fish` then delays 9 s, counts down `%cumulative%` |
| **Fish 1x** | `!fish` (90 s user cooldown, online-only) | Sets `cumulative = 1`, calls Fish - Multi |
| **Fish 5x** | "Fish 5x" channel point reward | Sets `cumulative = 5`, calls Fish - Multi |
| **Fish Cheer** | `ChannelCheer` (50+ bits) | Sets `cumulative = min(bits ÷ 50, 15)`, calls Fish - Multi |
| **Fish Help** | `!fishhelp` | Posts link to the fishing user guide |

### Execute Action Linking

All Execute Action links within this group are resolved automatically on import (all targets are in the same file).

### Variables

| Variable | Source | Description |
|----------|--------|-------------|
| `cumulative` | Entry actions | Casts remaining |
| `fish_rarity` | `Fishing` sub-action | e.g. `Common`, `Rare`, `Accident` |
| `fish_type` | `Fishing` sub-action | Fish species name |
| `fish_stars` | `Fishing` sub-action | Star rating (1–3) |
| `fish_gold` | `Fishing` sub-action | Base gold value |
| `fish_weight` | `Fishing` sub-action | Weight string |
| `award_multiplier` | Logic chain in `Fish` | 1★ → 1000 · 2★ → 4000 · 3★ → 8000 |
| `award_amount` | Calculated in `Fish` | `(fish_gold × 4) × (fish_stars × 4) × award_multiplier` |

### Accident Handling

If `%fish_rarity%` equals `Accident`, a special message is sent (`%user% had an accident and their %fish_type%`) and the normal catch message is suppressed. Points are still awarded.

---

## ActionGroup_PointsAndJackpot.json — Group: `Pasties`

Core commands for the Pasties stream-currency system, plus a jackpot win announcement.

### Required Queues

| Queue | Notes |
|-------|-------|
| `Default` | All actions in this group |

### Actions

| Action | Trigger(s) | Description |
|--------|-----------|-------------|
| **checkpoints** | `!check [user]` | Replies with the target's (or caller's) Pasties balance using `%targetorself%` |
| **Gift Pasties** | `!gift @user amount` (5 s user cooldown) | Transfers Pasties from caller to target via `GiftPoints` |
| **Redeem** | `!redeem` | Posts a list of available redemption options in chat |
| **Jackpot Win!** | *(no trigger — invoked by the Gamble system on jackpot)* | Fires 25 fireworks and reads a TTS announcement |

### Execute Action Linking

**Jackpot Win!** calls `Fireworks - 25` from the Fireworks group. If the Fireworks group is already imported (or imported in the same session before this file), the link resolves automatically. Otherwise, import the Fireworks group first — the importer will find `Fireworks - 25` by name as long as it exists in the system.
