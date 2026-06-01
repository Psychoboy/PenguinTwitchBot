namespace PenguinTwitchBot.Bot.Twitch.Models;

/// <summary>
/// Domain model for a user connected to the broadcaster's chat session.
/// </summary>
public record Chatter(
    string UserId,
    string UserLogin);
