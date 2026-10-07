using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables.Triggers;

/// <summary>
/// Resolves variables for miscellaneous triggers (BannedSongRequest, Manual, and general fallbacks).
/// </summary>
public class MiscellaneousTriggerVariableResolver : ITriggerVariableResolver
{
    public bool CanHandle(TriggerTypes triggerType) =>
        triggerType is TriggerTypes.BannedSongRequest or TriggerTypes.Manual;

    public void ResolveVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        const string category = "Triggers";
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();

        switch (trigger.Type)
        {
            case TriggerTypes.BannedSongRequest:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Banned Song: {triggerName})" : $"Trigger: Banned Song Request ({triggerName})";
                variables.SetVariable("User", "Chatter who requested the banned song", source, category, "viewer");
                variables.SetVariable("DisplayName", "Display name of the chatter", source, category, "Viewer");
                variables.SetVariable("banned_song_id", "YouTube ID of the banned song", source, category, "dQw4w9WgXcQ");
                variables.SetVariable("banned_song_title", "Title of the banned song", source, category, "Never Bank");
                variables.SetVariable("banned_song_reason", "Reason why the song was banned", source, category, "Overplayed");
                variables.SetVariable("banned_song_banned_by", "Streamer or moderator who banned the song", source, category, "streamer");
                variables.SetVariable("banned_song_url", "YouTube URL of the banned song", source, category, "https://youtu.be/dQw4w9WgXcQ");
                variables.SetVariable("banned_song_request", "Original song request input string from chat", source, category, "!sr dQw4w9WgXcQ");
                break;
            }

            case TriggerTypes.Manual:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Manual: {triggerName})" : $"Trigger: Manual ({triggerName})";
                variables.SetVariable("User", "The user or broadcaster manually executing the action", source, category, "broadcaster");
                variables.SetVariable("UserName", "The login username of the executor", source, category, "broadcaster");
                variables.SetVariable("DisplayName", "The display name of the executor", source, category, "Broadcaster");
                break;
            }

            default:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} ({triggerName})" : $"Trigger: {triggerName}";
                variables.SetVariable("User", "The user or initiator triggering this action", source, category, "viewer");
                break;
            }
        }
    }
}

