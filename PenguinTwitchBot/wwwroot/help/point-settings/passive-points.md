# Passive Points

Each **Point Type** has its own independent **Passive Points** schedule and payout configuration. Passive points are only awarded while your stream is **Live**.

## Configuration Options (Per Point Type)

- **Enabled / Disabled Toggle**: Turn passive payouts on or off for this specific point currency.
- **Payout Interval (Minutes)**: How often passive points are distributed (minimum `1` minute). Different point types can run on completely different intervals (e.g., `Tickets` every 5 minutes, `Loyalty` every 1 minute).
- **All Viewers**: Base points awarded to everyone currently in the Twitch chat list.
- **Active Viewers Bonus**: Extra points awarded to viewers who have recently spoken in chat.
- **Subscribers Bonus**: Extra points awarded to channel subscribers.

## Additive (Stacking) Payout Rules

Passive point tiers **stack** together:

1. **Lurkers (All Viewers)**: Receive `All Viewers`.
2. **Active Chatters**: Receive `All Viewers + Active Viewers`.
3. **Subscribers in Chat**: Subscribers in chat are **always considered active at all times**, so they always receive `All Viewers + Active Viewers + Subscribers` (even if they are lurking).

Known bots are automatically excluded from all passive point payouts.

## Giveaway Page Dynamic Variables

In **Giveaway Settings → Passive Earnings**, you can use dynamic variables that pull directly from your giveaway's assigned Point Type and automatically calculate totals and hourly rates:

| Variable | Description |
| --- | --- |
| `{PointType}` | Name of the giveaway's point type (e.g., `Tickets`) |
| `{Interval}` / `{IntervalMinutes}` | Configured passive interval in minutes |
| `{AllViewers}` | Base points for all viewers per interval |
| `{ActiveViewers}` | Bonus points for active chatters per interval |
| `{Subscribers}` | Bonus points for subscribers per interval |
| `{ActiveTotal}` | Auto-calculated `AllViewers + ActiveViewers` per interval |
| `{SubTotal}` | Auto-calculated `AllViewers + ActiveViewers + Subscribers` per interval |
| `{AllViewersPerHour}` | Auto-calculated hourly rate for lurkers (`AllViewers × 60 / Interval`) |
| `{ActiveTotalPerHour}` | Auto-calculated hourly rate for active chatters (`ActiveTotal × 60 / Interval`) |
| `{SubTotalPerHour}` | Auto-calculated hourly rate for subscribers (`SubTotal × 60 / Interval`) |
| `{PointsPerEntry}` | Points required per 1 giveaway entry |

