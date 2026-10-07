# Twitch Event Loyalty Bonuses

The Twitch Event Bonus system automatically awards configured channel points to viewers when significant Twitch events occur during your broadcast.

## Supported Twitch Events

- **Subscriptions**:
  - Direct subscriber signups and renewals (Tier 1, Tier 2, and Tier 3).
  - Configurable point multipliers for Tier 2 and Tier 3 subscribers.
- **Gifted Subscriptions**:
  - Points awarded to the generous gifter per sub gifted.
  - Optional bonus tiers for community bomb gifts (e.g. 5, 10, 20 gifts).
- **Cheers / Bits**:
  - Points awarded per bit cheered or on specific bit milestones.
  - Minimum bit threshold required to qualify for bonus point distribution.
- **Raids & Hosts**:
  - Points awarded to incoming raiding streamers or participating raiders.
- **Followers**:
  - Optional one-time welcome bonus points for new followers.

## Template Variables

When configuring event reward messages, the following dynamic placeholders are evaluated at runtime:
- `{Name}`: Username / display name of the viewer triggering the event.
- `{Amount}`: Number of bits cheered, subs gifted, or viewers brought in a raid.
- `{Points}`: Number of points awarded for the event.
- `{PointType}`: The display name of the point currency being awarded.
- `{Tier}`: Subscription tier (e.g. Prime, Tier 1, Tier 2, Tier 3).

## Enabling or Disabling Event Bonuses

Event bonus handling is managed via the [Loyalty Bonuses](/settings/loyalty-bonuses) dashboard. If disabled in feature flags or coordinators, the provider card will be hidden until enabled in bot feature settings.

