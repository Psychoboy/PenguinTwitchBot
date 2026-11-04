# Raffles

The old hard-coded Bacon, Pancake, and Waffle raffles have been replaced with a generic raffle runtime driven by Actions, Triggers, and SubActions.

This gives you:

- Multiple raffles running at the same time as long as each raffle uses a different raffle key and different commands.
- Full control over start, join, end, messages, reminders, and follow-up logic from the Action Management UI.
- A guided **Timed Raffle** recipe that creates join, start, and end commands, selects the award point type, and optionally adds timed announcements and automatic closing.

## Quick Start

Create a raffle from the guided recipe in Action Management.

1. Open Action Management and choose **Create from recipe > Timed raffle**.
2. Set the raffle name/key and join, start, and end commands.
3. Choose an award point type, winner count, and total award.
4. Configure duration, announcement cadence, and message variants.
5. Choose the action group and queue, and whether to include starter chat messages.
6. Create the raffle.

The wizard creates three actions for you:

- `Raffle: NAME Start`
- `Raffle: NAME Join`
- `Raffle: NAME End`

It also creates the corresponding action commands and wires each action to its command trigger. The start sequence can close the raffle automatically after its configured duration; the end command remains available to moderators for early closure.

By default:

- The join command is viewer-usable.
- The start command is moderator-only.
- The end command is moderator-only.
- The timed start sequence uses the `Raffles` queue; join and end commands remain on `Default` so they can run while the timed sequence is waiting.

You can change command permissions or cooldowns later from the Action Commands page.

## How It Works

Each raffle is identified by its raffle key.

- `Raffle Start` opens the raffle and stores the current configuration in memory.
- `Raffle Enter` adds the current command user to the running raffle.
- `Raffle End` closes the raffle, randomly selects winners, and awards the configured total prize.
- `Raffle Set Winner Count` updates the winner count for a running raffle.
- `Raffle Set Total Award` updates the total award for a running raffle.
- `Raffle Get Entry Count` loads the current entry count into action variables.

Because the runtime is keyed, multiple raffles can coexist at the same time as long as the keys are different.

## Recommended Setup Pattern

The quick-create wizard gives you a solid starting point, but the system is designed to be extended.

Typical flow:

1. A start command triggers an action that runs `Raffle Start` and then sends a message.
2. A join command triggers an action that runs `Raffle Enter` and optionally sends different messages based on whether the user joined successfully.
3. An end command triggers an action that runs `Raffle End` and announces winners.
4. Optional timer groups or other actions can call `Raffle Get Entry Count`, `Raffle Set Winner Count`, or `Raffle Set Total Award`.

If you want reminders, overlays, OBS updates, Discord posts, or more advanced branching, add those as normal SubActions around the raffle subactions.

## Variables Written By Raffle SubActions

The raffle handlers write variables you can use in later subactions such as `Send Message`, `Logic: If/Else`, OBS actions, or other automation.

Common variables:

- `%raffle_success%`
- `%raffle_status%`
- `%raffle_key%`
- `%raffle_name%`
- `%raffle_join_command%`
- `%raffle_point_game%`
- `%raffle_is_active%`
- `%raffle_entry_count%`
- `%raffle_winner_count%`
- `%raffle_total_award%`
- `%raffle_username%`
- `%raffle_joined%`
- `%raffle_already_entered%`
- `%raffle_each_award%`
- `%raffle_awarded_total%`
- `%raffle_resolved_winner_count%`
- `%raffle_has_entries%`
- `%raffle_winners%`

Winner names are also exposed individually:

- `%raffle_winner_1%`
- `%raffle_winner_2%`
- and so on

## Common Examples

Start message:

```text
%raffle_name% started. Type !%raffle_join_command% to enter. %raffle_winner_count% winner(s), %raffle_total_award% %raffle_point_game% total.
```

Successful join message:

```text
%raffle_username% joined %raffle_name%. Entries: %raffle_entry_count%.
```

End message:

```text
%raffle_name% ended. Winner(s): %raffle_winners%. %raffle_each_award% %raffle_point_game% each.
```

## Notes

- Raffles are runtime state, so a raffle must be started before users can join it.
- The raffle key should stay stable across the actions that belong to the same raffle.
- If you duplicate raffle actions manually, make sure you update both the raffle key and the command names.
- The quick-create wizard is meant to save setup time, not limit you. Edit the generated actions freely after creation.