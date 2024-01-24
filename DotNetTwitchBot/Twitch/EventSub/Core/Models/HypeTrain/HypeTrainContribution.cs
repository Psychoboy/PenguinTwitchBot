namespace DotNetTwitchBot.Twitch.EventSub.Core.Models.HypeTrain;

/// <summary>
/// Describes a user's contribution to a HypeTrain
/// </summary>
public sealed class HypeTrainContribution
{
    /// <summary>
    /// The ID of the contributor.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>
    /// The display name of the contributor.
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>
    /// The login of the contributor.
    /// </summary>
    public string UserLogin { get; set; } = string.Empty;
    /// <summary>
    /// Type of contribution. Valid values include bits, subscription, other.
    /// </summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>
    /// The total contributed.
    /// </summary>
    public int Total { get; set; }
}