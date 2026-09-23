# Triggers & Sub-Actions Reference

Penguin Twitch Bot connects incoming channel events to automation pipelines using Triggers and Sub-Actions.

---

## Triggers

Triggers define the circumstances that initiate an action:

- **Twitch Events**: Follows, subscriptions, resubscriptions, gift subs, raids, cheers (Bits), channel point custom rewards, hype trains, and stream online/offline.
- **Chat Commands & Keywords**: Custom prefixes, specific keywords detected in chat messages, or user-level filters (moderator, subscriber, VIP, broadcaster).
- **Timer Groups**: Scheduled ticks from configured background timers.
- **Hotkeys**: Global or local keyboard combinations pressed by the broadcaster.
- **Websocket / API**: Remote signals received from external stream deck utilities or webhooks.

---

## Sub-Actions

Sub-actions are the modular building blocks executed when an action fires:

### Stream & Broadcasting
- **OBS Studio**: Switch scenes, toggle source visibility, mute/unmute audio inputs, start/stop recording or streaming, set filter parameters.
- **Twitch Chat**: Send messages, reply to the triggering user, send whispers, announce in chat.
- **Text-to-Speech (TTS)**: Speak text using configured local or cloud voices.

### Overlay & Visual Effects
- **Overlay Alerts**: Send styled alerts, images, animations, or video to overlay browser sources.
- **Fireworks**: Trigger real-time particle firework bursts on the overlay (used by sample alert actions).
- **Stream Timers**: Start, pause, resume, or adjust overlay countdowns and countup timers.

### Variables & Logic
- **Global Variables**: Read and write persistent variables using `SetVariable`. Values can be passed to subsequent actions.
- **Delays & Pauses**: Introduce millisecond or second delays between sub-action steps.
- **Execute Action**: Trigger child or companion actions either synchronously or asynchronously.
- **Timer Control**: Enable or disable specific Timer Groups dynamically using `TimerGroupSetEnabledState`.

### Games & Economy
- **Viewer Points**: Award, deduct, or check loyalty points for the triggering user or target.
- **Wheel Spin**: Trigger spinning the interactive wheel widget.
- **Fishing Tournament**: Start or manage interactive chat fishing tournaments.

---

## Variable Replacement

Sub-actions support variable placeholders in text fields:
- `%user%` / `%username%`: The name of the viewer who triggered the event.
- `%rawInput%`: Any arguments passed after a command.
- `%targetUser%`: The recipient or target specified in command arguments.
- Custom variables saved in Global Variables or injected by previous sub-actions.

