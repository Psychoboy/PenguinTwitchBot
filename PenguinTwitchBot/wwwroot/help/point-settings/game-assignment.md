# Game Assignment & Point Automation

Configured point types can be assigned across various bot features, games, and automation sub-actions.

## Assigning Point Types to Games

Each game in PenguinTwitchBot (Heist, FFA, Gamble, Slots, Roulette, Defuse, ModSpam, etc.) can be bound to any point type:
1. Navigate to **Game Settings** (`/settings/games`).
2. Select any game provider card.
3. In the game configuration panel, locate the **Point Type** dropdown selector.
4. Choose the desired currency. When viewers play that game, entry costs are deducted from and winnings are deposited into that specific currency balance.

## Automation & Sub-Actions

Point balances can be manipulated seamlessly within the Actions engine via dedicated sub-actions:

### 1. `GiftPoints`
- Transfers a configured amount of internal points from a designated sender (`FromUsername`) to a recipient (`TargetName`).
- Deducts points from the sender's balance before crediting the recipient, facilitating user-to-user transfers or bot transfers.

### 2. `CheckPoints`
- Reads the current point balance of a viewer into execution context variables.
- Often paired with `LogicIfElse` to enforce balance requirements before executing expensive actions (e.g. sound effects or overlay alerts).

### 3. `ExecutePointCommand`
- Invokes a point command programmatically as part of an action execution flow.

### 4. Integration with Raid Rewards & Loyalty Bonuses
- In **Raid Rewards** (`/settings/raidrewards`), select which point type is awarded to raiders who participate in raids out to other channels.
- In **Loyalty Bonuses** (`/settings/loyalty-bonuses`), select which point type is awarded for Twitch subs, cheers, and one-time stream bonus claims (`!claim`).

