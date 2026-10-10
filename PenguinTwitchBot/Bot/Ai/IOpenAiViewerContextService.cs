using System.Collections.Generic;
using System.Threading.Tasks;

namespace PenguinTwitchBot.Bot.Ai
{
    /// <summary>
    /// Generates contextual channel, chatter role, and viewer profile information
    /// for OpenAI prompts and system instructions.
    /// </summary>
    public interface IOpenAiViewerContextService
    {
        /// <summary>
        /// Builds the full channel and viewer context XML block, including broadcaster, bot, caller,
        /// active chatters grouped by role, and rich profiles for any mentioned viewers.
        /// </summary>
        Task<string> BuildViewerContextAsync(
            string? promptText,
            string? instructionsText,
            IReadOnlyDictionary<string, string>? variables,
            int maxActiveChatters = 30);

        /// <summary>
        /// Builds the rich viewer profile context XML block (<mentioned_viewers>) for viewers
        /// referenced in the text or variables.
        /// </summary>
        Task<string> BuildMentionedViewersContextAsync(
            string? text,
            IReadOnlyDictionary<string, string>? variables = null);

        /// <summary>
        /// Returns a comma-separated list of currently active chatters.
        /// </summary>
        Task<string> GetActiveViewersListAsync(int maxActiveChatters = 50);

        /// <summary>
        /// Processes and replaces viewer context tags (%ViewersContext%, %ChannelContext%,
        /// %MentionedViewers%, %ActiveViewers%, &lt;channel_context&gt;, &lt;viewers_context&gt;) in the given template.
        /// </summary>
        Task<string> ProcessViewerTagsAsync(
            string template,
            string? promptText,
            string? instructionsText,
            IReadOnlyDictionary<string, string> variables);
    }
}

