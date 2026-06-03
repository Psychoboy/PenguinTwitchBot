namespace PenguinTwitchBot.TwitchApi.EventSub.Models.Chat;

public sealed class ChatPrimePaidUpgrade
{
    /// <summary>
    /// The type of subscription plan being used. Possible values are:
    /// <para>1000 — First level of paid subscription</para>
    /// <para>2000 — Second level of paid subscription</para>
    /// <para>3000 — Third level of paid subscription</para>
    /// </summary>
    public string SubTier { get; set; } = string.Empty;
}