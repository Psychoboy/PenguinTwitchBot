# Action Commands

The **Action Commands** manager (`/actions/commands`) links custom chat commands directly to PenguinTwitchBot's Actions automation pipeline.

## Linking Commands to Actions

When viewers type an action command in chat:
- The bot evaluates permissions, cooldowns, and point costs.
- If permitted, it invokes the target Action defined in **Action Manager**.
- All configured sub-actions (playing audio, triggering overlay animations, firing OBS hotkeys, sending webhooks, awarding points) execute sequentially.

## Runtime Variables from Commands

When an action command executes, the following variables are automatically injected into the sub-action execution context:
- `{Command}`: The command trigger name that was invoked.
- `{Args}`: The full string of arguments supplied by the viewer.
- `{TargetUser}`: The first username mentioned in the command arguments.
- `{User}`: Username of the chatter who ran the command.
- `{DisplayName}`: Display name of the chatter.
- `{UserId}`: Twitch User ID of the chatter.
- `{IsSub}` / `{IsMod}` / `{IsVip}`: Boolean flags reflecting the chatter's channel status.

## Economy & Access Rules

Just like default commands, action commands can require point costs (`Cost` and `PointType`), user/global cooldowns, and minimum rank permissions.
