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
- **Min Amount**: Minimum points awarded on claim (defines the lower bound of the random award range).
- **Max Amount**: Maximum points awarded on claim (defines the upper bound of the random award range).
- **Win Message**: Sent to chat upon a successful claim. Supports `{Name}`, `{Amount}`, `{Total}`, and `{PointsName}` placeholders.
- **Error Message**: Sent to chat if the point award fails. Supports `{Name}` and `{PointsName}` placeholders.

## Sub-Actions & Automation

- The `!claim` command can also be mirrored or triggered via sub-actions such as `ExecuteDefaultCommand`.
- Bonus tickets are commonly configured to reward giveaway tickets directly, ensuring dedicated viewers receive additional entries in upcoming stream raffles.

