namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.Chat;

/// <summary>
/// Optional. Metadata pertaining to the cheermote.
/// </summary>
public sealed class ChatCheermote
{
    /// <summary>
    /// 	The name portion of the Cheermote string that you use in chat to cheer Bits. The full Cheermote string is the concatenation of {prefix} + {number of Bits}. For example, if the prefix is “Cheer” and you want to cheer 100 Bits, the full Cheermote string is Cheer100. When the Cheermote string is entered in chat, Twitch converts it to the image associated with the Bits tier that was cheered.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;
    /// <summary>
    /// The amount of bits cheered.
    /// </summary>
    public int Bits { get; set; }
    /// <summary>
    /// The tier level of the cheermote.
    /// </summary>
    public int Tier { get; set; }
}