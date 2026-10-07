# OBS Sub-Actions & Automation

All 13 OBS sub-actions available when building custom Actions in [Manage Actions](/actions/manage).

---

### OBS Sub-Action Catalog

| Sub-Action | Description | Common Use Cases |
| :--- | :--- | :--- |
| **Set Scene** (`ObsSetSceneType`) | Switches the active OBS program scene. | Switch to "BRB" on stream break, or "Celebration" scene on hype train. |
| **Set Source Visibility** (`ObsSetSourceVisibilityType`) | Shows or hides an item in a specific scene. | Pop up an image meme, show webcam frame, toggle emote wall. |
| **Set Source Mute State** (`ObsSetSourceMuteStateType`) | Mutes, unmutes, or toggles an audio input. | Mute mic during cough trigger, unmute sound effects channel. |
| **Set Media State** (`ObsSetMediaStateType`) | Plays, pauses, restarts, or stops a media source. | Play a video clip or victory sting on follow/sub. |
| **Set Text** (`ObsSetTextType`) | Updates GDI+ / Freetype2 text source content. | Display latest subscriber name, high score, or shoutout target. |
| **Set Browser Source URL** (`ObsSetBrowserSourceUrlType`) | Changes the URL loaded in an OBS browser source. | Dynamically load a clip link or external web app widget. |
| **Set Image Source File** (`ObsSetImageSourceFileType`) | Updates the image path on an image source. | Swap overlay badges or display viewer avatar art. |
| **Set Media Source File** (`ObsSetMediaSourceFileType`) | Updates the video/audio file path on a media source. | Change background music or video stingers dynamically. |
| **Set Source Filter State** (`ObsSetSourceFilterStateType`) | Enables or disables an effect filter on a source. | Turn on blur, color correction, or green screen chroma key. |
| **Set Scene Filter State** (`ObsSetSceneFilterStateType`) | Enables or disables a filter on an entire scene. | Screen shake, invert colors, or black-and-white retro filter. |
| **Set Audio Track State** (`ObsSetSourceAudioTrackStateType`) | Controls routing of audio to specific OBS tracks (1-6). | Ensure VOD track excludes copyrighted music. |
| **Set Color Source Color** (`ObsSetColorSourceColorType`) | Changes the color of a solid color source. | Flash background red on damage alerts. |
| **Trigger Hotkey** (`ObsTriggerHotkeyType`) | Simulates pressing an OBS hotkey shortcut. | Trigger replay buffer save or start virtual camera. |

---

### Chaining with Delays & Logic

You can combine OBS sub-actions with **Delay** and **Logic (If / Else)** subactions:
```
1. Set Source Visibility -> Show "VictoryDance.webm"
2. Set Media State -> Play "VictoryDance.webm"
3. Delay -> 5,000 ms
4. Set Source Visibility -> Hide "VictoryDance.webm"
```

