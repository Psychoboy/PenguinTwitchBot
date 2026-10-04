# Fishing Balance & Economy Analysis

The Fishing Balance Analysis dashboard provides data-driven telemetry into player activity, economy flow, item durability maintenance, accident risks, and simulated progression horizons across your channel.

---

## 1. Overview & Health Dashboard

- **Gross Gold / Cast**: Average and median gold earned per cast across all successful catches.
- **Accident Sink / Cast**: Risk-weighted gold lost to equipment accidents (Line snaps, Rod snaps, Reel jams, Tackle Box losses, Net breaks).
- **Durability Upkeep / Cast**: Operating maintenance wear cost per attempt. When actual player repairs are recorded, this reflects the observed gold spent on repairs during the selected date window; otherwise, it falls back to the theoretical equipment wear model based on configured `RepairCostMultiplier` and item degradation rates.
- **Net Gold / Cast**: The true bottom-line profit retained by players per cast (`Gross Gold - Upkeep - Accident Losses - Consumable Costs`).
- **Stream Catch Quality & Trophies**: Tracks the quality experience of fishing beyond gold — including 3-Star (⭐⭐⭐) catch rate, Rare+ catch chance (Rare, Epic, Legendary, Mythical), average fish weight, and the **Stream Trophy** (heaviest catch with fish name, weight, stars, and catcher).
- **Economy Health Diagnostics**: Automated warning system that alerts you if the game falls into a deflationary spiral (players losing gold), if failure rates are excessively punitive, or if item costs are too cheap or too grindy.

---

## 2. Theoretical vs. Real Telemetry

- **Side-by-Side Comparison**: Compares theoretical mathematical expectations (pure baseline models) with actual streamer broadcast data, including catch success rate, gold yield, 3-star rate, rare+ chance, and average weight.
- **Accident Breakdown**: Individual counts and gold value lost across all five failure types: Rod Snaps, Line Snaps, Reel Jams, Tackle Box Losses, and Net Breaks.
- **Equipment Repair Breakdown**: Details observed repair telemetry including total repairs executed, total gold spent repairing gear, average repair cost, and per-slot maintenance totals (Rods, Reels, Tackle Boxes, etc.).
- **Player Engagement Cadence**: Analyzes viewer habits by percentiles:
  - **Casual Players**: 25th percentile casts per stream.
  - **Active Players**: Median (50th percentile) casts per stream.
  - **Hardcore Players**: 75th percentile casts per stream.
- **Multi-Month Progression Forecasting**: Projects 3-month, 6-month, and 12-month net gold accumulation and progress toward full endgame equipment loadouts.

---

## 3. Item Economy & Return on Investment (ROI)

- **Operating Cost / Cast**: Exact maintenance wear cost plus slot accident risk exposure for every shop item.
- **Bonus Gold / Cast**: Incremental gold lift delivered by the item's rarity, star, or weight boost multipliers.
- **Boost Summary & Identity**: Details the gameplay boosts delivered by each item (e.g. `+25% Rarity`, `+20% 3-Star`, `+45% Weight`) and its role in trophy hunting.
- **Net Value / Cast**: True net earnings contributed by equipping the item (`Bonus Gold - Operating Cost`).
- **Payback Period**: Estimated number of casts required for an item to pay for itself from its bonus earnings.
- **Economic & Trophy Ratings**:
  - `Profitable Investment`: Generates more bonus gold than its maintenance cost and repays its purchase price in a reasonable timeframe.
  - `Elite Trophy Gear`: High rarity boost gear designed for catching epic, legendary, and mythical fish.
  - `Master Quality Gear`: Precision gear focused on maximizing 3-Star trophy catch rates.
  - `Big Game Specialist`: Heavy strength gear maximizing fish weight records and monster catches.
  - `Fair Upgrade`: Net-positive upgrade that expands player catch potential.
  - `Luxury Sink`: Aspirational cosmetic or status item without major functional boosts.
  - `Great Value / Good Value`: Cost-effective consumable baits and lures.
  - `Consumable Sink`: High-cost consumable designed to drain excess gold.

---

## 4. Interactive Balance Simulator ("What-If" Knobs)

- **Real-Time Sandbox**: Test adjustments to failure rates, repair multipliers, and viewer habits without altering live database settings.
- **Accident Sliders**: Tune Line Snap, Rod Snap, Reel Jam, Tackle Box Loss, and Net Break chances with live feedback.
- **Durability Upkeep Slider**: Adjust the repair cost multiplier (0.0x to 1.0x) to balance gear maintenance. Setting this to 0.0x disables repairs (broken gear is removed upon depletion, with upkeep estimated at full replacement cost).
- **Pacing Projections**: Instantly calculates streams and weeks required to afford top-tier gear under simulated conditions.

---

## 5. Dynamic Pricing & Rebalancing

- **Target Progression Time**: Select your preferred progression horizon (e.g., 3 months, 6 months, 1 year to reach endgame).
- **Durability-Aware Rebalancer**: Computes fair pricing across all equipment tiers based on actual net gold accumulation.
- **Price Preview & 1-Click Apply**: Review item-by-item price changes with clear deltas and apply updates directly to your shop.

---

## Data Export

- **JSON Export**: Export the full balance report, telemetry data, and item ROI statistics for offline modeling or external review.
