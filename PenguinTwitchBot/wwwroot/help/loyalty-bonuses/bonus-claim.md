# Stream Bonus Points Claim (`!claim`)

The Bonus Tickets / Bonus Points feature encourages viewers to tune in to live broadcasts by offering a once-per-stream bonus reward.

## Default Command: `!claim`

Viewers who tune in can type `!claim` in chat to redeem their attendance bonus for the current stream session:
- **Default Command**: `!claim` (configurable per provider).
- **Eligibility**:
  - Available to viewers while the stream is actively live.
  - Can only be claimed once per stream broadcast per viewer.
  - Automatically resets when the stream goes offline and starts fresh on the next stream.

## Configuration Options

In the **Loyalty Bonuses** -> **Bonus Tickets** panel:
- **Point Type**: Select which currency is awarded when viewers claim (e.g. general points, giveaway tickets, or event tokens).
- **Points to Award**: The quantity of points granted upon each successful claim.
- **Bonus Claim Message**: Customizable chat confirmation template supporting `{Name}`, `{Amount}`, and `{PointType}` placeholders.
- **Already Claimed Message**: Friendly response sent if a viewer attempts to claim more than once during the same broadcast session.
- **Stream Offline Message**: Response if a user types the command when the stream is not currently live.

## Sub-Actions & Automation

- The `!claim` command can also be mirrored or triggered via sub-actions such as `ExecuteDefaultCommand`.
- Bonus tickets are commonly configured to reward giveaway tickets directly, ensuring dedicated viewers receive additional entries in upcoming stream raffles.

