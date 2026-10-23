# UFO Claw Triggers and Variables

Use **UFO Claw Control** sub-actions for Show, Hide, Grab, Open Fresh Entries, Close Entries and Reset Winners. Show chooses prize/viewer mode and, for prizes, the saved set ID shown on the management page. Prize Set ID accepts variables. Grab uses the current pool. Open Fresh Entries intentionally replaces the previous roster. These are real action-system controls, not chat-command execution shortcuts.

Attach normal actions to **UFO Claw - Result** through Outcome Automations or the trigger manager. It fires at the reveal boundary, once per accepted grab. Optional Send Message actions announce the result; Manage Points or other actions fulfill rewards. No parallel direct reward/action selectors are used.

| Variable | Meaning |
| --- | --- |
| `%UfoClawResult%`, `%WinningLabel%` | Selected prize label or viewer display name |
| `%WinningValue%` | Prize's configured value, not an automatic payout |
| `%WinningMessage%` | Suggested announcement |
| `%WinningEntryId%`, `%WinningIndex%` | Stable entry ID and zero-based index in the frozen eligible pool |
| `%PrizeSetId%`, `%PrizeSetName%` | Selected prize set; viewer draws use the Viewers name |
| `%Mode%`, `%IsViewerDraw%` | `prizes`/`viewers`, and `true`/`false` |
| `%WinningUserId%`, `%WinningLogin%`, `%WinningDisplayName%` | Winner identity in viewer mode; empty for prizes |
| `%EntriesCount%` | Eligible entries when the grab was accepted |
| `%RemovedAfterWin%` | Whether this grab removes its winner |
| `%GrabId%`, `%OperationId%` | Unique accepted-grab identity |

All result variables are frozen from the actual draw, not current edited settings. In viewer mode, the trigger's regular user variables target the selected viewer. In prize mode they retain the action caller when known; a grab from the management page has system identity, not an inferred reward recipient. Select an explicit target before awarding personal points for a prize draw.

Example message: `The claw chose %WinningLabel% from %PrizeSetName%!`

Result state, removal and a dispatch claim are persisted before trigger dispatch. This provides at-most-once dispatch across recovery, not an exactly-once delivery guarantee. A crash after the claim can skip an action; the saved result remains for explicit operator review. Do not automatically replay reward actions. Hiding or reconnecting never creates another result.