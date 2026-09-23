# Point Types & Currencies

PenguinTwitchBot includes a multi-currency points engine. You can create multiple distinct point systems to power different features of your stream.

## Multi-Currency Concepts

Rather than being locked into a single balance per viewer, streamers can define separate currencies for different purposes:
- **Default Currency (ID 1)**: The core channel currency (e.g. "Points" or "Pasties"). This initial point type cannot be deleted, but you can rename and customize its description freely.
- **Giveaway / Raffle Tickets**: A dedicated point type awarded for participation, subscriptions, or events that viewers spend strictly on giveaway entries.
- **Minigame Tokens**: A secondary currency used exclusively for high-stakes chat games like Heist, FFA, or Roulette.
- **Seasonal / Event Currencies**: Temporary event currencies that can be used for charity goals, seasonal tournaments, or limited-time events.

## Managing Point Types

In the **Point Settings** dashboard under **Point Types**:
- **Creating a Point Type**: Enter a unique name and description in the right-hand form and click **Save**.
- **Editing a Point Type**: Click the Edit icon on any row to modify its name or description.
- **Deleting a Point Type**: Click the Delete icon. The primary point type (ID 1) cannot be deleted to guarantee system integrity. Deleting a secondary point type removes viewer balances associated with that specific currency type.
- **Viewing Assigned Chat Commands**: Expand any point type row to view the commands associated with that currency. Each currency has its own dedicated set of commands.

