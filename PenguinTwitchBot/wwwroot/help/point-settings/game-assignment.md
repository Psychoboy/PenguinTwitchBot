# Game Assignment & Point Automation

Configured point types can be assigned across various bot features, games, and automation sub-actions.

## Assigning Point Types to Games

Each chat game in PenguinTwitchBot (Heist, FFA, Gamble, Slots, Roulette, Defuse, ModSpam, etc.) can be bound to any point type:
1. Navigate to [Game Settings](/settings/games).
2. Select any game provider card.
3. In the game configuration panel, locate the **Point Type** dropdown selector.
4. Choose the desired currency. When viewers play that game, entry costs are deducted from and winnings are deposited into that specific currency balance.

For stream overlay minigames (**Parachute Drop**, **Plinko Bounce**, **Balloon Ascent**, **Curling**, **Golf**, **Racing**, and **UFO Claw**), point rewards are configured either through the game's built-in point settings or through the **Outcome Automations** wizard on the game's page, which creates standard Actions using the `ManagePoints` sub-action.

## Automation & Sub-Actions

Point balances can be manipulated seamlessly within the Actions engine via dedicated sub-actions under **Points & Rewards**:

### 1. `ManagePoints` (Add / Remove / Set / Add Active)
- The primary sub-action for awarding, deducting, or setting viewer balances in any configured point currency.
- Supports dynamic variables (such as `%Score%`, `%PointsAwarded%`, `%WinningValue%`) and math formulas (such as `$math(%Score% * 10)$`).
- **Remove** safely leaves balances unchanged if a viewer has insufficient points so balances never drop below zero.

### 2. `GiftPoints`
- Transfers a configured amount of internal points from a designated sender (`FromUsername`) to a recipient (`TargetName`).
- Deducts points from the sender's balance before crediting the recipient, facilitating user-to-user transfers or bot transfers.

### 3. `CheckPoints`
- Reads the current point balance of a viewer into execution context variables (`%TargetPoints%`).
- Often paired with `LogicIfElse` to enforce balance requirements before executing expensive actions (e.g. sound effects, minigame entries, or overlay alerts).

### 4. `ExecutePointCommand`
- Invokes a point command programmatically as part of an action execution flow.

### 5. Integration with Raid Rewards & Loyalty Bonuses
- In [Raid Rewards](/settings/raidrewards), select which point type is awarded to raiders who participate in raids out to other channels.
- In [Loyalty Bonuses](/settings/loyalty-bonuses), select which point type is awarded for Twitch subs, cheers, and one-time stream bonus claims (`!claim`).
